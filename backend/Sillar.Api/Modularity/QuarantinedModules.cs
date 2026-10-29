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

/// <summary>
/// Cuarentenas compiladas del Host. No existe configuración que pueda retirarlas.
/// </summary>
internal static class QuarantinedModules
{
    internal const string AssistantCode = "assistant";

    private static readonly IReadOnlySet<string> Codes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AssistantCode
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
        return EnsureSafeAsync(token => ObserveAsync(database, token), cancellationToken);
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

        var reasons = new List<string>();

        if (!string.IsNullOrWhiteSpace(observation.IndeterminateReason))
        {
            reasons.Add($"estado indeterminado: {observation.IndeterminateReason}");
        }

        if (observation.HasAssistantModule)
        {
            reasons.Add("existe el registro core.modules");
        }

        if (observation.HasAssistantActivation)
        {
            reasons.Add(observation.IsAssistantActive
                ? "existe una activación activa"
                : "existe una activación inactiva o histórica");
        }

        if (observation.HasAssistantMedia)
        {
            reasons.Add("existen medios cuyo owner_module_code es 'assistant'");
        }

        if (observation.HasAssistantSchema)
        {
            reasons.Add("existe el schema 'assistant'");
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
            "El módulo 'assistant' está en cuarentena compilada y la instalación conserva huellas que "
            + $"impiden continuar: {string.Join("; ", reasons)}. "
            + "No se eliminaron filas, no se cambió la activación y no se marcaron medios como huérfanos. "
            + "Se requiere conciliación expresamente autorizada por JP.");
    }

    /// <summary>
    /// Observa catálogos de PostgreSQL y después, solo si CORE está completo,
    /// consulta sus tablas. Todas las sentencias son SELECT.
    /// </summary>
    private static async Task<AssistantQuarantineObservation> ObserveAsync(
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
            bool assistantSchema;
            bool anyUserRelation;

            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT
                        to_regclass('core.modules') IS NOT NULL,
                        to_regclass('core.module_activations') IS NOT NULL,
                        to_regclass('core.media_assets') IS NOT NULL,
                        EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'assistant'),
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
                assistantSchema = reader.GetBoolean(3);
                anyUserRelation = reader.GetBoolean(4);
            }

            var completeCoreCatalog = modulesTable && activationsTable && mediaTable;
            var noCoreCatalog = !modulesTable && !activationsTable && !mediaTable;

            if (noCoreCatalog)
            {
                return new AssistantQuarantineObservation(
                    IsVerifiedNewDatabase: !anyUserRelation && !assistantSchema,
                    HasCompleteCoreCatalog: false,
                    HasAssistantModule: false,
                    HasAssistantActivation: false,
                    IsAssistantActive: false,
                    HasAssistantMedia: false,
                    HasAssistantSchema: assistantSchema,
                    IndeterminateReason: anyUserRelation
                        ? "hay estructuras de usuario pero no existe el catálogo CORE"
                        : null);
            }

            if (!completeCoreCatalog)
            {
                return new AssistantQuarantineObservation(
                    IsVerifiedNewDatabase: false,
                    HasCompleteCoreCatalog: false,
                    HasAssistantModule: false,
                    HasAssistantActivation: false,
                    IsAssistantActive: false,
                    HasAssistantMedia: false,
                    HasAssistantSchema: assistantSchema,
                    IndeterminateReason: "las tablas de catálogo, activación y medios de CORE están incompletas");
            }

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
            code.Value = AssistantCode;
            footprint.Parameters.Add(code);

            await using var footprintReader = await footprint.ExecuteReaderAsync(cancellationToken);
            if (!await footprintReader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("PostgreSQL no devolvió el diagnóstico de Assistant.");
            }

            return new AssistantQuarantineObservation(
                IsVerifiedNewDatabase: false,
                HasCompleteCoreCatalog: true,
                HasAssistantModule: footprintReader.GetBoolean(0),
                HasAssistantActivation: footprintReader.GetBoolean(1),
                IsAssistantActive: footprintReader.GetBoolean(2),
                HasAssistantMedia: footprintReader.GetBoolean(3),
                HasAssistantSchema: assistantSchema);
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
