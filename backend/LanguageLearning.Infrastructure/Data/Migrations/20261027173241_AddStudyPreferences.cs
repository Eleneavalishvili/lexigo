using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddStudyPreferences : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ActiveCourseId",
            table: "UserPreferences",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "EmailRemindersEnabled",
            table: "UserPreferences",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "ReminderTime",
            table: "UserPreferences",
            type: "time without time zone",
            nullable: false,
            defaultValue: new TimeOnly(18, 0, 0));

        migrationBuilder.AddColumn<int>(
            name: "StudyDays",
            table: "UserPreferences",
            type: "integer",
            nullable: false,
            defaultValue: 31);

        migrationBuilder.CreateIndex(
            name: "IX_UserPreferences_ActiveCourseId",
            table: "UserPreferences",
            column: "ActiveCourseId");

        migrationBuilder.AddCheckConstraint(
            name: "CK_UserPreferences_StudyDays",
            table: "UserPreferences",
            sql: "\"StudyDays\" BETWEEN 1 AND 127");

        migrationBuilder.AddCheckConstraint(
            name: "CK_UserPreferences_Theme",
            table: "UserPreferences",
            sql: "\"Theme\" IN ('system', 'light', 'dark')");

        migrationBuilder.AddForeignKey(
            name: "FK_UserPreferences_Courses_ActiveCourseId",
            table: "UserPreferences",
            column: "ActiveCourseId",
            principalTable: "Courses",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_UserPreferences_Courses_ActiveCourseId",
            table: "UserPreferences");

        migrationBuilder.DropIndex(
            name: "IX_UserPreferences_ActiveCourseId",
            table: "UserPreferences");

        migrationBuilder.DropCheckConstraint(
            name: "CK_UserPreferences_StudyDays",
            table: "UserPreferences");

        migrationBuilder.DropCheckConstraint(
            name: "CK_UserPreferences_Theme",
            table: "UserPreferences");

        migrationBuilder.DropColumn(
            name: "ActiveCourseId",
            table: "UserPreferences");

        migrationBuilder.DropColumn(
            name: "EmailRemindersEnabled",
            table: "UserPreferences");

        migrationBuilder.DropColumn(
            name: "ReminderTime",
            table: "UserPreferences");

        migrationBuilder.DropColumn(
            name: "StudyDays",
            table: "UserPreferences");
    }
}
