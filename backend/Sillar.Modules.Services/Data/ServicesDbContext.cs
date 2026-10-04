using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Services.Domain;

namespace Sillar.Modules.Services.Data;

public sealed class ServicesDbContext(DbContextOptions<ServicesDbContext> options) : DbContext(options)
{
    public const string Schema = "services";
    public const string MigrationsHistoryTable = "__migrations";
    public DbSet<ServiceEntry> Entries => Set<ServiceEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        var entry = modelBuilder.Entity<ServiceEntry>();
        entry.ToTable("service_entries", table =>
        {
            table.HasCheckConstraint("ck_service_entries_name_no_vacio", "btrim(name) <> ''");
            table.HasCheckConstraint("ck_service_entries_slug_formato", "slug COLLATE \"C\" ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'");
            table.HasCheckConstraint("ck_service_entries_price", "price IS NULL OR price >= 0");
            table.HasCheckConstraint("ck_service_entries_display_order", "display_order >= 0");
            table.HasCheckConstraint("ck_service_entries_description", "short_description IS NOT NULL OR description IS NOT NULL");
            table.HasCheckConstraint("ck_service_entries_image_alt", "image_id IS NULL OR image_alt_text IS NOT NULL");
            table.HasCheckConstraint("ck_service_entries_publication_state", "publication_state IN ('draft','published','archived')");
        });
        entry.HasKey(x => x.Id).HasName("pk_service_entries");
        entry.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        entry.Property(x => x.Name).HasColumnName("name").IsRequired();
        entry.Property(x => x.Slug).HasColumnName("slug").IsRequired();
        entry.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_service_entries_slug");
        entry.Property(x => x.ShortDescription).HasColumnName("short_description");
        entry.Property(x => x.Description).HasColumnName("description");
        entry.Property(x => x.Price).HasColumnName("price").HasColumnType("numeric(12,2)");
        entry.Property(x => x.SaleUnit).HasColumnName("sale_unit");
        entry.Property(x => x.ImageId).HasColumnName("image_id");
        entry.Property(x => x.ImageAltText).HasColumnName("image_alt_text");
        entry.Property(x => x.PublicationState).HasColumnName("publication_state")
            .HasConversion(value => value.ToString().ToLowerInvariant(), value => Enum.Parse<PublicationState>(value, true));
        entry.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        entry.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        entry.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
    }
}
