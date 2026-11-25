using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowRepeatedExtraStudyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExtraStudyRequests_OfferId_LearnerId",
                table: "ExtraStudyRequests");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraStudyRequests_OfferId_LearnerId",
                table: "ExtraStudyRequests",
                columns: new[] { "OfferId", "LearnerId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Accepted')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "ExtraStudyRequests"
                        GROUP BY "OfferId", "LearnerId" HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Preserve teacher request history: retain the active-request index when rolling back the application.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_ExtraStudyRequests_OfferId_LearnerId",
                table: "ExtraStudyRequests");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraStudyRequests_OfferId_LearnerId",
                table: "ExtraStudyRequests",
                columns: new[] { "OfferId", "LearnerId" },
                unique: true);
        }
    }
}
