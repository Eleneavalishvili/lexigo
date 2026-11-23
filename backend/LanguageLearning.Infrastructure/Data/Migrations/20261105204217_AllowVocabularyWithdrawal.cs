using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowVocabularyWithdrawal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION lexigo_guard_deleted_account() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE owner_status text;
                BEGIN
                    IF TG_OP = 'UPDATE' AND (to_jsonb(NEW)->>TG_ARGV[0])
                        IS NOT DISTINCT FROM (to_jsonb(OLD)->>TG_ARGV[0]) THEN
                        RETURN NEW;
                    END IF;
                    SELECT "Status" INTO owner_status FROM "Users"
                    WHERE "Id" = (to_jsonb(NEW)->>TG_ARGV[0])::uuid FOR SHARE;
                    IF owner_status = 'Deleted' THEN
                        RAISE EXCEPTION 'Deleted account cannot receive private data' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $body$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_VocabularyRevisions_WordId",
                table: "VocabularyRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VocabularyRevision_Workflow",
                table: "VocabularyRevisions");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyRevisions_WordId",
                table: "VocabularyRevisions",
                column: "WordId",
                unique: true,
                filter: "\"Status\" NOT IN ('Published', 'Withdrawn')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VocabularyRevision_Workflow",
                table: "VocabularyRevisions",
                sql: "\"Status\" IN ('Draft', 'Submitted', 'Approved', 'Published', 'Withdrawn')\nAND (\"ReviewerId\" IS NULL OR \"ReviewerId\" <> \"AuthorId\")\nAND (\"Status\" NOT IN ('Approved', 'Published') OR (\"ReviewerId\" IS NOT NULL AND \"ReviewedAt\" IS NOT NULL))\nAND (\"Status\" <> 'Published' OR (\"PublisherId\" IS NOT NULL AND \"PublishedAt\" IS NOT NULL AND \"WordId\" IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "VocabularyRevisions" WHERE "Status" = 'Withdrawn') THEN
                        RAISE EXCEPTION 'Rollback requires a data migration for withdrawn revision history.';
                    END IF;
                END $body$;
                """);
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION lexigo_guard_deleted_account() RETURNS trigger LANGUAGE plpgsql AS $body$
                DECLARE owner_status text;
                BEGIN
                    IF TG_OP = 'UPDATE' AND TG_TABLE_NAME = 'EmailOutboxMessages' AND
                        to_jsonb(NEW)->>'Recipient' = '' AND to_jsonb(NEW)->>'HtmlBody' = '' AND
                        (to_jsonb(NEW)->>'Attempts')::int = 8 THEN RETURN NEW; END IF;
                    SELECT "Status" INTO owner_status FROM "Users"
                    WHERE "Id" = (to_jsonb(NEW)->>TG_ARGV[0])::uuid FOR SHARE;
                    IF owner_status = 'Deleted' THEN
                        RAISE EXCEPTION 'Deleted account cannot receive private data' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $body$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_VocabularyRevisions_WordId",
                table: "VocabularyRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VocabularyRevision_Workflow",
                table: "VocabularyRevisions");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyRevisions_WordId",
                table: "VocabularyRevisions",
                column: "WordId",
                unique: true,
                filter: "\"Status\" <> 'Published'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VocabularyRevision_Workflow",
                table: "VocabularyRevisions",
                sql: "\"Status\" IN ('Draft', 'Submitted', 'Approved', 'Published')\nAND (\"ReviewerId\" IS NULL OR \"ReviewerId\" <> \"AuthorId\")\nAND (\"Status\" NOT IN ('Approved', 'Published') OR (\"ReviewerId\" IS NOT NULL AND \"ReviewedAt\" IS NOT NULL))\nAND (\"Status\" <> 'Published' OR (\"PublisherId\" IS NOT NULL AND \"PublishedAt\" IS NOT NULL AND \"WordId\" IS NOT NULL))");
        }
    }
}
