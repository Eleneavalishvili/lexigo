using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrackPrivateDraftErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PrivateTextErasedAt",
                table: "LessonVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LessonVersions_PrivateErasure",
                table: "LessonVersions",
                sql: "\"PrivateTextErasedAt\" IS NULL OR (\"Status\" = 'Archived' AND \"PublishedAt\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LessonVersions_PrivateErasure",
                table: "LessonVersions");

            migrationBuilder.DropColumn(
                name: "PrivateTextErasedAt",
                table: "LessonVersions");
        }
    }
}
