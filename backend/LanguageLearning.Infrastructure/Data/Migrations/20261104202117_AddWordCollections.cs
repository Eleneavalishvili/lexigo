using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWordCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WordCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordCollections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WordCollections_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WordCollections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionWords",
                columns: table => new
                {
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedWordId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionWords", x => new { x.CollectionId, x.SavedWordId });
                    table.ForeignKey(
                        name: "FK_CollectionWords_SavedWords_SavedWordId",
                        column: x => x.SavedWordId,
                        principalTable: "SavedWords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionWords_WordCollections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "WordCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionWords_SavedWordId",
                table: "CollectionWords",
                column: "SavedWordId");

            migrationBuilder.CreateIndex(
                name: "IX_WordCollections_CourseId",
                table: "WordCollections",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_WordCollections_UserId_CourseId_Name",
                table: "WordCollections",
                columns: new[] { "UserId", "CourseId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionWords");

            migrationBuilder.DropTable(
                name: "WordCollections");
        }
    }
}
