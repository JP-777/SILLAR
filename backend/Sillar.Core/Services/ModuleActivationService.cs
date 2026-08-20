using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Core.Contracts.Events;
using Sillar.Core.Data;
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
/// <param name="Outcome">Cómo terminó.</param>
/// <param name="Error">
/// Motivo del rechazo, redactado para que lo lea una persona.
/// </param>
/// <param name="IsActive">Estado en el que queda el módulo.</param>
/// <param name="BlockedBy">
/// Códigos de los módulos que impiden la operación.
/// </param>
/// <remarks>
/// Los códigos van aparte del mensaje a propósito. El servidor explica el
/// motivo; convertir esos códigos en nombres visibles y en enlaces a su tarjeta
/// es lo único que solo la interfaz puede hacer, y necesita los datos, no una
/// frase de la que extraerlos.
/// </remarks>
internal sealed record ActivationOperation(
    ActivationOutcome Outcome,
    string? Error = null,
    bool IsActive = false,
    IReadOnlyList<string>? BlockedBy = null);

/// <summary>Motivo de un rechazo, con los códigos que lo provocan.</summary>
internal sealed record ActivationBlock(string Message, IReadOnlyList<string> BlockedBy);

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
    TimeProvider clock,
    HostRestarter restarter)
{
    /// <summary>
    /// Clave del bloqueo que serializa las activaciones de la instalación.
    /// </summary>
    /// <remarks>
    /// Constante y propia de CORE. El valor no significa nada por sí mismo: solo
    /// tiene que ser el mismo en todos los procesos de la instalación y no
    /// chocar con el que use otro módulo. Si algún día otro necesita un bloqueo
    /// de este tipo, que elija otro número y lo anote junto a este.
    /// </remarks>
    public const long ActivationLockKey = 5_111_401_001;

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

        // Antes de leer nada. Validar dentro de la transacción protege contra un
        // cambio malo, pero no contra DOS CAMBIOS BUENOS QUE JUNTOS SON MALOS:
        // con dos administradores operando a la vez, cada transacción ve su
        // propia instantánea, las dos se aprueban por separado y el estado
        // resultante impide arrancar.
        //
        // Va antes de la lectura a propósito: tomado después, cada transacción
        // ya tendría su instantánea y el bloqueo llegaría tarde para lo único
        // que debe impedir.
        //
        // Se libera solo al terminar la transacción, y no hace falta subir el
        // nivel de aislamiento.
        await database.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0})",
            [ActivationLockKey],
            cancellationToken);

        var rows = await ReadActivationsAsync(cancellationToken);
        var activeCodes = ActiveCodesOf(rows);

        if (activeCodes.Contains(module.Code) == activate)
        {
            return new ActivationOperation(ActivationOutcome.NoChange, IsActive: activate);
        }

        if (Blocks(module, activeCodes, activate) is { } blocked)
        {
            return new ActivationOperation(
                ActivationOutcome.Conflict,
                blocked.Message,
                BlockedBy: blocked.BlockedBy);
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
    /// <remarks>
    /// El mensaje NO lleva la lista de códigos embebida: esos van en
    /// <see cref="ActivationBlock.BlockedBy"/>, para que la interfaz pueda
    /// escribir los nombres visibles y enlazarlos a su tarjeta. La frase se basta
    /// sola cuando quien la recibe no sabe qué hacer con los datos.
    /// </remarks>
    private ActivationBlock? Blocks(IModule module, IReadOnlySet<string> activeCodes, bool activate)
    {
        if (activate)
        {
            var missing = ModuleGraph.MissingHardDependencies(Catalog, activeCodes, module.Code);

            return missing.Count == 0
                ? null
                : new ActivationBlock(
                    $"«{module.DisplayName}» necesita otros módulos que ahora mismo están " +
                    "inactivos. Actívalos primero.",
                    missing);
        }

        var dependents = ModuleGraph.ActiveHardDependents(Catalog, activeCodes, module.Code);

        // Nada de desactivar en cascada: que apagar Servicios apague también
        // Seguimiento sin avisar es la clase de comodidad que un día apaga media
        // instalación con un clic. Se explica quién bloquea y decide la persona.
        return dependents.Count == 0
            ? null
            : new ActivationBlock(
                $"«{module.DisplayName}» no se puede desactivar porque otros módulos activos " +
                "dependen de él. Desactívalos primero.",
                dependents);
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
            BlockedBy: isActive ? dependents : missing,
            RestartsAutomatically: restarter.RestartsAutomatically);
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
