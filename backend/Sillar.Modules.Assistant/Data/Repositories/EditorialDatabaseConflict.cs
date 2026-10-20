using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Modules.Assistant.Data;

namespace Sillar.Modules.Assistant.Data.Repositories;

/// <summary>
/// Traduce solo la colisión física de la clave primaria de artículos M12.
/// No transforma otros errores SQL, de conexión ni de integridad en conflictos editoriales.
/// </summary>
public static class EditorialDatabaseConflict
{
    public const string ArticlePrimaryKeyConstraintName = "pk_assistant_knowledge_articles";

    public static bool IsArticlePrimaryKeyCollision(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            SchemaName: AssistantDbContext.Schema,
            TableName: "knowledge_articles",
            ConstraintName: ArticlePrimaryKeyConstraintName
        };
    }
}
