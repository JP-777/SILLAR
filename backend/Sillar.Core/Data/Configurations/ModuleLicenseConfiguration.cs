using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;

namespace Sillar.Core.Data.Configurations;
internal sealed class ModuleLicenseConfiguration : IEntityTypeConfiguration<ModuleLicense>
{
    public void Configure(EntityTypeBuilder<ModuleLicense> b)
    {
        b.ToTable("module_licenses", t => t.HasCheckConstraint("ck_module_license_code", "module_code ~ '^[a-z][a-z0-9_]{0,39}$'"));
        b.HasKey(x => x.ModuleCode).HasName("pk_module_licenses");
        b.Property(x => x.ModuleCode).HasColumnName("module_code").HasMaxLength(40).UseCollation("C");
        b.Property(x => x.IsLicensed).HasColumnName("is_licensed");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
        b.Property(x => x.Source).HasColumnName("source").HasMaxLength(100);
    }
}
