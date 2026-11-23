using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndexCompletedDialogueHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DialogueSessions_UserId_CompletedAt",
                table: "DialogueSessions",
                columns: new[] { "UserId", "CompletedAt" },
                filter: "\"CompletedAt\" IS NOT NULL")
                .Annotation("Npgsql:IndexInclude", new[] { "DialogueId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DialogueSessions_UserId_CompletedAt",
                table: "DialogueSessions");
        }
    }
}
