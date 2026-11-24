using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDialogueAuthoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Dialogues_VersionStatus",
                table: "Dialogues");

            migrationBuilder.AddColumn<Guid>(
                name: "EditToken",
                table: "Dialogues",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Dialogues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "Dialogues",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "Dialogues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerId",
                table: "Dialogues",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_OwnerId",
                table: "Dialogues",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_ReviewerId",
                table: "Dialogues",
                column: "ReviewerId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Dialogues_VersionStatus",
                table: "Dialogues",
                sql: " \"VersionNumber\" > 0 AND \"Status\" IN ('Draft', 'Submitted', 'Approved', 'Published', 'Archived') ");

            migrationBuilder.AddForeignKey(
                name: "FK_Dialogues_Users_OwnerId",
                table: "Dialogues",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Dialogues_Users_ReviewerId",
                table: "Dialogues",
                column: "ReviewerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Dialogues" WHERE "OwnerId" IS NOT NULL
                        OR "ReviewerId" IS NOT NULL OR "Status" IN ('Submitted', 'Approved')) THEN
                        RAISE EXCEPTION 'Retain dialogue authorship and review history before downgrading.';
                    END IF;
                END; $body$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Dialogues_Users_OwnerId",
                table: "Dialogues");

            migrationBuilder.DropForeignKey(
                name: "FK_Dialogues_Users_ReviewerId",
                table: "Dialogues");

            migrationBuilder.DropIndex(
                name: "IX_Dialogues_OwnerId",
                table: "Dialogues");

            migrationBuilder.DropIndex(
                name: "IX_Dialogues_ReviewerId",
                table: "Dialogues");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Dialogues_VersionStatus",
                table: "Dialogues");

            migrationBuilder.DropColumn(
                name: "EditToken",
                table: "Dialogues");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Dialogues");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "Dialogues");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Dialogues");

            migrationBuilder.DropColumn(
                name: "ReviewerId",
                table: "Dialogues");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Dialogues_VersionStatus",
                table: "Dialogues",
                sql: " \"VersionNumber\" > 0 AND \"Status\" IN ('Draft', 'Published', 'Archived') ");
        }
    }
}
