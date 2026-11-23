using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSpeechCheckAttempts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SpeechCheckAttempts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                AudioSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                HeartsCharged = table.Column<int>(type: "integer", nullable: false),
                ResponseSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SpeechCheckAttempts", x => x.Id);
                table.CheckConstraint("CK_SpeechCheckAttempt_Values", "\"Status\" IN ('Matched','Mismatch','NoSpeech') AND \"HeartsCharged\" BETWEEN 0 AND 1 AND (\"HeartsCharged\" = 0 OR \"Status\" = 'Mismatch') AND length(\"AudioSha256\") = 64");
                table.ForeignKey(
                    name: "FK_SpeechCheckAttempts_SessionExercises_UserId_SessionExercise~",
                    columns: x => new { x.UserId, x.SessionExerciseId },
                    principalTable: "SessionExercises",
                    principalColumns: new[] { "UserId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SpeechCheckAttempts_UserId_CreatedAt",
            table: "SpeechCheckAttempts",
            columns: new[] { "UserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_SpeechCheckAttempts_UserId_IdempotencyKey",
            table: "SpeechCheckAttempts",
            columns: new[] { "UserId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SpeechCheckAttempts_UserId_SessionExerciseId_Status",
            table: "SpeechCheckAttempts",
            columns: new[] { "UserId", "SessionExerciseId", "Status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SpeechCheckAttempts");
    }
}
