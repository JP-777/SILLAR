using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;
using Sillar.Core.Data;

namespace Sillar.Core.Settings;

/// <summary>Una búsqueda que puede encontrar, no encontrar o no poder mirar.</summary>
internal enum Lookup { Found, NotFound, Unavailable }

/// <summary>Los hechos que solo se leen cuando hay un valor configurado que el runtime resuelve.</summary>
internal sealed record TimeZoneFacts(
    TzdbObservation Tzdb,
    Lookup AttestationLookup,
    TimeZoneAttestation? Attestation,
    Lookup InstallationLookup,
    Guid? InstallationKey);

/// <summary>
/// La regla de zona y verificación, sin base ni reloj propios.
/// Acepta únicamente la evidencia completa de la autoridad CORE.
/// </summary>
internal static class InstallationTimeZoneEvaluator
{
    public static InstallationTimeZoneResolution Evaluate(
        IanaZoneEvaluation setting,
        string initialZoneId,
        TimeZoneFacts? facts,
        DateTimeOffset observedAt)
    {
        switch (setting.Outcome)
        {
            case IanaZoneOutcome.Missing:
                var initial = IanaZoneCandidate.Evaluate(initialZoneId);
                return initial.Outcome == IanaZoneOutcome.RuntimeResolvedCandidate
                    ? Zone(initial.RuntimeId!, InstallationTimeZoneOrigin.ApprovedInitial,
                        TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.InitialZoneNotAttestable,
                        setting, null, observedAt)
                    : NoZone(TimeZoneVerificationReason.InitialZoneUnresolvable, setting, observedAt);

            case IanaZoneOutcome.RuntimeResolvedCandidate:
                return Configured(setting, facts, observedAt);

            default:
                return NoZone(TimeZoneVerificationReason.ConfiguredValueInvalid, setting, observedAt);
        }
    }

    private static InstallationTimeZoneResolution Configured(IanaZoneEvaluation setting, TimeZoneFacts? facts, DateTimeOffset observedAt)
    {
        var zoneId = setting.RuntimeId!;

        InstallationTimeZoneResolution With(TimeZoneVerificationStatus status, TimeZoneVerificationReason reason, Guid? attestation = null)
            => Zone(zoneId, InstallationTimeZoneOrigin.ConfiguredSetting, status, reason, setting, attestation, observedAt);

        if (facts is null || !facts.Tzdb.IsAvailable)
        {
            return With(TimeZoneVerificationStatus.SourceNotVerifiable, TimeZoneVerificationReason.TzdbSourceUnavailable);
        }

        if (!facts.Tzdb.IsCanonical)
        {
            return With(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.NotCanonicalInSource);
        }

        switch (facts.AttestationLookup)
        {
            case Lookup.Unavailable:
                return With(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.AttestationUnavailable);
            case Lookup.NotFound:
                return With(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.NotAttested);
        }

        var attestation = facts.Attestation!;
        var id = attestation.AttestationId;

        if (attestation.RevokedAt is not null)
        {
            return With(TimeZoneVerificationStatus.Revoked, TimeZoneVerificationReason.AttestationRevoked, id);
        }

        if (facts.InstallationLookup != Lookup.Found || facts.InstallationKey is not { } key || key == Guid.Empty)
        {
            return With(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.InstallationUnavailable, id);
        }

        if (attestation.InstallationKey != key)
        {
            return With(TimeZoneVerificationStatus.Stale, TimeZoneVerificationReason.StaleInstallationChanged, id);
        }

        if (!string.Equals(attestation.ZoneId, zoneId, StringComparison.Ordinal))
        {
            return With(TimeZoneVerificationStatus.Stale, TimeZoneVerificationReason.StaleValueChanged, id);
        }

        if (!string.Equals(attestation.SourceName, facts.Tzdb.SourceName, StringComparison.Ordinal)
            || !string.Equals(attestation.SourceVersion, facts.Tzdb.SourceVersion, StringComparison.Ordinal))
        {
            return With(TimeZoneVerificationStatus.Stale, TimeZoneVerificationReason.StaleSourceChanged, id);
        }

        // Solo la operación de CORE y el instalador producen esta autoridad.
        // Las pruebas candidatas o una atestación incompleta no se promueven.
        if (id.Version != 7 || attestation.AttestedByAdminUserId <= 0
            || string.IsNullOrWhiteSpace(attestation.AttestedByName)
            || string.IsNullOrWhiteSpace(attestation.AttestedByHomeNode)
            || attestation.AuthorityVersion != InstallationTimeZoneService.AuthorityVersion
            || attestation.AttestedAt > observedAt)
            return With(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.AttestationAuthorityUnratified, id);
        return With(TimeZoneVerificationStatus.Verified, TimeZoneVerificationReason.AttestationAccepted, id);
    }

    private static InstallationTimeZoneResolution Zone(
        string zoneId,
        InstallationTimeZoneOrigin origin,
        TimeZoneVerificationStatus status,
        TimeZoneVerificationReason reason,
        IanaZoneEvaluation setting,
        Guid? attestationId,
        DateTimeOffset observedAt)
        => new(TimeZoneInfo.FindSystemTimeZoneById(zoneId), zoneId, origin, status, reason, setting, attestationId, observedAt);

    private static InstallationTimeZoneResolution NoZone(TimeZoneVerificationReason reason, IanaZoneEvaluation setting, DateTimeOffset observedAt)
        => new(null, null, null, TimeZoneVerificationStatus.Unverified, reason, setting, null, observedAt);
}

/// <summary>
/// Productor candidato de <see cref="IInstallationTimeZoneResolver"/>.
/// </summary>
/// <remarks>
/// Solo consulta la fuente, la atestación y la instalación cuando hay un valor
/// configurado que el runtime resuelve. El reloj se lee una vez, al final.
/// </remarks>
internal sealed class InstallationTimeZoneResolver(
    ISettingsReader settings,
    ITzdbSource tzdb,
    ITimeZoneAttestationStore attestations,
    CoreDbContext database,
    TimeProvider clock,
    ILogger<InstallationTimeZoneResolver> logger) : IInstallationTimeZoneResolver
{
    /// <inheritdoc />
    public async Task<InstallationTimeZoneResolution> ResolveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var setting = IanaZoneCandidate.Read(settings);
        TimeZoneFacts? facts = null;

        if (setting.Outcome == IanaZoneOutcome.RuntimeResolvedCandidate)
        {
            var observation = tzdb.Observe(setting.RuntimeId!);
            var (attestationLookup, attestation) = await ReadAttestationAsync(cancellationToken);
            var (installationLookup, key) = await ReadInstallationKeyAsync(cancellationToken);
            facts = new TimeZoneFacts(observation, attestationLookup, attestation, installationLookup, key);
        }

        return InstallationTimeZoneEvaluator.Evaluate(setting, IInstallationTimeZoneResolver.InitialZoneId, facts, clock.GetUtcNow());
    }

    private async Task<(Lookup, TimeZoneAttestation?)> ReadAttestationAsync(CancellationToken cancellationToken)
    {
        try
        {
            var found = await attestations.ReadCurrentAsync(cancellationToken);
            return found is null ? (Lookup.NotFound, null) : (Lookup.Found, found);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw Cancelled(exception, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "No se pudo leer la atestación de zona; queda sin verificar.");
            return (Lookup.Unavailable, null);
        }
    }

    private async Task<(Lookup, Guid?)> ReadInstallationKeyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var keys = await database.Installations
                .AsNoTracking()
                .Select(installation => installation.InstallationKey)
                .Take(2)
                .ToListAsync(cancellationToken);
            return keys.Count == 1 ? (Lookup.Found, keys[0]) : (Lookup.NotFound, null);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw Cancelled(exception, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "No se pudo leer la identidad de la instalación; la zona queda sin verificar.");
            return (Lookup.Unavailable, null);
        }
    }

    /// <summary>La cancelación del llamador se propaga como tal, venga como venga del proveedor.</summary>
    private static OperationCanceledException Cancelled(Exception exception, CancellationToken cancellationToken)
        => exception as OperationCanceledException
            ?? new OperationCanceledException("Resolución cancelada por el llamador.", exception, cancellationToken);
}

/// <summary>Registro explícito y opcional del productor candidato de zona horaria.</summary>
/// <remarks>
/// No lo llama <c>CoreServices</c> ni el host. Registra los puertos con sus
/// únicos adaptadores actuales, que no encuentran fuente ni atestación.
/// </remarks>
public static class CandidateInstallationTimeZoneRegistration
{
    /// <summary>Registra <see cref="IInstallationTimeZoneResolver"/> con el productor candidato.</summary>
    public static IServiceCollection AddCandidateInstallationTimeZone(this IServiceCollection services)
    {
        services.TryAddSingleton<ITzdbSource, UnavailableTzdbSource>();
        services.TryAddSingleton<ITimeZoneAttestationStore, NoTimeZoneAttestationStore>();
        services.AddScoped<IInstallationTimeZoneResolver, InstallationTimeZoneResolver>();
        return services;
    }
}
