using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Shared.Platform;
using Sillar.Shared.Replication;

namespace Sillar.Core.Authorization;

/// <summary>Una fila de <c>core.installation</c>, solo lo que la procedencia necesita.</summary>
internal sealed record InstallationProvenanceRow(Guid InstallationKey, bool IsSetupComplete, string ProductVersion);

/// <summary>
/// La regla de procedencia, sin base de datos ni reloj propios.
/// </summary>
internal static class InstallationProvenanceEvaluator
{
    /// <summary>De dónde salen los hechos.</summary>
    public const string Source = "core.installation;configuration:" + NodeIdentity.SettingKey + ";assembly";

    public static InstallationProvenanceResult Evaluate(
        IReadOnlyList<InstallationProvenanceRow> rows,
        string? configuredNode,
        string stampedNode,
        string runningVersion,
        DateTimeOffset observedAt)
    {
        if (rows.Count == 0)
        {
            return InstallationProvenanceResult.Unavailable(ProvenanceFailure.InstallationMissing, observedAt);
        }

        if (rows.Count > 1 || rows[0].InstallationKey == Guid.Empty)
        {
            return InstallationProvenanceResult.Unavailable(ProvenanceFailure.InstallationIdentityInvalid, observedAt);
        }

        var row = rows[0];

        if (!row.IsSetupComplete)
        {
            return InstallationProvenanceResult.Unavailable(ProvenanceFailure.InstallationIncomplete, observedAt);
        }

        // NodeIdentity cae en «principal» si no hay nada configurado. Ese valor
        // sirve para trabajar, pero no es un hecho: nadie dijo que este nodo sea ese.
        if (configuredNode is null)
        {
            return InstallationProvenanceResult.Unavailable(ProvenanceFailure.NodeNotConfigured, observedAt);
        }

        if (!string.Equals(configuredNode, stampedNode, StringComparison.Ordinal))
        {
            return InstallationProvenanceResult.Unavailable(ProvenanceFailure.NodeInconsistent, observedAt);
        }

        return InstallationProvenanceResult.Observed(
            new InstallationProvenance(row.InstallationKey, stampedNode, row.ProductVersion, runningVersion, Source),
            observedAt);
    }
}

/// <summary>
/// Productor candidato: lee <c>core.installation</c> en cada llamada, con ámbito de
/// petición. Mismas reglas de cancelación y de reloj que
/// <see cref="CandidateAssistantAccessAuthority"/>.
/// </summary>
internal sealed class InstallationProvenanceReader(
    CoreDbContext database,
    IConfiguration configuration,
    NodeIdentity node,
    TimeProvider clock,
    ILogger<InstallationProvenanceReader> logger) : IInstallationProvenance
{
    /// <inheritdoc />
    public async Task<InstallationProvenanceResult> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<InstallationProvenanceRow> rows;
        try
        {
            rows = await RowsQuery().ToListAsync(cancellationToken);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw exception as OperationCanceledException
                ?? new OperationCanceledException("Lectura cancelada por el llamador.", exception, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "No se pudo leer la procedencia de la instalación; no se emite.");
            return InstallationProvenanceResult.Unavailable(ProvenanceFailure.AuthorityUnavailable, clock.GetUtcNow());
        }

        return InstallationProvenanceEvaluator.Evaluate(
            rows,
            NodoParaMigrar.Configurado(configuration),
            node.Code,
            SillarProduct.Version,
            clock.GetUtcNow());
    }

    /// <summary>Una sola sentencia; <c>Take(2)</c> detecta una segunda instalación.</summary>
    internal IQueryable<InstallationProvenanceRow> RowsQuery()
        => database.Installations
            .AsNoTracking()
            .Select(installation => new InstallationProvenanceRow(
                installation.InstallationKey,
                installation.IsSetupComplete,
                installation.ProductVersion))
            .Take(2);
}

/// <summary>Registro explícito y opcional del productor candidato de procedencia.</summary>
/// <remarks>No lo llama <c>CoreServices</c> ni el host. Queda sin invocar hasta la puerta de JP.</remarks>
public static class CandidateInstallationProvenanceRegistration
{
    /// <summary>Registra <see cref="IInstallationProvenance"/> con el productor candidato, con ámbito de petición.</summary>
    public static IServiceCollection AddCandidateInstallationProvenance(this IServiceCollection services)
    {
        services.AddScoped<IInstallationProvenance, InstallationProvenanceReader>();
        return services;
    }
}
