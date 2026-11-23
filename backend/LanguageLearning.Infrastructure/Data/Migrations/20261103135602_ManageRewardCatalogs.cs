using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ManageRewardCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Challenges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Challenges",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Achievements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Achievements",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                columns: new[] { "IsArchived", "Revision" },
                values: new object[] { false, 1 });

            migrationBuilder.UpdateData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                columns: new[] { "IsArchived", "Revision" },
                values: new object[] { false, 1 });

            migrationBuilder.UpdateData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                columns: new[] { "IsArchived", "Revision" },
                values: new object[] { false, 1 });

            migrationBuilder.UpdateData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                columns: new[] { "IsArchived", "Revision" },
                values: new object[] { false, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_RuleType_StartsAt_EndsAt",
                table: "Challenges",
                columns: new[] { "RuleType", "StartsAt", "EndsAt" },
                unique: true,
                filter: "\"IsArchived\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Challenges_RuleType_StartsAt_EndsAt",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Achievements");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Achievements");
        }
    }
}
