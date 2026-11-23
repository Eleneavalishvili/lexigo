using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Skills",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Skills",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Skill_Revision",
                table: "Skills",
                sql: "\"Revision\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Skills" WHERE "IsArchived" OR "Revision" > 1) THEN
                        RAISE EXCEPTION 'Retain skill archive and revision history before downgrading.';
                    END IF;
                END; $body$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Skill_Revision",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Skills");
        }
    }
}
