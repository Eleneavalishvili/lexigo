using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparateReminderPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReminderPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    LocalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    WeekDays = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderPreferences", x => x.UserId);
                    table.CheckConstraint("CK_ReminderPreferences_Channel", "\"Channel\" = 'Email'");
                    table.CheckConstraint("CK_ReminderPreferences_WeekDays", "jsonb_typeof(\"WeekDays\") = 'array' AND \"WeekDays\" <> '[]'::jsonb AND \"WeekDays\" <@ '[0,1,2,3,4,5,6]'::jsonb");
                    table.ForeignKey(
                        name: "FK_ReminderPreferences_UserPreferences_UserId",
                        column: x => x.UserId,
                        principalTable: "UserPreferences",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "ReminderPreferences" ("UserId", "Channel", "Enabled", "LocalTime", "WeekDays")
                SELECT "UserId", 'Email', "EmailRemindersEnabled", "ReminderTime",
                    (SELECT jsonb_agg(day ORDER BY day) FROM generate_series(0, 6) AS day
                     WHERE ("StudyDays" & (1 << day)) <> 0)
                FROM "UserPreferences";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserPreferences_StudyDays",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.Sql("""
                UPDATE "UserPreferences" AS profile
                SET "EmailRemindersEnabled" = reminder."Enabled", "ReminderTime" = reminder."LocalTime",
                    "StudyDays" = (SELECT bit_or(1 << day::int) FROM jsonb_array_elements_text(reminder."WeekDays") AS day)
                FROM "ReminderPreferences" AS reminder
                WHERE profile."UserId" = reminder."UserId";
                """);

            migrationBuilder.DropTable(
                name: "ReminderPreferences");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserPreferences_StudyDays",
                table: "UserPreferences",
                sql: "\"StudyDays\" BETWEEN 1 AND 127");
        }
    }
}
