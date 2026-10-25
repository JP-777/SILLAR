using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;

namespace Sillar.Core.Settings;

/// <summary>
/// Productor candidato de <see cref="IInstallationTimeZoneResolver"/>: lee la
/// clave con <see cref="IanaZoneCandidate.Read"/> y aplica la regla de la zona
/// inicial.
/// </summary>
internal sealed class InstallationTimeZoneResolver(ISettingsReader settings) : IInstallationTimeZoneResolver
{
    /// <inheritdoc />
    public InstallationTimeZoneResolution Resolve()
        => Resolve(IanaZoneCandidate.Read(settings), IInstallationTimeZoneResolver.InitialZoneId);

    /// <summary>La regla, sin lectura: recibe lo leído y la zona inicial.</summary>
    internal static InstallationTimeZoneResolution Resolve(IanaZoneEvaluation setting, string initialZoneId)
    {
        switch (setting.Outcome)
        {
            case IanaZoneOutcome.Missing:
                var initial = IanaZoneCandidate.Evaluate(initialZoneId);
                return initial.Outcome == IanaZoneOutcome.RuntimeResolvedCandidate
                    ? Resolved(initial.RuntimeId!, InstallationTimeZoneOrigin.ApprovedInitial, setting)
                    : Failed(InstallationTimeZoneFailure.InitialZoneUnresolvable, setting);

            case IanaZoneOutcome.RuntimeResolvedCandidate:
            // Ningún camino produce hoy IanaCanonicalVerified. Si llegara, la zona
            // se usa igual, pero sin verificar: esa evaluación no es una autoridad
            // de verificación ratificada.
            case IanaZoneOutcome.IanaCanonicalVerified:
                return Resolved(setting.RuntimeId!, InstallationTimeZoneOrigin.ConfiguredSetting, setting);

            default:
                // Inválido. No se cae a la zona inicial.
                return Failed(InstallationTimeZoneFailure.ConfiguredValueInvalid, setting);
        }
    }

    private static InstallationTimeZoneResolution Resolved(string zoneId, InstallationTimeZoneOrigin origin, IanaZoneEvaluation setting)
        => new(
            TimeZoneInfo.FindSystemTimeZoneById(zoneId),
            zoneId,
            origin,
            InstallationTimeZoneVerification.Unverified,
            null,
            setting);

    private static InstallationTimeZoneResolution Failed(InstallationTimeZoneFailure failure, IanaZoneEvaluation setting)
        => new(null, null, null, InstallationTimeZoneVerification.Unverified, failure, setting);
}

/// <summary>Registro explícito y opcional del productor candidato de zona horaria.</summary>
/// <remarks>No lo llama <c>CoreServices</c> ni el host.</remarks>
public static class CandidateInstallationTimeZoneRegistration
{
    /// <summary>Registra <see cref="IInstallationTimeZoneResolver"/> con el productor candidato.</summary>
    public static IServiceCollection AddCandidateInstallationTimeZone(this IServiceCollection services)
    {
        services.AddScoped<IInstallationTimeZoneResolver, InstallationTimeZoneResolver>();
        return services;
    }
}
