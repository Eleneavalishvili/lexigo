using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CloseExtraStudyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ExtraStudyRequests_Status",
                table: "ExtraStudyRequests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExtraStudyRequests_Status",
                table: "ExtraStudyRequests",
                sql: " \"Status\" IN ('Pending', 'Accepted', 'Declined', 'Closed') ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ExtraStudyRequests_Status",
                table: "ExtraStudyRequests");

            migrationBuilder.Sql("""
                UPDATE "ExtraStudyRequests" SET "Status" = 'Declined' WHERE "Status" = 'Closed';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExtraStudyRequests_Status",
                table: "ExtraStudyRequests",
                sql: " \"Status\" IN ('Pending', 'Accepted', 'Declined') ");
        }
    }
}
