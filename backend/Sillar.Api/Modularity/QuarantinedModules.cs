using System.Data;
using Microsoft.EntityFrameworkCore;
using Sillar.Core.Data;
using Sillar.Shared.Modularity;

namespace Sillar.Api.Modularity;

/// <summary>Foto de solo lectura usada para decidir si la cuarentena es segura.</summary>
internal sealed record AssistantQuarantineObservation(
    bool IsVerifiedNewDatabase,
    bool HasCompleteCoreCatalog,
    bool HasAssistantModule,
    bool HasAssistantActivation,
    bool IsAssistantActive,
    bool HasAssistantMedia,
    bool HasAssistantSchema,
    string? IndeterminateReason = null);

/// <summary>Foto de solo lectura de un módulo excluido del catálogo operativo.</summary>
internal sealed record QuarantinedModuleObservation(
    string ModuleCode,
    bool IsVerifiedNewDatabase,
    bool HasCompleteCoreCatalog,
    bool HasModule,
    bool HasActivation,
    bool IsActive,
    bool HasMedia,
    bool HasSchema,
    string? IndeterminateReason = null);

/// <summary>
/// Cuarentenas compiladas del Host. No existe configuración que pueda retirarlas.
/// </summary>
internal static class QuarantinedModules
{
    internal const string AssistantCode = "assistant";
    internal const string PaymentsCode = "payments";

    private static readonly IReadOnlySet<string> Codes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AssistantCode,
            PaymentsCode
        };

    /// <summary>
    /// Conserva el orden de descubrimiento y retira únicamente módulos cuya
    /// cuarentena forma parte del binario.
    /// </summary>
    internal static IReadOnlyList<IModule> Admit(IReadOnlyList<IModule> discovered)
    {
        ArgumentNullException.ThrowIfNull(discovered);
        return [.. discovered.Where(module => !Codes.Contains(module.Code))];
    }

    /// <summary>
    /// Lee y valida antes de que el arranque pueda migrar o sincronizar.
    /// </summary>
    internal static Task EnsureSafeAsync(CoreDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        return EnsureAllSafeAsync(token => ObserveAllAsync(database, token), cancellationToken);
    }

    /// <summary>
    /// Costura determinista del flujo productivo generalizado. Cualquier fallo
    /// deja ambos módulos en DENY y nunca se interpreta como ausencia segura.
    /// </summary>
    internal static async Task EnsureAllSafeAsync(
        Func<CancellationToken, Task<IReadOnlyList<QuarantinedModuleObservation>>> read,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);

        IReadOnlyList<QuarantinedModuleObservation> observations;
        try
        {
            observations = await read(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StartupAbortedException(
                "No se pudo acreditar la ausencia de huellas persistidas de los módulos en cuarentena. "
                + $"El arranque se aborta sin modificar datos: {exception.GetType().Name}: {exception.Message}");
        }

        if (observations is null)
        {
            throw new StartupAbortedException(
                "No se pudo acreditar la ausencia de huellas persistidas de los módulos en cuarentena: "
                + "la observación no devolvió datos. El arranque se aborta sin modificar datos.");
        }

        var byCode = observations
            .Where(observation => observation is not null)
            .GroupBy(observation => observation.ModuleCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        foreach (var code in Codes.Order(StringComparer.Ordinal))
        {
            if (!byCode.TryGetValue(code, out var matches) || matches.Length != 1)
            {
                throw new StartupAbortedException(
                    $"No se pudo acreditar de forma inequívoca la ausencia de huellas del módulo '{code}'. "
                    + "El arranque se aborta sin modificar datos.");
            }

            EnsureQuarantinedObservationSafe(matches[0]);
        }
    }

    /// <summary>
    /// Costura determinista para comprobar el comportamiento fail-closed sin
    /// necesitar PostgreSQL en las pruebas focales.
    /// </summary>
    internal static async Task EnsureSafeAsync(
        Func<CancellationToken, Task<AssistantQuarantineObservation>> read,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);

        AssistantQuarantineObservation observation;
        try
        {
            observation = await read(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StartupAbortedException(
                "No se pudo acreditar la ausencia de huellas persistidas del módulo 'assistant'. "
                + $"El arranque se aborta sin modificar datos: {exception.GetType().Name}: {exception.Message}");
        }

        EnsureObservationSafe(observation);
    }

    /// <summary>Convierte toda huella o estado indeterminado en un aborto explícito.</summary>
    internal static void EnsureObservationSafe(AssistantQuarantineObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        EnsureQuarantinedObservationSafe(new QuarantinedModuleObservation(
            AssistantCode,
            observation.IsVerifiedNewDatabase,
            observation.HasCompleteCoreCatalog,
            observation.HasAssistantModule,
            observation.HasAssistantActivation,
            observation.IsAssistantActive,
            observation.HasAssistantMedia,
            observation.HasAssistantSchema,
            observation.IndeterminateReason));
    }

    /// <summary>Convierte toda huella o estado indeterminado en un aborto explícito.</summary>
    internal static void EnsureQuarantinedObservationSafe(QuarantinedModuleObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (!Codes.Contains(observation.ModuleCode))
        {
            throw new StartupAbortedException(
                $"La observación corresponde al módulo no reconocido '{observation.ModuleCode}'. "
                + "El arranque se aborta sin modificar datos.");
        }

        var reasons = new List<string>();

        if (!string.IsNullOrWhiteSpace(observation.IndeterminateReason))
        {
            reasons.Add($"estado indeterminado: {observation.IndeterminateReason}");
        }

        if (observation.HasModule)
        {
            reasons.Add("existe el registro core.modules");
        }

        if (observation.HasActivation)
        {
            reasons.Add(observation.IsActive
                ? "existe una activación activa"
                : "existe una activación inactiva o histórica");
        }

        if (observation.HasMedia)
        {
            reasons.Add($"existen medios cuyo owner_module_code es '{observation.ModuleCode}'");
        }

        if (observation.HasSchema)
        {
            reasons.Add($"existe el schema '{observation.ModuleCode}'");
        }

        if (!observation.IsVerifiedNewDatabase && !observation.HasCompleteCoreCatalog)
        {
            reasons.Add("la base no es nueva y el catálogo CORE no está completo");
        }

        if (reasons.Count == 0)
        {
            return;
        }

        throw new StartupAbortedException(
            $"El módulo '{observation.ModuleCode}' está en cuarentena compilada y la instalación conserva huellas que "
            + $"impiden continuar: {string.Join("; ", reasons)}. "
            + "No se eliminaron filas, no se cambió la activación y no se marcaron medios como huérfanos. "
            + "Se requiere conciliación expresamente autorizada por JP.");
    }

    /// <summary>
    /// Observa catálogos de PostgreSQL y después, solo si CORE está completo,
    /// consulta sus tablas. Todas las sentencias son SELECT.
    /// </summary>
    private static async Task<IReadOnlyList<QuarantinedModuleObservation>> ObserveAllAsync(
        CoreDbContext database,
        CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await database.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            bool modulesTable;
            bool activationsTable;
            bool mediaTable;
            bool anyUserRelation;

            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT
                        to_regclass('core.modules') IS NOT NULL,
                        to_regclass('core.module_activations') IS NOT NULL,
                        to_regclass('core.media_assets') IS NOT NULL,
                        EXISTS (
                            SELECT 1
                            FROM pg_class AS relation
                            JOIN pg_namespace AS schema ON schema.oid = relation.relnamespace
                            WHERE schema.nspname NOT IN ('pg_catalog', 'information_schema')
                              AND schema.nspname NOT LIKE 'pg_toast%'
                              AND relation.relkind IN ('r', 'p', 'v', 'm', 'S', 'f'))
                    """;

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException("PostgreSQL no devolvió el diagnóstico de estructuras.");
                }

                modulesTable = reader.GetBoolean(0);
                activationsTable = reader.GetBoolean(1);
                mediaTable = reader.GetBoolean(2);
                anyUserRelation = reader.GetBoolean(3);
            }

            var completeCoreCatalog = modulesTable && activationsTable && mediaTable;
            var noCoreCatalog = !modulesTable && !activationsTable && !mediaTable;

            var schemas = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var moduleCode in Codes.Order(StringComparer.Ordinal))
            {
                await using var schemaCommand = connection.CreateCommand();
                schemaCommand.CommandText =
                    "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = @module_code)";
                var schemaCode = schemaCommand.CreateParameter();
                schemaCode.ParameterName = "module_code";
                schemaCode.Value = moduleCode;
                schemaCommand.Parameters.Add(schemaCode);
                schemas[moduleCode] = Convert.ToBoolean(
                    await schemaCommand.ExecuteScalarAsync(cancellationToken));
            }

            if (noCoreCatalog)
            {
                return
                [
                    .. Codes.Order(StringComparer.Ordinal).Select(moduleCode =>
                        new QuarantinedModuleObservation(
                            moduleCode,
                            IsVerifiedNewDatabase: !anyUserRelation && !schemas[moduleCode],
                            HasCompleteCoreCatalog: false,
                            HasModule: false,
                            HasActivation: false,
                            IsActive: false,
                            HasMedia: false,
                            HasSchema: schemas[moduleCode],
                            IndeterminateReason: anyUserRelation
                                ? "hay estructuras de usuario pero no existe el catálogo CORE"
                                : null))
                ];
            }

            if (!completeCoreCatalog)
            {
                return
                [
                    .. Codes.Order(StringComparer.Ordinal).Select(moduleCode =>
                        new QuarantinedModuleObservation(
                            moduleCode,
                            IsVerifiedNewDatabase: false,
                            HasCompleteCoreCatalog: false,
                            HasModule: false,
                            HasActivation: false,
                            IsActive: false,
                            HasMedia: false,
                            HasSchema: schemas[moduleCode],
                            IndeterminateReason: "las tablas de catálogo, activación y medios de CORE están incompletas"))
                ];
            }

            var observations = new List<QuarantinedModuleObservation>(Codes.Count);
            foreach (var moduleCode in Codes.Order(StringComparer.Ordinal))
            {
                await using var footprint = connection.CreateCommand();
                footprint.CommandText =
                    """
                    SELECT
                        EXISTS (SELECT 1 FROM core.modules WHERE code = @module_code),
                        EXISTS (
                            SELECT 1
                            FROM core.module_activations AS activation
                            JOIN core.modules AS module ON module.module_id = activation.module_id
                            WHERE module.code = @module_code),
                        EXISTS (
                            SELECT 1
                            FROM core.module_activations AS activation
                            JOIN core.modules AS module ON module.module_id = activation.module_id
                            WHERE module.code = @module_code AND activation.is_active),
                        EXISTS (
                            SELECT 1 FROM core.media_assets
                            WHERE owner_module_code = @module_code)
                    """;

                var code = footprint.CreateParameter();
                code.ParameterName = "module_code";
                code.Value = moduleCode;
                footprint.Parameters.Add(code);

                await using var footprintReader = await footprint.ExecuteReaderAsync(cancellationToken);
                if (!await footprintReader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException(
                        $"PostgreSQL no devolvió el diagnóstico de '{moduleCode}'.");
                }

                observations.Add(new QuarantinedModuleObservation(
                    moduleCode,
                    IsVerifiedNewDatabase: false,
                    HasCompleteCoreCatalog: true,
                    HasModule: footprintReader.GetBoolean(0),
                    HasActivation: footprintReader.GetBoolean(1),
                    IsActive: footprintReader.GetBoolean(2),
                    HasMedia: footprintReader.GetBoolean(3),
                    HasSchema: schemas[moduleCode]));
            }

            return observations;
        }
        finally
        {
            if (openedHere)
            {
                await database.Database.CloseConnectionAsync();
            }
        }
    }
}
