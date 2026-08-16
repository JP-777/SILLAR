using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Catalog.Domain;

namespace Sillar.Modules.Catalog.Data.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", table =>
        {
            table.HasCheckConstraint("ck_categories_name_no_vacio", Check.NotEmpty("name"));
            table.HasCheckConstraint("ck_categories_slug_formato", Check.SlugFormat("slug"));
            table.HasCheckConstraint("ck_categories_sort_order", "sort_order >= 0");
            // Solo el ciclo directo. El ciclo largo lo comprueba la aplicación:
            // recorrer el árbol desde un CHECK exige un disparador recursivo.
            table.HasCheckConstraint("ck_categories_parent_no_self", "parent_id IS NULL OR parent_id <> id");
        });

        builder.MapCatalogKey("categories");
        builder.MapReplication();

        builder.Property(x => x.ParentId).HasColumnName("parent_id");

        // La colación se aplica con SQL explícito en la migración: el proveedor
        // Npgsql entrecomilla el nombre calificado entero y PostgreSQL entonces
        // busca una colación llamada literalmente «core.es_search».
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.ImageId).HasColumnName("image_id");

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0)
            .ValueGeneratedNever();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_categories_slug");
        builder.HasIndex(x => x.ParentId).HasDatabaseName("idx_categories_parent");

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("idx_categories_activas")
            .HasFilter("is_active");

        // Restrict y no Cascade: borrar una categoría con hijas debe fallar y
        // que alguien decida, no llevarse el subárbol por delante.
        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .HasConstraintName("fk_categories_parent_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
