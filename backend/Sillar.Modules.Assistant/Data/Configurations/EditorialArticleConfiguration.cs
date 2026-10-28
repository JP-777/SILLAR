using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Assistant.Data.Entities;
using Sillar.Modules.Assistant.Data.Repositories;

namespace Sillar.Modules.Assistant.Data.Configurations;

internal sealed class EditorialArticleConfiguration : IEntityTypeConfiguration<EditorialArticleRow>
{
    public void Configure(EntityTypeBuilder<EditorialArticleRow> builder)
    {
        builder.ToTable("knowledge_articles", table =>
        {
            table.HasCheckConstraint("ck_assistant_article_version", "version >= 0");
            table.HasCheckConstraint("ck_assistant_writer_no_vacio", "btrim(writer_node) <> ''");
            table.HasCheckConstraint("ck_assistant_instalacion_no_vacia", "installation_id <> '00000000-0000-0000-0000-000000000000'::uuid");
        });
        builder.HasKey(x => x.Id).HasName(EditorialDatabaseConflict.ArticlePrimaryKeyConstraintName);
        builder.HasAlternateKey(x => new { x.InstallationId, x.Id });
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.InstallationId).HasColumnName("installation_id").IsRequired();
        builder.Property(x => x.WriterNode).HasColumnName("writer_node").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.Property(x => x.CurrentDraftRevisionId).HasColumnName("draft_revision_id");
        builder.Property(x => x.CurrentPublishedRevisionId).HasColumnName("published_revision_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        builder.HasIndex(x => new { x.InstallationId, x.WriterNode });
    }
}
