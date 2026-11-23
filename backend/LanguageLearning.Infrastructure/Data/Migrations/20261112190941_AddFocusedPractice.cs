using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFocusedPractice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PracticeFocus",
                table: "LearningSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_UserId_CourseId_PracticeFocus",
                table: "LearningSessions",
                columns: new[] { "UserId", "CourseId", "PracticeFocus" },
                unique: true,
                filter: "\"PracticeFocus\" IS NOT NULL AND \"CompletedAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LearningSessions_PracticeFocus",
                table: "LearningSessions",
                sql: "\"PracticeFocus\" IS NULL OR (\"PracticeFocus\" IN ('repeated', 'due') AND \"Mode\" = 'Practice'\n    AND \"LessonId\" IS NULL AND \"PlacementMap\" IS NULL AND \"PracticeSkillId\" IS NULL\n    AND \"FirstCompletionXp\" = 0 AND \"AwardedXp\" = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearningSessions_UserId_CourseId_PracticeFocus",
                table: "LearningSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LearningSessions_PracticeFocus",
                table: "LearningSessions");

            migrationBuilder.DropColumn(
                name: "PracticeFocus",
                table: "LearningSessions");
        }
    }
}
