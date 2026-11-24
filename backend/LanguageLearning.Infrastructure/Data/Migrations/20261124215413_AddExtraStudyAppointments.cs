using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtraStudyAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "ExtraStudyLessons",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledAt",
                table: "ExtraStudyLessons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExtraStudyLessons_ScheduledAt",
                table: "ExtraStudyLessons",
                column: "ScheduledAt");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExtraStudyLessons_Schedule",
                table: "ExtraStudyLessons",
                sql: "(\"ScheduledAt\" IS NULL AND \"DurationMinutes\" IS NULL) OR (\"ScheduledAt\" IS NOT NULL AND \"DurationMinutes\" IS NOT NULL AND \"DurationMinutes\" BETWEEN 15 AND 120)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExtraStudyLessons_ScheduledAt",
                table: "ExtraStudyLessons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExtraStudyLessons_Schedule",
                table: "ExtraStudyLessons");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "ExtraStudyLessons");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "ExtraStudyLessons");
        }
    }
}
