using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Data;

/// <summary>Contexto de datos de M02. Solo escribe en el schema <c>cms</c>.</summary>
public sealed class CmsDbContext : DbContext
{
    public const string Schema = "cms";
    public const string MigrationsHistoryTable = "__migrations";

    public CmsDbContext(DbContextOptions<CmsDbContext> options) : base(options)
    {
    }

    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<FeaturedProduct> FeaturedProducts => Set<FeaturedProduct>();
    public DbSet<FeaturedProject> FeaturedProjects => Set<FeaturedProject>();
    public DbSet<SocialLink> SocialLinks => Set<SocialLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CmsDbContext).Assembly);
    }
}
