using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddLessonPublishing : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MediaAssets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ByteSize = table.Column<long>(type: "bigint", nullable: false),
                LanguageId = table.Column<int>(type: "integer", nullable: true),
                Source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                License = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                IsVerified = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MediaAssets", x => x.Id);
                table.CheckConstraint("CK_MediaAssets_Size", "\"ByteSize\" > 0");
                table.ForeignKey(
                    name: "FK_MediaAssets_Languages_LanguageId",
                    column: x => x.LanguageId,
                    principalTable: "Languages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MediaAssets_Users_UploadedBy",
                    column: x => x.UploadedBy,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Units",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                OrderNumber = table.Column<int>(type: "integer", nullable: false),
                Difficulty = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Units", x => x.Id);
                table.CheckConstraint("CK_Units_Order", "\"OrderNumber\" > 0");
                table.ForeignKey(
                    name: "FK_Units_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AcceptedAnswers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                AnswerText = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                OrderedTokenIds = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AcceptedAnswers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ContentReviews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                LessonVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Comment = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContentReviews", x => x.Id);
                table.CheckConstraint("CK_ContentReviews_Decision", "\"Decision\" IN ('Approved','ChangesRequested')");
                table.ForeignKey(
                    name: "FK_ContentReviews_Users_ReviewerId",
                    column: x => x.ReviewerId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ExerciseOptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                OrderNumber = table.Column<int>(type: "integer", nullable: false),
                MatchKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExerciseOptions", x => x.Id);
                table.CheckConstraint("CK_ExerciseOptions_Order", "\"OrderNumber\" > 0");
            });

        migrationBuilder.CreateTable(
            name: "Exercises",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                LessonVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Prompt = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                Explanation = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                OrderNumber = table.Column<int>(type: "integer", nullable: false),
                AudioMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                EvaluationRules = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Exercises", x => x.Id);
                table.CheckConstraint("CK_Exercises_Order", "\"OrderNumber\" > 0");
                table.ForeignKey(
                    name: "FK_Exercises_MediaAssets_AudioMediaId",
                    column: x => x.AudioMediaId,
                    principalTable: "MediaAssets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LessonPrerequisites",
            columns: table => new
            {
                LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                PrerequisiteLessonId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LessonPrerequisites", x => new { x.LessonId, x.PrerequisiteLessonId });
                table.CheckConstraint("CK_Prerequisites_NotSelf", "\"LessonId\" <> \"PrerequisiteLessonId\"");
            });

        migrationBuilder.CreateTable(
            name: "Lessons",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Objective = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                OrderNumber = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CurrentVersionId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Lessons", x => x.Id);
                table.CheckConstraint("CK_Lessons_Order", "\"OrderNumber\" > 0");
                table.CheckConstraint("CK_Lessons_Status", "\"Status\" IN ('Draft','Submitted','Approved','Published','Archived')");
                table.ForeignKey(
                    name: "FK_Lessons_Units_UnitId",
                    column: x => x.UnitId,
                    principalTable: "Units",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Lessons_Users_OwnerId",
                    column: x => x.OwnerId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LessonVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionNumber = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Objective = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Explanation = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LessonVersions", x => x.Id);
                table.UniqueConstraint("AK_LessonVersions_LessonId_Id", x => new { x.LessonId, x.Id });
                table.CheckConstraint("CK_LessonVersions_Number", "\"VersionNumber\" > 0");
                table.CheckConstraint("CK_LessonVersions_Status", "\"Status\" IN ('Draft','Submitted','Approved','Published','Archived')");
                table.ForeignKey(
                    name: "FK_LessonVersions_Lessons_LessonId",
                    column: x => x.LessonId,
                    principalTable: "Lessons",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LessonVersions_Users_CreatedBy",
                    column: x => x.CreatedBy,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AcceptedAnswers_ExerciseId",
            table: "AcceptedAnswers",
            column: "ExerciseId");

        migrationBuilder.CreateIndex(
            name: "IX_ContentReviews_LessonVersionId_ReviewedAt",
            table: "ContentReviews",
            columns: new[] { "LessonVersionId", "ReviewedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ContentReviews_ReviewerId",
            table: "ContentReviews",
            column: "ReviewerId");

        migrationBuilder.CreateIndex(
            name: "IX_ExerciseOptions_ExerciseId_OrderNumber",
            table: "ExerciseOptions",
            columns: new[] { "ExerciseId", "OrderNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Exercises_AudioMediaId",
            table: "Exercises",
            column: "AudioMediaId");

        migrationBuilder.CreateIndex(
            name: "IX_Exercises_LessonVersionId_OrderNumber",
            table: "Exercises",
            columns: new[] { "LessonVersionId", "OrderNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_LessonPrerequisites_PrerequisiteLessonId",
            table: "LessonPrerequisites",
            column: "PrerequisiteLessonId");

        migrationBuilder.CreateIndex(
            name: "IX_Lessons_Id_CurrentVersionId",
            table: "Lessons",
            columns: new[] { "Id", "CurrentVersionId" });

        migrationBuilder.CreateIndex(
            name: "IX_Lessons_OwnerId",
            table: "Lessons",
            column: "OwnerId");

        migrationBuilder.CreateIndex(
            name: "IX_Lessons_UnitId_OrderNumber",
            table: "Lessons",
            columns: new[] { "UnitId", "OrderNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_LessonVersions_CreatedBy",
            table: "LessonVersions",
            column: "CreatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_LessonVersions_LessonId",
            table: "LessonVersions",
            column: "LessonId",
            unique: true,
            filter: "\"Status\" IN ('Draft','Submitted','Approved')");

        migrationBuilder.CreateIndex(
            name: "IX_LessonVersions_LessonId_VersionNumber",
            table: "LessonVersions",
            columns: new[] { "LessonId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MediaAssets_LanguageId",
            table: "MediaAssets",
            column: "LanguageId");

        migrationBuilder.CreateIndex(
            name: "IX_MediaAssets_StorageKey",
            table: "MediaAssets",
            column: "StorageKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MediaAssets_UploadedBy",
            table: "MediaAssets",
            column: "UploadedBy");

        migrationBuilder.CreateIndex(
            name: "IX_Units_CourseId_OrderNumber",
            table: "Units",
            columns: new[] { "CourseId", "OrderNumber" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_AcceptedAnswers_Exercises_ExerciseId",
            table: "AcceptedAnswers",
            column: "ExerciseId",
            principalTable: "Exercises",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ContentReviews_LessonVersions_LessonVersionId",
            table: "ContentReviews",
            column: "LessonVersionId",
            principalTable: "LessonVersions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ExerciseOptions_Exercises_ExerciseId",
            table: "ExerciseOptions",
            column: "ExerciseId",
            principalTable: "Exercises",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Exercises_LessonVersions_LessonVersionId",
            table: "Exercises",
            column: "LessonVersionId",
            principalTable: "LessonVersions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_LessonPrerequisites_Lessons_LessonId",
            table: "LessonPrerequisites",
            column: "LessonId",
            principalTable: "Lessons",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_LessonPrerequisites_Lessons_PrerequisiteLessonId",
            table: "LessonPrerequisites",
            column: "PrerequisiteLessonId",
            principalTable: "Lessons",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Lessons_LessonVersions_Id_CurrentVersionId",
            table: "Lessons",
            columns: new[] { "Id", "CurrentVersionId" },
            principalTable: "LessonVersions",
            principalColumns: new[] { "LessonId", "Id" },
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Lessons_LessonVersions_Id_CurrentVersionId",
            table: "Lessons");

        migrationBuilder.DropTable(
            name: "AcceptedAnswers");

        migrationBuilder.DropTable(
            name: "ContentReviews");

        migrationBuilder.DropTable(
            name: "ExerciseOptions");

        migrationBuilder.DropTable(
            name: "LessonPrerequisites");

        migrationBuilder.DropTable(
            name: "Exercises");

        migrationBuilder.DropTable(
            name: "MediaAssets");

        migrationBuilder.DropTable(
            name: "LessonVersions");

        migrationBuilder.DropTable(
            name: "Lessons");

        migrationBuilder.DropTable(
            name: "Units");
    }
}
