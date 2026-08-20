using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Domain.Values;
using Sillar.Shared.Modularity;

namespace Sillar.Core.Modularity;

/// <summary>Resultado de sincronizar el catálogo de módulos con la base.</summary>
/// <param name="Active">Módulos activos que además existen en el binario.</param>
/// <param name="ActiveCodesInDatabase">
/// Todo lo que <c>core.module_activations</c> marcaba activo antes de
/// sincronizar, exista o no en el binario. La ADR-019 lo usa para decidir si
/// el arranque puede continuar: lo que queda fuera de <see cref="Active"/> por
/// no estar declarado es, precisamente, lo que hay que nombrar al abortar.
/// </param>
public sealed record ModuleSyncResult(
    IReadOnlyList<ActiveModule> Active,
    IReadOnlyList<string> ActiveCodesInDatabase);

/// <summary>
/// Vuelca al schema <c>core</c> el catálogo de módulos que declara el código y
/// devuelve cuáles están activos en esta instalación.
/// </summary>
/// <remarks>
/// Son los pasos 4, 5 y la lectura del 6 del arranque (SPEC CORE §7).
///
/// La dirección importa: el código escribe en <c>core.modules</c> y en
/// <c>core.module_dependencies</c>, nunca al revés. Editar esas tablas a mano no
/// cambia nada, porque el siguiente arranque las reescribe. Lo que sí decide el
/// negocio es <c>core.module_activations</c>, y eso el sincronizador solo lo
/// crea cuando falta: jamás pisa una activación existente.
/// </remarks>
public sealed class ModuleSynchronizer(CoreDbContext database, ILogger<ModuleSynchronizer> logger)
{
    /// <summary>
    /// Sincroniza catálogo y dependencias, garantiza una fila de activación por
    /// módulo y devuelve los módulos activos.
    /// </summary>
    public async Task<ModuleSyncResult> SynchronizeAsync(
        IReadOnlyList<IModule> declared,
        CancellationToken cancellationToken)
    {
        var stored = await database.Modules
            .Include(module => module.Activation)
            .ToDictionaryAsync(module => module.Code, cancellationToken);

        // ADR-019: lo que la base ya marcaba activo, antes de tocar nada. Si el
        // binario no lo trae, el arranque tiene que abortar más abajo — aquí
        // solo se recoge el dato, sin decidir nada todavía.
        var activeCodesInDatabase = stored
            .Where(entry => entry.Value.Activation?.IsActive == true)
            .Select(entry => entry.Key)
            .ToList();

        SynchronizeCatalog(declared, stored);
        WarnAboutUnknown(declared, stored);

        // Guardado intermedio: las dependencias necesitan los identificadores de
        // los módulos recién insertados.
        await database.SaveChangesAsync(cancellationToken);

        var byCode = await database.Modules.ToDictionaryAsync(module => module.Code, cancellationToken);
        await SynchronizeDependenciesAsync(declared, byCode, cancellationToken);
        await EnsureActivationsAsync(declared, byCode, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);

        await MarkOrphanMediaAsync(declared, cancellationToken);

        var active = await ReadActiveAsync(declared, cancellationToken);
        return new ModuleSyncResult(active, activeCodesInDatabase);
    }

    /// <summary>
    /// Marca como huérfanos los archivos cuyo módulo ya no existe en el producto.
    /// </summary>
    /// <remarks>
    /// <b>Desinstalado, no desactivado.</b> La distinción importa: desactivar es
    /// reversible y ocurre a diario en una demostración, así que marcar
    /// huérfanos al desactivar llenaría el panel de avisos falsos que habría que
    /// deshacer al reactivar. Un módulo ausente del catálogo que declara el
    /// código sí deja sus archivos sin dueño para siempre.
    ///
    /// Por eso se compara contra los módulos <i>declarados</i>, sin mirar
    /// activaciones, y por eso vive aquí y no en el endpoint de activación.
    ///
    /// Los archivos huérfanos no se borran (SPEC §8.13): se listan para que
    /// alguien decida.
    /// </remarks>
    private async Task MarkOrphanMediaAsync(IReadOnlyList<IModule> declared, CancellationToken cancellationToken)
    {
        var knownCodes = declared.Select(module => module.Code).ToArray();

        // Los que perdieron su módulo. Un archivo sin dueño declarado —subido
        // por CORE mismo o por una versión anterior— no cuenta como huérfano.
        var orphaned = await database.MediaAssets
            .Where(asset => asset.OwnerModuleCode != null
                && !knownCodes.Contains(asset.OwnerModuleCode)
                && !asset.IsOrphan)
            .ExecuteUpdateAsync(update => update.SetProperty(asset => asset.IsOrphan, true), cancellationToken);

        // Y los que lo recuperaron: reinstalar un módulo devuelve sus archivos.
        var adopted = await database.MediaAssets
            .Where(asset => asset.OwnerModuleCode != null
                && knownCodes.Contains(asset.OwnerModuleCode)
                && asset.IsOrphan)
            .ExecuteUpdateAsync(update => update.SetProperty(asset => asset.IsOrphan, false), cancellationToken);

        if (orphaned > 0)
        {
            logger.LogWarning(
                "{Count} archivo(s) quedaron huérfanos: su módulo ya no existe en esta versión del producto. " +
                "No se borran; se listan en el panel para que alguien decida.",
                orphaned);
        }

        if (adopted > 0)
        {
            logger.LogInformation("{Count} archivo(s) recuperaron su módulo y dejan de estar huérfanos.", adopted);
        }
    }

    /// <summary>Da de alta o actualiza la ficha de cada módulo declarado.</summary>
    private void SynchronizeCatalog(IReadOnlyList<IModule> declared, Dictionary<string, Module> stored)
    {
        foreach (var module in declared)
        {
            var isCore = module.Code == ModuleGraph.CoreCode;

            if (stored.TryGetValue(module.Code, out var row))
            {
                row.DisplayName = module.DisplayName;
                row.Description = module.Description;
                row.Version = module.Version;
                row.IsCore = isCore;
                row.DisplayOrder = module.DisplayOrder;
                continue;
            }

            database.Modules.Add(new Module
            {
                Code = module.Code,
                DisplayName = module.DisplayName,
                Description = module.Description,
                Version = module.Version,
                IsCore = isCore,
                DisplayOrder = module.DisplayOrder
            });

            logger.LogInformation("Módulo '{Code}' añadido al catálogo de la instalación.", module.Code);
        }
    }

    /// <summary>
    /// Avisa de módulos que están en la base pero ya no en el código.
    /// </summary>
    /// <remarks>
    /// No se borran: eliminarlos arrastraría en cascada su activación y su
    /// historial, que es información del negocio. Casi siempre significa que se
    /// desplegó una versión del producto anterior a la que creó esa fila.
    /// </remarks>
    private void WarnAboutUnknown(IReadOnlyList<IModule> declared, Dictionary<string, Module> stored)
    {
        var declaredCodes = declared.Select(module => module.Code).ToHashSet();

        foreach (var (code, row) in stored.Where(entry => !declaredCodes.Contains(entry.Key)))
        {
            logger.LogWarning(
                "El módulo '{Code}' figura en core.modules pero no existe en esta versión del producto. " +
                "Se conserva la fila y se ignora. Activación actual: {Estado}.",
                code,
                row.Activation?.IsActive == true ? "activo" : "inactivo");
        }
    }

    /// <summary>Deja el grafo de la base igual al declarado en el código.</summary>
    private async Task SynchronizeDependenciesAsync(
        IReadOnlyList<IModule> declared,
        Dictionary<string, Module> byCode,
        CancellationToken cancellationToken)
    {
        var expected = new Dictionary<(int Module, int DependsOn), string>();

        foreach (var module in declared)
        {
            foreach (var required in module.HardDependencies)
            {
                expected[(byCode[module.Code].ModuleId, byCode[required].ModuleId)] = ModuleDependencyKind.Hard;
            }

            foreach (var optional in module.SoftDependencies)
            {
                // Una blanda hacia un módulo que no existe en esta versión ya se
                // avisó al validar el grafo; aquí sencillamente no se proyecta.
                if (!byCode.TryGetValue(optional, out var target))
                {
                    continue;
                }

                var key = (byCode[module.Code].ModuleId, target.ModuleId);
                if (!expected.ContainsKey(key))
                {
                    expected[key] = ModuleDependencyKind.Soft;
                }
            }
        }

        var current = await database.ModuleDependencies.ToListAsync(cancellationToken);

        foreach (var edge in current)
        {
            var key = (edge.ModuleId, edge.DependsOnModuleId);

            if (!expected.TryGetValue(key, out var kind))
            {
                database.ModuleDependencies.Remove(edge);
                continue;
            }

            edge.Kind = kind;
            expected.Remove(key);
        }

        foreach (var ((moduleId, dependsOnId), kind) in expected)
        {
            database.ModuleDependencies.Add(new ModuleDependency
            {
                ModuleId = moduleId,
                DependsOnModuleId = dependsOnId,
                Kind = kind
            });
        }
    }

    /// <summary>
    /// Garantiza una fila de activación por módulo. Los módulos nuevos nacen
    /// inactivos; CORE nace y permanece activo.
    /// </summary>
    private async Task EnsureActivationsAsync(
        IReadOnlyList<IModule> declared,
        Dictionary<string, Module> byCode,
        CancellationToken cancellationToken)
    {
        var activations = await database.ModuleActivations.ToDictionaryAsync(
            activation => activation.ModuleId,
            cancellationToken);

        foreach (var module in declared)
        {
            var row = byCode[module.Code];
            var isCore = module.Code == ModuleGraph.CoreCode;

            if (!activations.TryGetValue(row.ModuleId, out var activation))
            {
                database.ModuleActivations.Add(new ModuleActivation
                {
                    ModuleId = row.ModuleId,
                    IsActive = isCore,
                    ActivatedAt = isCore ? DateTimeOffset.UtcNow : null,
                    Notes = isCore ? null : "Pendiente de licenciar."
                });

                logger.LogInformation(
                    "Creada la activación de '{Code}' en estado {Estado}.",
                    module.Code,
                    isCore ? "activo" : "inactivo");
                continue;
            }

            // CORE no se desactiva: si alguien tocó la fila a mano, se corrige.
            if (isCore && !activation.IsActive)
            {
                activation.IsActive = true;
                activation.ActivatedAt = DateTimeOffset.UtcNow;
                activation.DeactivatedAt = null;
                logger.LogWarning("La activación de CORE estaba desactivada. Se ha restablecido: CORE no se desactiva.");
            }
        }
    }

    /// <summary>Lee los módulos activos, en orden de presentación.</summary>
    /// <remarks>
    /// El nombre y la versión salen del código, no de la fila: la tabla es una
    /// proyección y, si alguien la editó, la verdad sigue estando en el módulo.
    /// </remarks>
    private async Task<IReadOnlyList<ActiveModule>> ReadActiveAsync(
        IReadOnlyList<IModule> declared,
        CancellationToken cancellationToken)
    {
        var rows = await database.Modules
            .Where(module => module.Activation != null && module.Activation.IsActive)
            .OrderBy(module => module.DisplayOrder)
            .ThenBy(module => module.Code)
            .Select(module => new { module.Code, module.Activation!.ExpiresAt })
            .ToListAsync(cancellationToken);

        var byCode = declared.ToDictionary(module => module.Code);
        var active = new List<ActiveModule>(rows.Count);

        foreach (var row in rows)
        {
            // Activo en la base pero inexistente en el código: ya se avisó.
            if (!byCode.TryGetValue(row.Code, out var module))
            {
                continue;
            }

            if (row.ExpiresAt is { } expiry && expiry < DateTimeOffset.UtcNow)
            {
                logger.LogWarning(
                    "La activación del módulo '{Code}' venció el {Fecha:yyyy-MM-dd} y sigue activo. " +
                    "El control de vencimientos llega en la fase de comercialización (ADR-004).",
                    row.Code,
                    expiry);
            }

            active.Add(new ActiveModule(module.Code, module.DisplayName, module.Version));
        }

        return active;
    }
}
