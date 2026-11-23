using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddVocabularyLibrary : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VocabularyItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                Word = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                EnglishTranslation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Example = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                AudioMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                PartOfSpeech = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VocabularyItems", x => x.Id);
                table.CheckConstraint("CK_VocabularyItem_Text", "length(btrim(\"Word\")) > 0 AND length(btrim(\"EnglishTranslation\")) > 0");
                table.ForeignKey(
                    name: "FK_VocabularyItems_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VocabularyItems_MediaAssets_AudioMediaId",
                    column: x => x.AudioMediaId,
                    principalTable: "MediaAssets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SavedWords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                VocabularyItemId = table.Column<Guid>(type: "uuid", nullable: false),
                DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IntervalDays = table.Column<int>(type: "integer", nullable: false),
                CorrectStreak = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavedWords", x => x.Id);
                table.CheckConstraint("CK_SavedWord_Schedule", "\"CorrectStreak\" >= 0 AND \"IntervalDays\" IN (0,1,3,7,14,30)");
                table.ForeignKey(
                    name: "FK_SavedWords_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SavedWords_VocabularyItems_VocabularyItemId",
                    column: x => x.VocabularyItemId,
                    principalTable: "VocabularyItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SavedWords_UserId_DueAt_Id",
            table: "SavedWords",
            columns: new[] { "UserId", "DueAt", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_SavedWords_UserId_VocabularyItemId",
            table: "SavedWords",
            columns: new[] { "UserId", "VocabularyItemId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SavedWords_VocabularyItemId",
            table: "SavedWords",
            column: "VocabularyItemId");

        migrationBuilder.CreateIndex(
            name: "IX_VocabularyItems_AudioMediaId",
            table: "VocabularyItems",
            column: "AudioMediaId");

        migrationBuilder.CreateIndex(
            name: "IX_VocabularyItems_CourseId_Word",
            table: "VocabularyItems",
            columns: new[] { "CourseId", "Word" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SavedWords");

        migrationBuilder.DropTable(
            name: "VocabularyItems");
    }
}
