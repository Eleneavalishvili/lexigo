using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabularyRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VocabularyRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublisherId = table.Column<Guid>(type: "uuid", nullable: true),
                    Word = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EnglishTranslation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Example = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PartOfSpeech = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Topic = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReviewComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyRevisions", x => x.Id);
                    table.CheckConstraint("CK_VocabularyRevision_Workflow", "\"Status\" IN ('Draft', 'Submitted', 'Approved', 'Published')\nAND (\"ReviewerId\" IS NULL OR \"ReviewerId\" <> \"AuthorId\")\nAND (\"Status\" NOT IN ('Approved', 'Published') OR (\"ReviewerId\" IS NOT NULL AND \"ReviewedAt\" IS NOT NULL))\nAND (\"Status\" <> 'Published' OR (\"PublisherId\" IS NOT NULL AND \"PublishedAt\" IS NOT NULL AND \"WordId\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_VocabularyRevisions_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VocabularyRevisions_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VocabularyRevisions_Users_PublisherId",
                        column: x => x.PublisherId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VocabularyRevisions_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VocabularyRevisions_VocabularyItems_WordId",
                        column: x => x.WordId,
                        principalTable: "VocabularyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyRevisions_AuthorId",
                table: "VocabularyRevisions",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyRevisions_CourseId_CreatedAt",
                table: "VocabularyRevisions",
                columns: new[] { "CourseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyRevisions_PublisherId",
                table: "VocabularyRevisions",
                column: "PublisherId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyRevisions_ReviewerId",
                table: "VocabularyRevisions",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyRevisions_WordId",
                table: "VocabularyRevisions",
                column: "WordId",
                unique: true,
                filter: "\"Status\" <> 'Published'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VocabularyRevisions");
        }
    }
}
