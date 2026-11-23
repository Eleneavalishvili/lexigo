using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlacementSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearningSessions_UserId_CourseId",
                table: "LearningSessions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PlacementAppliedAt",
                table: "LearningSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlacementMap",
                table: "LearningSessions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecommendedUnitId",
                table: "LearningSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Units_CourseId_Id",
                table: "Units",
                columns: new[] { "CourseId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_CourseId_RecommendedUnitId",
                table: "LearningSessions",
                columns: new[] { "CourseId", "RecommendedUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_UserId_CourseId",
                table: "LearningSessions",
                columns: new[] { "UserId", "CourseId" },
                unique: true,
                filter: "\"PlacementMap\" IS NOT NULL AND \"CompletedAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LearningSessions_Placement",
                table: "LearningSessions",
                sql: "(\"PlacementMap\" IS NULL AND \"RecommendedUnitId\" IS NULL AND \"PlacementAppliedAt\" IS NULL) OR (\"PlacementMap\" IS NOT NULL AND \"Mode\" = 'Practice' AND \"LessonId\" IS NULL AND \"FirstCompletionXp\" = 0 AND \"AwardedXp\" = 0 AND ((\"PlacementAppliedAt\" IS NULL AND \"RecommendedUnitId\" IS NULL) OR (\"PlacementAppliedAt\" IS NOT NULL AND \"RecommendedUnitId\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL)))");

            migrationBuilder.AddForeignKey(
                name: "FK_LearningSessions_Units_CourseId_RecommendedUnitId",
                table: "LearningSessions",
                columns: new[] { "CourseId", "RecommendedUnitId" },
                principalTable: "Units",
                principalColumns: new[] { "CourseId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"LearningSessions\" WHERE \"PlacementMap\" IS NOT NULL) THEN RAISE EXCEPTION 'Placement history exists; export and explicitly plan a data-preserving rollback first.'; END IF; END $$;");

            migrationBuilder.DropForeignKey(
                name: "FK_LearningSessions_Units_CourseId_RecommendedUnitId",
                table: "LearningSessions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Units_CourseId_Id",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_LearningSessions_CourseId_RecommendedUnitId",
                table: "LearningSessions");

            migrationBuilder.DropIndex(
                name: "IX_LearningSessions_UserId_CourseId",
                table: "LearningSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LearningSessions_Placement",
                table: "LearningSessions");

            migrationBuilder.DropColumn(
                name: "PlacementAppliedAt",
                table: "LearningSessions");

            migrationBuilder.DropColumn(
                name: "PlacementMap",
                table: "LearningSessions");

            migrationBuilder.DropColumn(
                name: "RecommendedUnitId",
                table: "LearningSessions");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_UserId_CourseId",
                table: "LearningSessions",
                columns: new[] { "UserId", "CourseId" });
        }
    }
}
