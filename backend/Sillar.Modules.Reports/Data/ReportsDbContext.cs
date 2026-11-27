using Microsoft.EntityFrameworkCore;

namespace Sillar.Modules.Reports.Data;

/// <summary>
/// Historial local de instalación. Reports no posee ni replica datos de negocio:
/// los agrega por contratos de sus dueños, sin duplicar pedidos ni cobros.
/// </summary>
public sealed class ReportsDbContext(DbContextOptions<ReportsDbContext> options) : DbContext(options)
{
    public const string Schema = "reporting";
    public const string MigrationsHistoryTable = "__migrations";
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.HasDefaultSchema(Schema);
}
