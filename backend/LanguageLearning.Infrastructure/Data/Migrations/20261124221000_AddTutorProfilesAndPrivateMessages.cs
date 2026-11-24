using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorProfilesAndPrivateMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeacherAccessCodeHash",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Biography",
                table: "ExtraStudyOffers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LevelFrom",
                table: "ExtraStudyOffers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "A1");

            migrationBuilder.AddColumn<string>(
                name: "LevelTo",
                table: "ExtraStudyOffers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "B2");

            migrationBuilder.AddColumn<string>(
                name: "PortraitKey",
                table: "ExtraStudyOffers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerLesson",
                table: "ExtraStudyOffers",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ExtraStudyMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtraStudyMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtraStudyMessages_ExtraStudyRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ExtraStudyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExtraStudyMessages_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TeacherAccessCodeHash",
                table: "Users",
                column: "TeacherAccessCodeHash",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExtraStudyOffers_Profile",
                table: "ExtraStudyOffers",
                sql: "\"PricePerLesson\" BETWEEN 0 AND 1000 AND \"LevelFrom\" IN ('A1','A2','B1','B2','C1','C2') AND \"LevelTo\" IN ('A1','A2','B1','B2','C1','C2')");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraStudyMessages_RequestId_CreatedAt_Id",
                table: "ExtraStudyMessages",
                columns: new[] { "RequestId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtraStudyMessages_SenderId",
                table: "ExtraStudyMessages",
                column: "SenderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtraStudyMessages");

            migrationBuilder.DropIndex(
                name: "IX_Users_TeacherAccessCodeHash",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExtraStudyOffers_Profile",
                table: "ExtraStudyOffers");

            migrationBuilder.DropColumn(
                name: "TeacherAccessCodeHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Biography",
                table: "ExtraStudyOffers");

            migrationBuilder.DropColumn(
                name: "LevelFrom",
                table: "ExtraStudyOffers");

            migrationBuilder.DropColumn(
                name: "LevelTo",
                table: "ExtraStudyOffers");

            migrationBuilder.DropColumn(
                name: "PortraitKey",
                table: "ExtraStudyOffers");

            migrationBuilder.DropColumn(
                name: "PricePerLesson",
                table: "ExtraStudyOffers");
        }
    }
}
