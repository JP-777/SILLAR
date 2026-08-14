using Microsoft.EntityFrameworkCore;
using Sillar.Core.Domain;

namespace Sillar.Core.Data;

/// <summary>
/// Contexto de datos del módulo CORE. Solo escribe en el schema <c>core</c>.
/// </summary>
/// <remarks>
/// Ningún <c>DbContext</c> mapea tablas de otro schema (ADR-003 y ADR-009). Para
/// leer datos de otro módulo se pide su contrato al contenedor, nunca su tabla.
/// </remarks>
public class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    /// <summary>Schema propio del módulo.</summary>
    public const string Schema = "core";

    /// <summary>
    /// Tabla de historial de migraciones, dentro del schema del módulo: instalar
    /// el módulo es aplicar sus migraciones y desinstalarlo es soltar su schema,
    /// historial incluido.
    /// </summary>
    public const string MigrationsHistoryTable = "__migrations";

    /// <summary>
    /// Colación de identidad: ignora mayúsculas y <b>respeta tildes</b>.
    /// </summary>
    /// <remarks>
    /// Para lo que identifica y es único —el correo de acceso, la clave de una
    /// configuración—. Respetar la tilde importa: <c>josé@ejemplo.pe</c> y
    /// <c>jose@ejemplo.pe</c> son buzones distintos, y confundirlos permitiría
    /// que alguien bloquease el registro de otro.
    /// </remarks>
    public const string IdentityCollation = $"{Schema}.es_ci";

    /// <summary>
    /// Colación de búsqueda: ignora mayúsculas <b>y tildes</b>.
    /// </summary>
    /// <remarks>
    /// Para los campos por los que busca el usuario —nombre de producto, marca,
    /// palabras clave—, porque nadie escribe las tildes al buscar. Ningún campo
    /// de CORE la usa todavía: se crea aquí porque vive en el schema de CORE, del
    /// que todos dependen de forma dura, y la estrenará M01 Catálogo.
    ///
    /// Ni esta ni <see cref="IdentityCollation"/> igualan la ñ con la n: en
    /// español son letras distintas.
    /// </remarks>
    public const string SearchCollation = $"{Schema}.es_search";

    /// <summary>Identidad del negocio instalado. Una sola fila.</summary>
    public DbSet<Installation> Installations => Set<Installation>();

    /// <summary>Catálogo de módulos que el producto conoce.</summary>
    public DbSet<Module> Modules => Set<Module>();

    /// <summary>Grafo de dependencias entre módulos.</summary>
    public DbSet<ModuleDependency> ModuleDependencies => Set<ModuleDependency>();

    /// <summary>Módulos activos en esta instalación.</summary>
    public DbSet<ModuleActivation> ModuleActivations => Set<ModuleActivation>();

    /// <summary>Usuarios administradores.</summary>
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    /// <summary>Sesiones administrativas abiertas.</summary>
    public DbSet<AdminSession> AdminSessions => Set<AdminSession>();

    /// <summary>Configuración general del sitio.</summary>
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

    /// <summary>Fichas de los archivos subidos.</summary>
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    /// <summary>Registro de auditoría.</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Dos colaciones no deterministas para dos necesidades distintas
        // (SPEC §4.0). Viven en el schema de CORE porque las usarán varios
        // módulos y CORE es dependencia dura de todos.

        // level2: ignora mayúsculas, respeta tildes. 'JOSE@x.pe' = 'jose@x.pe',
        // pero 'José' ≠ 'Jose'.
        modelBuilder.HasCollation(
            schema: Schema,
            name: "es_ci",
            locale: "es-PE-u-ks-level2",
            provider: "icu",
            deterministic: false);

        // level1: ignora mayúsculas y tildes. Es la que hace que 'lapiz'
        // encuentre 'Lápiz técnico HB'.
        modelBuilder.HasCollation(
            schema: Schema,
            name: "es_search",
            locale: "es-PE-u-ks-level1",
            provider: "icu",
            deterministic: false);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);
    }
}
