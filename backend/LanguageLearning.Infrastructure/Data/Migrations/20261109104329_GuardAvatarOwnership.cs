using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuardAvatarOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_UploadedBy",
                table: "MediaAssets");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MediaAssets_UploadedBy_Id",
                table: "MediaAssets",
                columns: new[] { "UploadedBy", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Id_AvatarMediaId",
                table: "Users",
                columns: new[] { "Id", "AvatarMediaId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_MediaAssets_Id_AvatarMediaId",
                table: "Users",
                columns: new[] { "Id", "AvatarMediaId" },
                principalTable: "MediaAssets",
                principalColumns: new[] { "UploadedBy", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_MediaAssets_Id_AvatarMediaId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Id_AvatarMediaId",
                table: "Users");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MediaAssets_UploadedBy_Id",
                table: "MediaAssets");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_UploadedBy",
                table: "MediaAssets",
                column: "UploadedBy");
        }
    }
}
