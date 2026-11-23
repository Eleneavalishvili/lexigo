using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSessionAudio : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AudioMediaId",
            table: "SessionExercises",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SessionExercises_AudioMediaId",
            table: "SessionExercises",
            column: "AudioMediaId");

        migrationBuilder.AddForeignKey(
            name: "FK_SessionExercises_MediaAssets_AudioMediaId",
            table: "SessionExercises",
            column: "AudioMediaId",
            principalTable: "MediaAssets",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SessionExercises_MediaAssets_AudioMediaId",
            table: "SessionExercises");

        migrationBuilder.DropIndex(
            name: "IX_SessionExercises_AudioMediaId",
            table: "SessionExercises");

        migrationBuilder.DropColumn(
            name: "AudioMediaId",
            table: "SessionExercises");
    }
}
