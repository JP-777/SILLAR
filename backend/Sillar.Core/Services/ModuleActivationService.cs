using Microsoft.EntityFrameworkCore;
using Sillar.Core.Auditing;
using Sillar.Core.Contracts.Events;
using Sillar.Core.Data;
using Sillar.Core.Domain.Values;
using Sillar.Core.Dtos;
using Sillar.Core.Modularity;
using Sillar.Shared.Events;
using Sillar.Shared.Modularity;

namespace Sillar.Core.Services;

/// <summary>Cómo terminó un cambio de activación.</summary>
internal enum ActivationOutcome
{
    /// <summary>Cambiado. El host tiene que reiniciarse.</summary>
    Changed,

    /// <summary>Ya estaba en ese estado. No se toca nada.</summary>
    NoChange,

    /// <summary>Ese código de módulo no existe.</summary>
    NotFound,

    /// <summary>El grafo no lo permite, o es CORE.</summary>
    Conflict
}

/// <summary>Resultado de un cambio de activación.</summary>
internal sealed record ActivationOperation(
    ActivationOutcome Outcome,
    string? Error = null,
    bool IsActive = false);

/// <summary>
/// Consulta y cambio de las activaciones de módulos.
/// </summary>
/// <remarks>
/// Los módulos declarados en el código llegan por el contenedor: son los mismos
/// que descubrió el arranque. La base de datos solo aporta el estado.
/// </remarks>
internal sealed class ModuleActivationService(
    CoreDbContext database,
    DeclaredModules declared,
    IAuditWriter audit,
    IEventPublisher events,
    TimeProvider clock)
{
    private IReadOnlyList<IModule> Catalog => declared.Modules;

    /// <summary>Devuelve el catálogo completo con su estado y sus bloqueos.</summary>
    public async Task<IReadOnlyList<ModuleResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await ReadActivationsAsync(cancellationToken);
        var activeCodes = ActiveCodesOf(rows);

        return
        [
            .. Catalog
                .OrderBy(module => module.DisplayOrder)
                .ThenBy(module => module.Code, StringComparer.Ordinal)
                .Select(module => Describe(module, rows, activeCodes))
        ];
    }

    /// <summary>Activa o desactiva un módulo.</summary>
    /// <remarks>
    /// El estado resultante se comprueba con <see cref="ModuleGraph.ValidateActivations"/>,
    /// que es <b>la misma función</b> que ejecuta el arranque en el paso 6 del
    /// SPEC §7. No es una comprobación parecida: es la misma.
    ///
    /// Importa porque el host se detiene justo después de responder. Si aquí se
    /// aceptara un estado que el arranque rechaza, el proceso se pararía, no
    /// volvería a levantarse y la instalación solo se recuperaría por SQL.
    /// </remarks>
    public async Task<ActivationOperation> SetActiveAsync(
        string code,
        bool activate,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var module = Catalog.FirstOrDefault(
            candidate => string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase));

        if (module is null)
        {
            return new ActivationOperation(ActivationOutcome.NotFound);
        }

        if (module.Code == ModuleGraph.CoreCode && !activate)
        {
            return new ActivationOperation(
                ActivationOutcome.Conflict,
                "CORE no se puede desactivar: es la base sobre la que se enchufa todo lo demás.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var rows = await ReadActivationsAsync(cancellationToken);
        var activeCodes = ActiveCodesOf(rows);

        if (activeCodes.Contains(module.Code) == activate)
        {
            return new ActivationOperation(ActivationOutcome.NoChange, IsActive: activate);
        }

        if (Blocks(module, activeCodes, activate) is { } blocked)
        {
            return new ActivationOperation(ActivationOutcome.Conflict, blocked);
        }

        var activation = await database.ModuleActivations
            .FirstOrDefaultAsync(candidate => candidate.ModuleId == rows[module.Code].ModuleId, cancellationToken);

        if (activation is null)
        {
            // El sincronizador crea una fila por módulo al arrancar, así que
            // llegar aquí significa que alguien la borró a mano.
            return new ActivationOperation(
                ActivationOutcome.Conflict,
                $"El módulo '{module.Code}' no tiene fila de activación. Reinicia el host para que se cree.");
        }

        var now = clock.GetUtcNow();
        activation.IsActive = activate;

        if (activate)
        {
            activation.ActivatedAt = now;
        }
        else
        {
            activation.DeactivatedAt = now;
        }

        await database.SaveChangesAsync(cancellationToken);

        // Se relee lo que ha quedado escrito, en lugar de fiarse del conjunto
        // calculado en memoria, y se pasa por el validador del arranque antes de
        // confirmar. Si el host no arrancaría con esto, se deshace y no queda
        // rastro.
        var resulting = ActiveCodesOf(await ReadActivationsAsync(cancellationToken));
        var problems = ModuleGraph.ValidateActivations(Catalog, resulting);

        if (problems.Count > 0)
        {
            await transaction.RollbackAsync(cancellationToken);

            return new ActivationOperation(
                ActivationOutcome.Conflict,
                "La operación dejaría una instalación que no arranca: " + string.Join(" ", problems));
        }

        await audit.WriteAsync(
            new AuditEntry(activate ? AuditAction.Activate : AuditAction.Deactivate)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = "module",
                EntityId = module.Code,
                Summary = activate
                    ? $"Módulo '{module.Code}' activado."
                    : $"Módulo '{module.Code}' desactivado."
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Después de confirmar: un evento sobre algo que aún podría deshacerse
        // sería una noticia falsa.
        await events.PublishAsync<object>(
            activate ? new ModuleActivated(module.Code, now) : new ModuleDeactivated(module.Code, now),
            cancellationToken);

        return new ActivationOperation(ActivationOutcome.Changed, IsActive: activate);
    }

    /// <summary>Explica qué impide la operación, o <c>null</c> si nada la impide.</summary>
    private string? Blocks(IModule module, IReadOnlySet<string> activeCodes, bool activate)
    {
        if (activate)
        {
            var missing = ModuleGraph.MissingHardDependencies(Catalog, activeCodes, module.Code);

            return missing.Count == 0
                ? null
                : $"'{module.Code}' no puede activarse porque necesita módulos que están inactivos: " +
                  $"{string.Join(", ", missing)}. Actívalos primero.";
        }

        var dependents = ModuleGraph.ActiveHardDependents(Catalog, activeCodes, module.Code);

        // Nada de desactivar en cascada: que apagar Servicios apague también
        // Seguimiento sin avisar es la clase de comodidad que un día apaga media
        // instalación con un clic. Se explica quién bloquea y decide la persona.
        return dependents.Count == 0
            ? null
            : $"'{module.Code}' no puede desactivarse porque estos módulos activos dependen de él: " +
              $"{string.Join(", ", dependents)}. Desactívalos primero.";
    }

    private ModuleResponse Describe(
        IModule module,
        IReadOnlyDictionary<string, ActivationRow> rows,
        IReadOnlySet<string> activeCodes)
    {
        rows.TryGetValue(module.Code, out var row);

        var isActive = activeCodes.Contains(module.Code);
        var isCore = module.Code == ModuleGraph.CoreCode;

        var missing = ModuleGraph.MissingHardDependencies(Catalog, activeCodes, module.Code);
        var dependents = ModuleGraph.ActiveHardDependents(Catalog, activeCodes, module.Code);

        return new ModuleResponse(
            module.Code,
            module.DisplayName,
            module.Description,
            module.Version,
            isCore,
            isActive,
            row?.ActivatedAt,
            row?.DeactivatedAt,
            row?.ExpiresAt,
            module.DisplayOrder,
            [.. module.HardDependencies],
            [.. module.SoftDependencies],
            CanActivate: !isActive && missing.Count == 0,
            CanDeactivate: isActive && !isCore && dependents.Count == 0,
            BlockedBy: isActive ? dependents : missing);
    }

    private async Task<IReadOnlyDictionary<string, ActivationRow>> ReadActivationsAsync(
        CancellationToken cancellationToken)
        => await database.Modules
            .AsNoTracking()
            .Select(module => new ActivationRow(
                module.Code,
                module.ModuleId,
                module.Activation != null && module.Activation.IsActive,
                module.Activation!.ActivatedAt,
                module.Activation!.DeactivatedAt,
                module.Activation!.ExpiresAt))
            .ToDictionaryAsync(row => row.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

    /// <summary>
    /// Códigos activos, limitados a los módulos que existen en el código.
    /// </summary>
    /// <remarks>
    /// Una fila activa de un módulo que esta versión del producto ya no incluye
    /// no cuenta: el validador razona sobre lo que hay, no sobre lo que hubo.
    /// </remarks>
    private IReadOnlySet<string> ActiveCodesOf(IReadOnlyDictionary<string, ActivationRow> rows)
        => Catalog
            .Where(module => rows.TryGetValue(module.Code, out var row) && row.IsActive)
            .Select(module => module.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private sealed record ActivationRow(
        string Code,
        int ModuleId,
        bool IsActive,
        DateTimeOffset? ActivatedAt,
        DateTimeOffset? DeactivatedAt,
        DateTimeOffset? ExpiresAt);
}
