using System.Text;
using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Crm.Domain;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Crm.Data;

/// <summary>
/// Contexto de datos de M04 Clientes. Solo escribe en el schema <c>crm</c>.
/// </summary>
/// <remarks>
/// Las claves foráneas hacia <c>crm.customers</c> son internas del schema y
/// están permitidas. La colación <c>core.es_ci</c> se aplica con SQL explícito
/// en la migración, siguiendo el precedente de Catalog: Npgsql genera
/// <c>COLLATE "core.es_ci"</c> (entrecomillado) y PostgreSQL lo busca como
/// un identificador literal, que no existe.
/// </remarks>
public class CrmDbContext(
    DbContextOptions<CrmDbContext> options,
    NodeIdentity node,
    TimeProvider clock) : DbContext(options)
{
    /// <summary>Schema propio del módulo.</summary>
    public const string Schema = "crm";

    /// <summary>Historial de migraciones, dentro del schema del módulo.</summary>
    public const string MigrationsHistoryTable = "__migrations";

    /// <summary>Colación de identidad de CORE: ignora mayúsculas, respeta tildes.</summary>
    public const string IdentityCollation = "core.es_ci";

    /// <summary>Colación de búsqueda de CORE: ignora mayúsculas y tildes.</summary>
    public const string SearchCollation = "core.es_search";

    /// <summary>Fichas de cliente.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Direcciones de los clientes.</summary>
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    /// <summary>Cuentas de acceso de los clientes.</summary>
    public DbSet<CustomerAccount> CustomerAccounts => Set<CustomerAccount>();

    /// <summary>Sesiones activas de los clientes.</summary>
    public DbSet<CustomerSession> CustomerSessions => Set<CustomerSession>();

    /// <summary>Tokens de un solo uso.</summary>
    public DbSet<CustomerToken> CustomerTokens => Set<CustomerToken>();

    /// <summary>Mensajes del formulario de contacto.</summary>
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        StampReplicationColumns();
        NormalizeEmails();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampReplicationColumns();
        NormalizeEmails();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Rellena el nodo de origen y sube la marca de versión.
    /// </summary>
    /// <remarks>
    /// Tercera copia temporal del mismo patrón que ya tienen CORE y Catalog.
    /// DEUDA: StampReplicationColumns está duplicado por tercera vez.
    /// DISPARADOR PARA GENERALIZAR:
    /// - aparece una cuarta copia; o
    /// - dos implementaciones existentes empiezan a discrepar.
    /// </remarks>
    private void StampReplicationColumns()
    {
        var now = clock.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries<IReplicatedEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.OriginNode = node.Code;
                    entry.Entity.RowVersion = 1;
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    // El origen no se toca: quien edita no es quien creó.
                    entry.Property(nameof(IReplicatedEntity.OriginNode)).IsModified = false;
                    entry.Property(nameof(IReplicatedEntity.CreatedAt)).IsModified = false;
                    entry.Entity.RowVersion += 1;
                    break;
            }
        }
    }

    /// <summary>
    /// Normaliza el correo de <see cref="Customer"/> y <see cref="ContactMessage"/>
    /// antes de persistir: <c>Trim()</c> + <c>Normalize(NormalizationForm.FormC)</c>.
    /// </summary>
    /// <remarks>
    /// Se aplica tanto en Added como en Modified. No se convierte a
    /// minúsculas: la equivalencia de mayúsculas la resuelve
    /// <c>core.es_ci</c>. En <see cref="ContactMessage"/> el email es
    /// opcional: si es <c>null</c> no se toca.
    /// </remarks>
    private void NormalizeEmails()
    {
        foreach (var entry in ChangeTracker.Entries<Customer>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                var email = entry.Entity.Email;
                if (entry.Property(nameof(Customer.Email)).IsModified || entry.State == EntityState.Added)
                {
                    entry.Entity.Email = email.Trim().Normalize(NormalizationForm.FormC);
                }
            }
        }

        foreach (var entry in ChangeTracker.Entries<ContactMessage>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                var email = entry.Entity.Email;
                if (email is not null &&
                    (entry.Property(nameof(ContactMessage.Email)).IsModified || entry.State == EntityState.Added))
                {
                    entry.Entity.Email = email.Trim().Normalize(NormalizationForm.FormC);
                }
            }
        }
    }
}
