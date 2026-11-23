using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddMatchingAudio : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AudioMediaId",
            table: "ExerciseOptions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "SessionOptionAudio",
            columns: table => new
            {
                SessionExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                AudioMediaId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SessionOptionAudio", x => new { x.SessionExerciseId, x.OptionId });
                table.ForeignKey(
                    name: "FK_SessionOptionAudio_MediaAssets_AudioMediaId",
                    column: x => x.AudioMediaId,
                    principalTable: "MediaAssets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SessionOptionAudio_SessionExercises_SessionExerciseId",
                    column: x => x.SessionExerciseId,
                    principalTable: "SessionExercises",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExerciseOptions_AudioMediaId",
            table: "ExerciseOptions",
            column: "AudioMediaId");

        migrationBuilder.CreateIndex(
            name: "IX_SessionOptionAudio_AudioMediaId",
            table: "SessionOptionAudio",
            column: "AudioMediaId");

        migrationBuilder.AddForeignKey(
            name: "FK_ExerciseOptions_MediaAssets_AudioMediaId",
            table: "ExerciseOptions",
            column: "AudioMediaId",
            principalTable: "MediaAssets",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ExerciseOptions_MediaAssets_AudioMediaId",
            table: "ExerciseOptions");

        migrationBuilder.DropTable(
            name: "SessionOptionAudio");

        migrationBuilder.DropIndex(
            name: "IX_ExerciseOptions_AudioMediaId",
            table: "ExerciseOptions");

        migrationBuilder.DropColumn(
            name: "AudioMediaId",
            table: "ExerciseOptions");
    }
}
