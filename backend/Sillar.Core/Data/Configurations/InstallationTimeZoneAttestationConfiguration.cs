using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;

namespace Sillar.Core.Data.Configurations;

internal sealed class InstallationTimeZoneAttestationConfiguration : IEntityTypeConfiguration<InstallationTimeZoneAttestation>
{
    public void Configure(EntityTypeBuilder<InstallationTimeZoneAttestation> b)
    {
        b.ToTable("installation_time_zone_attestations", t =>
        {
            t.HasCheckConstraint("ck_time_zone_actor", "attested_by_admin_user_local_id > 0 AND btrim(attested_by_name) <> '' AND btrim(attested_by_admin_user_home_node) <> ''");
            t.HasCheckConstraint("ck_time_zone_source", "btrim(zone_id) <> '' AND btrim(source_name) <> '' AND btrim(source_version) <> '' AND btrim(authority_version) <> ''");
        });
        b.HasKey(x => x.Id).HasName("pk_installation_time_zone_attestations");
        b.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(x => x.AttestationId).HasColumnName("attestation_id");
        b.HasIndex(x => x.AttestationId).IsUnique().HasDatabaseName("uq_time_zone_attestation_id");
        b.Property(x => x.InstallationKey).HasColumnName("installation_key");
        b.Property(x => x.ZoneId).HasColumnName("zone_id").HasMaxLength(100);
        b.Property(x => x.SourceName).HasColumnName("source_name").HasMaxLength(100);
        b.Property(x => x.SourceVersion).HasColumnName("source_version").HasMaxLength(160);
        b.Property(x => x.AttestedByName).HasColumnName("attested_by_name").HasMaxLength(200);
        b.Property(x => x.AttestedByAdminUserLocalId).HasColumnName("attested_by_admin_user_local_id");
        b.Property(x => x.AttestedByAdminUserHomeNode).HasColumnName("attested_by_admin_user_home_node").HasMaxLength(80);
        b.Property(x => x.AttestedAt).HasColumnName("attested_at").HasColumnType("timestamptz");
        b.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamptz");
        b.Property(x => x.AuthorityVersion).HasColumnName("authority_version").HasMaxLength(50);
    }
}
