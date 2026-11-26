using Microsoft.EntityFrameworkCore;

namespace Sillar.Modules.Portal.Data;

/// <summary>
/// Historial local de instalación. Portal no posee ni replica datos de negocio:
/// los obtiene por contratos de sus dueños, sin duplicar identidad ni pedidos.
/// </summary>
public sealed class PortalDbContext(DbContextOptions<PortalDbContext> options) : DbContext(options)
{
    public const string Schema = "portal";
    public const string MigrationsHistoryTable = "__migrations";
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.HasDefaultSchema(Schema);
}
