using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddMediaLifecycle : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CreatedAt",
            table: "MediaAssets",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "CURRENT_TIMESTAMP");

        migrationBuilder.AddColumn<int>(
            name: "DurationMilliseconds",
            table: "MediaAssets",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Sha256",
            table: "MediaAssets",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StorageState",
            table: "MediaAssets",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "Ready");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "VerifiedAt",
            table: "MediaAssets",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "VerifiedBy",
            table: "MediaAssets",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_MediaAssets_StorageState_CreatedAt",
            table: "MediaAssets",
            columns: new[] { "StorageState", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_MediaAssets_VerifiedBy",
            table: "MediaAssets",
            column: "VerifiedBy");

        migrationBuilder.AddCheckConstraint(
            name: "CK_MediaAssets_Duration",
            table: "MediaAssets",
            sql: "\"DurationMilliseconds\" IS NULL OR \"DurationMilliseconds\" BETWEEN 100 AND 120000");

        migrationBuilder.AddCheckConstraint(
            name: "CK_MediaAssets_Review",
            table: "MediaAssets",
            sql: "(\"VerifiedBy\" IS NULL) = (\"VerifiedAt\" IS NULL) AND (\"VerifiedBy\" IS NULL OR (\"VerifiedBy\" <> \"UploadedBy\" AND \"IsVerified\"))");

        migrationBuilder.AddCheckConstraint(
            name: "CK_MediaAssets_State",
            table: "MediaAssets",
            sql: "\"StorageState\" IN ('Pending', 'Ready', 'Deleting') AND (NOT \"IsVerified\" OR \"StorageState\" = 'Ready')");

        migrationBuilder.AddForeignKey(
            name: "FK_MediaAssets_Users_VerifiedBy",
            table: "MediaAssets",
            column: "VerifiedBy",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_MediaAssets_Users_VerifiedBy",
            table: "MediaAssets");

        migrationBuilder.DropIndex(
            name: "IX_MediaAssets_StorageState_CreatedAt",
            table: "MediaAssets");

        migrationBuilder.DropIndex(
            name: "IX_MediaAssets_VerifiedBy",
            table: "MediaAssets");

        migrationBuilder.DropCheckConstraint(
            name: "CK_MediaAssets_Duration",
            table: "MediaAssets");

        migrationBuilder.DropCheckConstraint(
            name: "CK_MediaAssets_Review",
            table: "MediaAssets");

        migrationBuilder.DropCheckConstraint(
            name: "CK_MediaAssets_State",
            table: "MediaAssets");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "MediaAssets");

        migrationBuilder.DropColumn(
            name: "DurationMilliseconds",
            table: "MediaAssets");

        migrationBuilder.DropColumn(
            name: "Sha256",
            table: "MediaAssets");

        migrationBuilder.DropColumn(
            name: "StorageState",
            table: "MediaAssets");

        migrationBuilder.DropColumn(
            name: "VerifiedAt",
            table: "MediaAssets");

        migrationBuilder.DropColumn(
            name: "VerifiedBy",
            table: "MediaAssets");
    }
}
