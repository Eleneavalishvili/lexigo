using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndexWeeklyXp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_XpTransactions_CourseId_EarnedAt_UserId",
                table: "XpTransactions",
                columns: new[] { "CourseId", "EarnedAt", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_XpTransactions_CourseId_EarnedAt_UserId",
                table: "XpTransactions");
        }
    }
}
