using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Assistant.Data.Entities;

namespace Sillar.Modules.Assistant.Data;

/// <summary>
/// Modelo físico PREPARATORIO de M12: sin registro en API, proveedor de conexión,
/// migraciones ni escritura en base real. El artículo y sus revisiones son privados.
/// </summary>
public sealed class AssistantDbContext(DbContextOptions<AssistantDbContext> options) : DbContext(options)
{
    public const string Schema = "assistant";
    public const string MigrationsHistoryTable = "__migrations";

    public DbSet<EditorialArticleRow> Articles => Set<EditorialArticleRow>();
    public DbSet<EditorialRevisionRow> Revisions => Set<EditorialRevisionRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssistantDbContext).Assembly);
    }
}
