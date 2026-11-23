using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtraStudyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtraStudyRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtraStudyRequests", x => x.Id);
                    table.CheckConstraint("CK_ExtraStudyRequests_Status", " \"Status\" IN ('Pending', 'Accepted', 'Declined') ");
                    table.ForeignKey(
                        name: "FK_ExtraStudyRequests_ExtraStudyOffers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "ExtraStudyOffers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExtraStudyRequests_Users_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtraStudyRequests_LearnerId_Id",
                table: "ExtraStudyRequests",
                columns: new[] { "LearnerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtraStudyRequests_OfferId_LearnerId",
                table: "ExtraStudyRequests",
                columns: new[] { "OfferId", "LearnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtraStudyRequests");
        }
    }
}
