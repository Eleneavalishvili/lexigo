using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletionCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompletionCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VerificationCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CourseTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UnitTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LessonCount = table.Column<int>(type: "integer", nullable: false),
                    GradedLessonCount = table.Column<int>(type: "integer", nullable: false),
                    AverageBestScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    ShareCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompletionCards", x => x.Id);
                    table.CheckConstraint("CK_CompletionCards_Sharing", "\"VerificationCode\" ~ '^[0-9a-f]{32}$'\nAND ((NOT \"IsShared\" AND \"ShareCode\" IS NULL)\n    OR (\"IsShared\" AND \"ShareCode\" ~ '^[0-9a-f]{32}$' AND \"ShareCode\" IS NOT NULL))");
                    table.CheckConstraint("CK_CompletionCards_Values", "\"LessonCount\" > 0 AND \"GradedLessonCount\" BETWEEN 0 AND \"LessonCount\" AND \"Revision\" > 0\nAND \"CompletedAt\" <= \"IssuedAt\"\nAND ((\"GradedLessonCount\" = 0 AND \"AverageBestScore\" IS NULL)\n    OR (\"GradedLessonCount\" > 0 AND \"AverageBestScore\" IS NOT NULL AND \"AverageBestScore\" BETWEEN 0 AND 100))");
                    table.ForeignKey(
                        name: "FK_CompletionCards_Enrollments_UserId_CourseId",
                        columns: x => new { x.UserId, x.CourseId },
                        principalTable: "Enrollments",
                        principalColumns: new[] { "UserId", "CourseId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompletionCards_Units_CourseId_UnitId",
                        columns: x => new { x.CourseId, x.UnitId },
                        principalTable: "Units",
                        principalColumns: new[] { "CourseId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompletionCards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompletionCards_CourseId_UnitId",
                table: "CompletionCards",
                columns: new[] { "CourseId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompletionCards_ShareCode",
                table: "CompletionCards",
                column: "ShareCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompletionCards_UserId_CourseId",
                table: "CompletionCards",
                columns: new[] { "UserId", "CourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompletionCards_UserId_UnitId",
                table: "CompletionCards",
                columns: new[] { "UserId", "UnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompletionCards_VerificationCode",
                table: "CompletionCards",
                column: "VerificationCode",
                unique: true);
            migrationBuilder.Sql("""
                CREATE TRIGGER guard_deleted_account BEFORE INSERT OR UPDATE ON "CompletionCards"
                FOR EACH ROW EXECUTE FUNCTION lexigo_guard_deleted_account('UserId');

                CREATE FUNCTION lexigo_guard_completion_card() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    IF ROW(NEW."Id", NEW."UserId", NEW."CourseId", NEW."UnitId", NEW."IssuedAt", NEW."CompletedAt",
                        NEW."VerificationCode", NEW."CourseTitle", NEW."UnitTitle", NEW."LessonCount",
                        NEW."GradedLessonCount", NEW."AverageBestScore") IS DISTINCT FROM
                       ROW(OLD."Id", OLD."UserId", OLD."CourseId", OLD."UnitId", OLD."IssuedAt", OLD."CompletedAt",
                        OLD."VerificationCode", OLD."CourseTitle", OLD."UnitTitle", OLD."LessonCount",
                        OLD."GradedLessonCount", OLD."AverageBestScore") THEN
                        RAISE EXCEPTION 'Issued completion card results are immutable.' USING ERRCODE = '23514';
                    END IF;
                    IF ROW(NEW."IsShared", NEW."ShareCode") IS DISTINCT FROM ROW(OLD."IsShared", OLD."ShareCode") THEN
                        IF NEW."Revision" <> OLD."Revision" + 1 THEN
                            RAISE EXCEPTION 'Sharing changes require the next revision.' USING ERRCODE = '23514';
                        END IF;
                    ELSIF NEW."Revision" <> OLD."Revision" THEN
                        RAISE EXCEPTION 'Card revision requires a sharing change.' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $body$;
                CREATE TRIGGER guard_completion_card BEFORE UPDATE ON "CompletionCards"
                FOR EACH ROW EXECUTE FUNCTION lexigo_guard_completion_card();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$ BEGIN
                    IF EXISTS (SELECT 1 FROM "CompletionCards") THEN
                        RAISE EXCEPTION 'Completion cards must be explicitly migrated before rollback.';
                    END IF;
                END $body$;
                """);
            migrationBuilder.DropTable(
                name: "CompletionCards");
            migrationBuilder.Sql("DROP FUNCTION lexigo_guard_completion_card();");
        }
    }
}
