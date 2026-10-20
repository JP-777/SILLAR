using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Assistant.Data.Entities;

namespace Sillar.Modules.Assistant.Data.Configurations;

internal sealed class EditorialRevisionConfiguration : IEntityTypeConfiguration<EditorialRevisionRow>
{
    public void Configure(EntityTypeBuilder<EditorialRevisionRow> builder)
    {
        builder.ToTable("knowledge_revisions", table =>
        {
            table.HasCheckConstraint("ck_assistant_revision_question", "btrim(question) <> ''");
            table.HasCheckConstraint("ck_assistant_revision_answer", "btrim(answer) <> ''");
            table.HasCheckConstraint("ck_assistant_revision_title", "btrim(title) <> ''");
            table.HasCheckConstraint("ck_assistant_publicacion_par",
                "(published_at IS NULL AND published_article_version IS NULL) OR " +
                "(published_at IS NOT NULL AND published_article_version > 0)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.InstallationId).HasColumnName("installation_id").IsRequired();
        builder.Property(x => x.ArticleId).HasColumnName("article_id").IsRequired();
        builder.Property(x => x.Question).HasColumnName("question").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Answer).HasColumnName("answer").HasMaxLength(10000).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.PublicUrl).HasColumnName("public_url").HasMaxLength(2048).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        builder.Property(x => x.PublishedAt).HasColumnName("published_at").HasColumnType("timestamptz");
        builder.Property(x => x.PublishedArticleVersion).HasColumnName("published_article_version");
        builder.HasOne<EditorialArticleRow>().WithMany()
            .HasForeignKey(x => new { x.InstallationId, x.ArticleId })
            .HasPrincipalKey(x => new { x.InstallationId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InstallationId, x.ArticleId });
    }
}
