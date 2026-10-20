using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Assistant.Data.Entities;

namespace Sillar.Modules.Assistant.Data;

/// <summary>
/// Modelo físico M12 con migración propia de assistant. El host solo lo registra
/// al activar el módulo y disponer de identidad confiable. No aplicar a bases existentes aquí.
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
