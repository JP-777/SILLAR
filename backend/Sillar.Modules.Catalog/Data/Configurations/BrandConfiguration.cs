using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Catalog.Domain;
using Sillar.Shared.Data.Replication;

namespace Sillar.Modules.Catalog.Data.Configurations;

internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands", table =>
        {
            table.HasCheckConstraint("ck_brands_name_no_vacio", Check.NotEmpty("name"));
            table.HasCheckConstraint("ck_brands_slug_formato", Check.SlugFormat("slug"));
        });

        builder.MapCatalogKey("brands");
        builder.MapReplication();

        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").IsRequired();
        builder.Property(x => x.LogoId).HasColumnName("logo_id");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        // Ambos con colación es_ci: «Artesco» y «ARTESCO» son la misma marca,
        // «Peña» y «Pena» no.
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("uq_brands_name");
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_brands_slug");
    }
}
