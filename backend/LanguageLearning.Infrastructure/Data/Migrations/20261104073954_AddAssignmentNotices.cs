using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AssignmentId_UserId_Type",
                table: "Notifications",
                columns: new[] { "AssignmentId", "UserId", "Type" },
                unique: true,
                filter: "\"AssignmentId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Assignments_AssignmentId",
                table: "Notifications",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Assignments_AssignmentId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_AssignmentId_UserId_Type",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "Notifications");
        }
    }
}
