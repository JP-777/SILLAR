using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Data.Configurations;

internal static class CmsConfiguration
{
    internal const string ValidPeriod = "starts_at IS NULL OR ends_at IS NULL OR ends_at > starts_at";
    internal const string ValidLink = "link_url IS NULL OR (link_label IS NOT NULL AND btrim(link_label) <> '')";
    internal const string ValidLinkUrl = "link_url IS NULL OR link_url COLLATE \"C\" ~ '^(/|https?://)'";
    internal const string BannerAltTextWithImage =
        "(image_desktop_id IS NULL AND image_mobile_id IS NULL) OR alt_text IS NOT NULL";
    internal const string AltTextWithImage = "image_id IS NULL OR alt_text IS NOT NULL";

    internal static string NotEmpty(string column) => $"btrim({column}) <> ''";
    internal static string OptionalNotEmpty(string column) => $"{column} IS NULL OR btrim({column}) <> ''";

    internal static void MapCommon<TEntity>(this EntityTypeBuilder<TEntity> builder, string table)
        where TEntity : CmsEntity
    {
        builder.HasKey(x => x.Id).HasName($"pk_{table}");
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
    }
}
