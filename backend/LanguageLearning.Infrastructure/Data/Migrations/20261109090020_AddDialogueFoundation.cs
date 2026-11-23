using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDialogueFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dialogues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Scenario = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dialogues", x => x.Id);
                    table.CheckConstraint("CK_Dialogues_VersionStatus", " \"VersionNumber\" > 0 AND \"Status\" IN ('Draft', 'Published', 'Archived') ");
                    table.ForeignKey(
                        name: "FK_Dialogues_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DialogueNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DialogueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AudioMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsStart = table.Column<bool>(type: "boolean", nullable: false),
                    IsTerminal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueNodes", x => x.Id);
                    table.UniqueConstraint("AK_DialogueNodes_DialogueId_Id", x => new { x.DialogueId, x.Id });
                    table.CheckConstraint("CK_DialogueNodes_StartTerminal", " NOT (\"IsStart\" AND \"IsTerminal\") ");
                    table.ForeignKey(
                        name: "FK_DialogueNodes_Dialogues_DialogueId",
                        column: x => x.DialogueId,
                        principalTable: "Dialogues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DialogueNodes_MediaAssets_AudioMediaId",
                        column: x => x.AudioMediaId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DialogueChoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DialogueId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Feedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    NextNodeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueChoices", x => x.Id);
                    table.UniqueConstraint("AK_DialogueChoices_DialogueId_Id", x => new { x.DialogueId, x.Id });
                    table.CheckConstraint("CK_DialogueChoices_NoSelfEdge", " \"NextNodeId\" IS DISTINCT FROM \"NodeId\" ");
                    table.ForeignKey(
                        name: "FK_DialogueChoices_DialogueNodes_DialogueId_NextNodeId",
                        columns: x => new { x.DialogueId, x.NextNodeId },
                        principalTable: "DialogueNodes",
                        principalColumns: new[] { "DialogueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DialogueChoices_DialogueNodes_DialogueId_NodeId",
                        columns: x => new { x.DialogueId, x.NodeId },
                        principalTable: "DialogueNodes",
                        principalColumns: new[] { "DialogueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DialogueSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DialogueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueSessions", x => x.Id);
                    table.UniqueConstraint("AK_DialogueSessions_DialogueId_Id", x => new { x.DialogueId, x.Id });
                    table.CheckConstraint("CK_DialogueSessions_Time", " \"CompletedAt\" IS NULL OR \"CompletedAt\" >= \"StartedAt\" ");
                    table.ForeignKey(
                        name: "FK_DialogueSessions_DialogueNodes_DialogueId_CurrentNodeId",
                        columns: x => new { x.DialogueId, x.CurrentNodeId },
                        principalTable: "DialogueNodes",
                        principalColumns: new[] { "DialogueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DialogueSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DialogueAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DialogueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialogueAttempts_DialogueChoices_DialogueId_ChoiceId",
                        columns: x => new { x.DialogueId, x.ChoiceId },
                        principalTable: "DialogueChoices",
                        principalColumns: new[] { "DialogueId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DialogueAttempts_DialogueSessions_DialogueId_SessionId",
                        columns: x => new { x.DialogueId, x.SessionId },
                        principalTable: "DialogueSessions",
                        principalColumns: new[] { "DialogueId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueAttempts_DialogueId_ChoiceId",
                table: "DialogueAttempts",
                columns: new[] { "DialogueId", "ChoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueAttempts_DialogueId_SessionId",
                table: "DialogueAttempts",
                columns: new[] { "DialogueId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueAttempts_SessionId_IdempotencyKey",
                table: "DialogueAttempts",
                columns: new[] { "SessionId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DialogueChoices_DialogueId_NextNodeId",
                table: "DialogueChoices",
                columns: new[] { "DialogueId", "NextNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueChoices_DialogueId_NodeId",
                table: "DialogueChoices",
                columns: new[] { "DialogueId", "NodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueNodes_AudioMediaId",
                table: "DialogueNodes",
                column: "AudioMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueNodes_DialogueId",
                table: "DialogueNodes",
                column: "DialogueId",
                unique: true,
                filter: "\"IsStart\"");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_CourseId_Scenario_VersionNumber",
                table: "Dialogues",
                columns: new[] { "CourseId", "Scenario", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DialogueSessions_DialogueId_CurrentNodeId",
                table: "DialogueSessions",
                columns: new[] { "DialogueId", "CurrentNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueSessions_UserId_DialogueId",
                table: "DialogueSessions",
                columns: new[] { "UserId", "DialogueId" },
                unique: true,
                filter: "\"CompletedAt\" IS NULL");

            migrationBuilder.Sql("""
                CREATE TRIGGER guard_deleted_account BEFORE INSERT OR UPDATE ON "DialogueSessions"
                FOR EACH ROW EXECUTE FUNCTION lexigo_guard_deleted_account('UserId');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Dialogues") THEN
                        RAISE EXCEPTION 'Archive or migrate dialogue data before rolling back';
                    END IF;
                END;
                $body$;
                """);
            migrationBuilder.DropTable(
                name: "DialogueAttempts");

            migrationBuilder.DropTable(
                name: "DialogueChoices");

            migrationBuilder.DropTable(
                name: "DialogueSessions");

            migrationBuilder.DropTable(
                name: "DialogueNodes");

            migrationBuilder.DropTable(
                name: "Dialogues");
        }
    }
}
