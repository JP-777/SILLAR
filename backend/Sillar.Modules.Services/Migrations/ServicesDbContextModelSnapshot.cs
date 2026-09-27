using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Sillar.Modules.Services.Data;
namespace Sillar.Modules.Services.Migrations;
[DbContext(typeof(ServicesDbContext))]
public sealed class ServicesDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("services");
        modelBuilder.Entity("Sillar.Modules.Services.Domain.ServiceEntry", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnName("id");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamptz").HasColumnName("created_at").HasDefaultValueSql("now()");
            b.Property<string>("Description").HasColumnName("description"); b.Property<int>("DisplayOrder").HasColumnName("display_order").HasDefaultValue(0);
            b.Property<Guid?>("ImageId").HasColumnName("image_id"); b.Property<string>("ImageAltText").HasColumnName("image_alt_text");
            b.Property<string>("Name").IsRequired().HasColumnName("name"); b.Property<decimal?>("Price").HasColumnType("numeric(12,2)").HasColumnName("price");
            b.Property<string>("PublicationState").IsRequired().HasColumnName("publication_state"); b.Property<string>("SaleUnit").HasColumnName("sale_unit");
            b.Property<string>("ShortDescription").HasColumnName("short_description"); b.Property<string>("Slug").IsRequired().HasColumnName("slug");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamptz").HasColumnName("updated_at").HasDefaultValueSql("now()");
            b.HasKey("Id").HasName("pk_service_entries"); b.HasIndex("Slug").IsUnique().HasDatabaseName("uq_service_entries_slug");
            b.ToTable("service_entries", "services");
        });
    }
}
