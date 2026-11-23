using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardAvatarConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SharedLeaderboardAvatarId",
                table: "UserPreferences",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_SharedLeaderboardAvatarId",
                table: "UserPreferences",
                column: "SharedLeaderboardAvatarId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserPreferences_SharedLeaderboardAvatar",
                table: "UserPreferences",
                sql: " \"SharedLeaderboardAvatarId\" IS NULL OR \"LeaderboardOptIn\" ");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPreferences_MediaAssets_SharedLeaderboardAvatarId",
                table: "UserPreferences",
                column: "SharedLeaderboardAvatarId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPreferences_MediaAssets_SharedLeaderboardAvatarId",
                table: "UserPreferences");

            migrationBuilder.DropIndex(
                name: "IX_UserPreferences_SharedLeaderboardAvatarId",
                table: "UserPreferences");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserPreferences_SharedLeaderboardAvatar",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "SharedLeaderboardAvatarId",
                table: "UserPreferences");
        }
    }
}
