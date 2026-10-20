using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Modules.Assistant.Data;

namespace Sillar.Modules.Assistant.Data.Repositories;

/// <summary>
/// Clasifica exclusivamente las colisiones físicas de las claves primarias
/// editoriales M12. No transforma otros errores SQL, de conexión o integridad.
/// </summary>
public static class EditorialDatabaseConflict
{
    public const string ArticlePrimaryKeyConstraintName = "pk_assistant_knowledge_articles";
    public const string RevisionPrimaryKeyConstraintName = "pk_assistant_knowledge_revisions";

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

    /// <summary>
    /// Aísla exclusivamente la violación 23505 de la PK de revisiones del schema assistant.
    /// No presupone la causa cuando PostgreSQL omite tabla, schema o restricción.
    /// </summary>
    public static bool IsRevisionPrimaryKeyCollision(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            SchemaName: AssistantDbContext.Schema,
            TableName: "knowledge_revisions",
            ConstraintName: RevisionPrimaryKeyConstraintName
        };
    }
}
