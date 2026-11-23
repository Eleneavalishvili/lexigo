using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSharedHeartRecovery : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LearnerHearts",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Count = table.Column<int>(type: "integer", nullable: false),
                RefillAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LearnerHearts", x => x.UserId);
                table.CheckConstraint("CK_LearnerHearts_Balance", "\"Count\" BETWEEN 0 AND 5 AND ((\"Count\" = 0 AND \"RefillAt\" IS NOT NULL) OR (\"Count\" > 0 AND \"RefillAt\" IS NULL))");
                table.ForeignKey(
                    name: "FK_LearnerHearts_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.Sql("""
            INSERT INTO "LearnerHearts" ("UserId", "Count", "RefillAt")
            SELECT u."Id", COALESCE(s.hearts, 5),
                CASE WHEN s.hearts = 0 THEN CURRENT_TIMESTAMP + INTERVAL '1 hour' ELSE NULL END
            FROM "Users" u
            LEFT JOIN (
                SELECT "UserId", MIN("Hearts") AS hearts FROM "LearningSessions"
                WHERE "CompletedAt" IS NULL AND "Mode" = 'Lesson' GROUP BY "UserId"
            ) s ON s."UserId" = u."Id";
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LearnerHearts");
    }
}
