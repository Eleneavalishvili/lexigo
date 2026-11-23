using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddAccountSessionsAndRecovery : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EmailOutboxMessages_UserId",
            table: "EmailOutboxMessages");

        migrationBuilder.AddColumn<string>(
            name: "Purpose",
            table: "EmailOutboxMessages",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "EmailConfirmation");

        migrationBuilder.CreateTable(
            name: "RefreshSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SecurityStamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                DeviceLabel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                MfaVerified = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevocationReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                ReplacedById = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefreshSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_RefreshSessions_RefreshSessions_ReplacedById",
                    column: x => x.ReplacedById,
                    principalTable: "RefreshSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RefreshSessions_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EmailOutboxMessages_UserId_Purpose_CreatedAt",
            table: "EmailOutboxMessages",
            columns: new[] { "UserId", "Purpose", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_RefreshSessions_FamilyId",
            table: "RefreshSessions",
            column: "FamilyId",
            unique: true,
            filter: "\"RevokedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshSessions_ReplacedById",
            table: "RefreshSessions",
            column: "ReplacedById");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshSessions_TokenHash",
            table: "RefreshSessions",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshSessions_UserId_ExpiresAt",
            table: "RefreshSessions",
            columns: new[] { "UserId", "ExpiresAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RefreshSessions");

        migrationBuilder.DropIndex(
            name: "IX_EmailOutboxMessages_UserId_Purpose_CreatedAt",
            table: "EmailOutboxMessages");

        migrationBuilder.DropColumn(
            name: "Purpose",
            table: "EmailOutboxMessages");

        migrationBuilder.CreateIndex(
            name: "IX_EmailOutboxMessages_UserId",
            table: "EmailOutboxMessages",
            column: "UserId");
    }
}
