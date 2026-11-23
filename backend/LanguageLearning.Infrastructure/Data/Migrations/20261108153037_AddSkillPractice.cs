using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillPractice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PracticeSkillId",
                table: "LearningSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Skills_CourseId_Id",
                table: "Skills",
                columns: new[] { "CourseId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_CourseId_PracticeSkillId",
                table: "LearningSessions",
                columns: new[] { "CourseId", "PracticeSkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_UserId_CourseId_PracticeSkillId",
                table: "LearningSessions",
                columns: new[] { "UserId", "CourseId", "PracticeSkillId" },
                unique: true,
                filter: "\"PracticeSkillId\" IS NOT NULL AND \"CompletedAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LearningSessions_PracticeSkill",
                table: "LearningSessions",
                sql: " \"PracticeSkillId\" IS NULL OR (\"Mode\" = 'Practice' AND \"LessonId\" IS NULL AND \"PlacementMap\" IS NULL AND \"FirstCompletionXp\" = 0 AND \"AwardedXp\" = 0) ");

            migrationBuilder.AddForeignKey(
                name: "FK_LearningSessions_Skills_CourseId_PracticeSkillId",
                table: "LearningSessions",
                columns: new[] { "CourseId", "PracticeSkillId" },
                principalTable: "Skills",
                principalColumns: new[] { "CourseId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "LearningSessions" WHERE "PracticeSkillId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Migrate skill practice sessions before rolling back';
                    END IF;
                END;
                $body$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_LearningSessions_Skills_CourseId_PracticeSkillId",
                table: "LearningSessions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Skills_CourseId_Id",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_LearningSessions_CourseId_PracticeSkillId",
                table: "LearningSessions");

            migrationBuilder.DropIndex(
                name: "IX_LearningSessions_UserId_CourseId_PracticeSkillId",
                table: "LearningSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LearningSessions_PracticeSkill",
                table: "LearningSessions");

            migrationBuilder.DropColumn(
                name: "PracticeSkillId",
                table: "LearningSessions");
        }
    }
}
