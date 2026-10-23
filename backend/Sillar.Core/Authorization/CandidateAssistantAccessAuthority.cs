using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;
using Sillar.Core.Data;

namespace Sillar.Core.Authorization;

/// <summary>
/// Productor candidato de Fase A: lee la base en cada llamada, con ámbito de
/// petición, y delega la regla en <see cref="AssistantAccessEvaluator"/>.
/// </summary>
/// <remarks>
/// <para>
/// Nunca consulta <c>IModuleRegistry</c>, que es la foto del arranque, como
/// autoridad. No concede acceso en Fase A.
/// </para>
/// <para>
/// <b>Hechos coherentes.</b> Instalación y activación salen de una sola
/// sentencia SQL, así que PostgreSQL las ve en la misma instantánea: no puede
/// colarse un cambio entre leer una y leer la otra.
/// </para>
/// <para>
/// <b>Un solo instante.</b> El reloj se lee una vez, después de la lectura, y con
/// ese instante se compara todo y se firma la decisión. Leerlo después es lo
/// conservador: si algo vence mientras dura la consulta, cuenta como vencido.
/// </para>
/// </remarks>
internal sealed class CandidateAssistantAccessAuthority(
    CoreDbContext database,
    TimeProvider clock,
    ILogger<CandidateAssistantAccessAuthority> logger) : IAssistantAccessAuthority
{
    /// <summary>Código de módulo del Asistente (M12).</summary>
    internal const string AssistantModuleCode = "assistant";

    /// <inheritdoc />
    public async Task<AssistantAccessDecision> EvaluateCurrentInstallationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<AssistantFactsRow> rows;
        try
        {
            rows = await FactsQuery().ToListAsync(cancellationToken);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            // La cancelación es del llamador: se propaga como tal, venga con la
            // forma que venga del proveedor.
            throw exception as OperationCanceledException
                ?? new OperationCanceledException("Evaluación cancelada por el llamador.", exception, cancellationToken);
        }
        catch (Exception exception)
        {
            // Fallo del proveedor, tiempo agotado o cancelación interna: ninguna
            // de esas cosas es una prueba, así que no se convierten en una.
            logger.LogWarning(exception, "No se pudo leer la autoridad de acceso al Asistente; se deniega.");
            return AssistantAccessEvaluator.Unavailable(clock.GetUtcNow());
        }

        return AssistantAccessEvaluator.Evaluate(rows, clock.GetUtcNow());
    }

    /// <summary>
    /// La consulta de hechos. Una sola sentencia; <c>Take(2)</c> basta para saber
    /// si hay más de una instalación sin traerlas todas.
    /// </summary>
    internal IQueryable<AssistantFactsRow> FactsQuery()
        => database.Installations
            .AsNoTracking()
            .Select(installation => new AssistantFactsRow(
                installation.InstallationKey,
                installation.IsSetupComplete,
                installation.LicenseType,
                installation.LicensedUntil,
                database.Modules.Any(module => module.Code == AssistantModuleCode),
                database.Modules.Any(module => module.Code == AssistantModuleCode && module.Activation != null),
                database.Modules.Any(module => module.Code == AssistantModuleCode
                    && module.Activation != null
                    && module.Activation.IsActive),
                database.Modules
                    .Where(module => module.Code == AssistantModuleCode && module.Activation != null)
                    .Select(module => module.Activation!.ExpiresAt)
                    .FirstOrDefault()))
            .Take(2);
}

/// <summary>
/// Registro explícito y opcional del productor candidato.
/// </summary>
/// <remarks>
/// No lo llama <c>CoreServices</c>, ni <c>CoreModule</c>, ni el host. Queda sin
/// invocar hasta la revisión cruzada y la puerta posterior de JP.
/// </remarks>
public static class CandidateAssistantAuthorityRegistration
{
    /// <summary>Registra <see cref="IAssistantAccessAuthority"/> con el productor candidato, con ámbito de petición.</summary>
    public static IServiceCollection AddCandidateAssistantAuthority(this IServiceCollection services)
    {
        services.AddScoped<IAssistantAccessAuthority, CandidateAssistantAccessAuthority>();
        return services;
    }
}
