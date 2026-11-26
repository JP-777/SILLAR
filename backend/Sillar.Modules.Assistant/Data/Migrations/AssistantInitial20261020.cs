using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sillar.Modules.Assistant.Data;

namespace Sillar.Modules.Assistant.Data.Migrations;

/// <summary>Primera migración física, exclusiva de assistant; sin referencias CORE.</summary>
[DbContext(typeof(AssistantDbContext))]
[Migration("20261020120000_AssistantInitial")]
public sealed class AssistantInitial20261020 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: AssistantDbContext.Schema);

        migrationBuilder.CreateTable(
            name: "knowledge_articles",
            schema: AssistantDbContext.Schema,
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                writer_node = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                draft_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                published_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_assistant_knowledge_articles", x => x.id);
                table.UniqueConstraint("ak_knowledge_articles_installation_id_id", x => new { x.installation_id, x.id });
                table.CheckConstraint("ck_assistant_article_version", "version >= 0");
                table.CheckConstraint("ck_assistant_writer_no_vacio", "btrim(writer_node) <> ''");
                table.CheckConstraint("ck_assistant_instalacion_no_vacia", "installation_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            });

        migrationBuilder.CreateTable(
            name: "knowledge_revisions",
            schema: AssistantDbContext.Schema,
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                article_id = table.Column<Guid>(type: "uuid", nullable: false),
                question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                question_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, collation: "C"),
                answer = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                public_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                published_article_version = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_assistant_knowledge_revisions", x => x.id);
                table.CheckConstraint("ck_assistant_revision_question", "btrim(question) <> ''");
                table.CheckConstraint("ck_assistant_revision_answer", "btrim(answer) <> ''");
                table.CheckConstraint("ck_assistant_revision_key", "btrim(question_key) <> ''");
                table.CheckConstraint("ck_assistant_revision_title", "btrim(title) <> ''");
                table.CheckConstraint("ck_assistant_publicacion_par", "(published_at IS NULL AND published_article_version IS NULL) OR (published_at IS NOT NULL AND published_article_version > 0)");
                table.ForeignKey(
                    name: "fk_assistant_revisions_articles_scope",
                    columns: x => new { x.installation_id, x.article_id },
                    principalSchema: AssistantDbContext.Schema,
                    principalTable: "knowledge_articles",
                    principalColumns: new[] { "installation_id", "id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_assistant_article_installation_writer",
            schema: AssistantDbContext.Schema,
            table: "knowledge_articles",
            columns: new[] { "installation_id", "writer_node" });
        migrationBuilder.CreateIndex(
            name: "ix_assistant_revision_installation_article",
            schema: AssistantDbContext.Schema,
            table: "knowledge_revisions",
            columns: new[] { "installation_id", "article_id" });
        migrationBuilder.CreateIndex(
            name: "idx_assistant_faq_question_key",
            schema: AssistantDbContext.Schema,
            table: "knowledge_revisions",
            columns: new[] { "installation_id", "question_key" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "knowledge_revisions", schema: AssistantDbContext.Schema);
        migrationBuilder.DropTable(name: "knowledge_articles", schema: AssistantDbContext.Schema);
        // El esquema se conserva deliberadamente; jamás se borra un esquema ajeno.
    }
}
