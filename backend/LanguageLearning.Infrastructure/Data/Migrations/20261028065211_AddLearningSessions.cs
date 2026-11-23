using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddLearningSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DailyActivities",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                EarnedXp = table.Column<int>(type: "integer", nullable: false),
                CompletedLessons = table.Column<int>(type: "integer", nullable: false),
                GoalReached = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailyActivities", x => new { x.UserId, x.LocalDate });
                table.CheckConstraint("CK_DailyActivities_Counts", "\"EarnedXp\" >= 0 AND \"CompletedLessons\" >= 0");
                table.ForeignKey(
                    name: "FK_DailyActivities_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Enrollments",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                EnrolledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PlacementLevel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Enrollments", x => new { x.UserId, x.CourseId });
                table.ForeignKey(
                    name: "FK_Enrollments_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Enrollments_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LessonProgress",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                BestScore = table.Column<int>(type: "integer", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LessonProgress", x => new { x.UserId, x.LessonId });
                table.CheckConstraint("CK_LessonProgress_Score", "\"BestScore\" BETWEEN 0 AND 100");
                table.CheckConstraint("CK_LessonProgress_Status", "\"Status\" IN ('InProgress','Completed')");
                table.ForeignKey(
                    name: "FK_LessonProgress_Lessons_LessonId",
                    column: x => x.LessonId,
                    principalTable: "Lessons",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LessonProgress_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LearningSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                LessonId = table.Column<Guid>(type: "uuid", nullable: true),
                LessonVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Explanation = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Hearts = table.Column<int>(type: "integer", nullable: false),
                PassScore = table.Column<int>(type: "integer", nullable: false),
                FirstCompletionXp = table.Column<int>(type: "integer", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Score = table.Column<int>(type: "integer", nullable: false),
                AwardedXp = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LearningSessions", x => x.Id);
                table.UniqueConstraint("AK_LearningSessions_UserId_Id", x => new { x.UserId, x.Id });
                table.CheckConstraint("CK_LearningSessions_Mode", "\"Mode\" IN ('Lesson','Practice')");
                table.CheckConstraint("CK_LearningSessions_Values", "\"Hearts\" BETWEEN 0 AND 5 AND \"Score\" BETWEEN 0 AND 100 AND \"AwardedXp\" >= 0 AND \"FirstCompletionXp\" BETWEEN 0 AND 1000 AND \"PassScore\" BETWEEN 1 AND 100");
                table.ForeignKey(
                    name: "FK_LearningSessions_Enrollments_UserId_CourseId",
                    columns: x => new { x.UserId, x.CourseId },
                    principalTable: "Enrollments",
                    principalColumns: new[] { "UserId", "CourseId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LearningSessions_LessonVersions_LessonId_LessonVersionId",
                    columns: x => new { x.LessonId, x.LessonVersionId },
                    principalTable: "LessonVersions",
                    principalColumns: new[] { "LessonId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "XpTransactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<int>(type: "integer", nullable: false),
                EarnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_XpTransactions", x => x.Id);
                table.CheckConstraint("CK_XpTransactions_Amount", "\"Amount\" > 0");
                table.ForeignKey(
                    name: "FK_XpTransactions_Enrollments_UserId_CourseId",
                    columns: x => new { x.UserId, x.CourseId },
                    principalTable: "Enrollments",
                    principalColumns: new[] { "UserId", "CourseId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SessionExercises",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                OrderNumber = table.Column<int>(type: "integer", nullable: false),
                PublicSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                PrivateEvaluationSnapshot = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SessionExercises", x => x.Id);
                table.UniqueConstraint("AK_SessionExercises_UserId_Id", x => new { x.UserId, x.Id });
                table.CheckConstraint("CK_SessionExercises_Order", "\"OrderNumber\" > 0");
                table.ForeignKey(
                    name: "FK_SessionExercises_Exercises_ExerciseId",
                    column: x => x.ExerciseId,
                    principalTable: "Exercises",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SessionExercises_LearningSessions_UserId_SessionId",
                    columns: x => new { x.UserId, x.SessionId },
                    principalTable: "LearningSessions",
                    principalColumns: new[] { "UserId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AnswerAttempts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                AnswerPayload = table.Column<string>(type: "jsonb", nullable: false),
                ResponseSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AnswerAttempts", x => x.Id);
                table.ForeignKey(
                    name: "FK_AnswerAttempts_SessionExercises_UserId_SessionExerciseId",
                    columns: x => new { x.UserId, x.SessionExerciseId },
                    principalTable: "SessionExercises",
                    principalColumns: new[] { "UserId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AnswerAttempts_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AnswerAttempts_SessionExerciseId",
            table: "AnswerAttempts",
            column: "SessionExerciseId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AnswerAttempts_UserId_IdempotencyKey",
            table: "AnswerAttempts",
            columns: new[] { "UserId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AnswerAttempts_UserId_SessionExerciseId",
            table: "AnswerAttempts",
            columns: new[] { "UserId", "SessionExerciseId" });

        migrationBuilder.CreateIndex(
            name: "IX_Enrollments_CourseId",
            table: "Enrollments",
            column: "CourseId");

        migrationBuilder.CreateIndex(
            name: "IX_LearningSessions_LessonId_LessonVersionId",
            table: "LearningSessions",
            columns: new[] { "LessonId", "LessonVersionId" });

        migrationBuilder.CreateIndex(
            name: "IX_LearningSessions_UserId_CourseId",
            table: "LearningSessions",
            columns: new[] { "UserId", "CourseId" });

        migrationBuilder.CreateIndex(
            name: "IX_LearningSessions_UserId_LessonId_Mode",
            table: "LearningSessions",
            columns: new[] { "UserId", "LessonId", "Mode" },
            unique: true,
            filter: "\"CompletedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_LessonProgress_LessonId",
            table: "LessonProgress",
            column: "LessonId");

        migrationBuilder.CreateIndex(
            name: "IX_SessionExercises_ExerciseId",
            table: "SessionExercises",
            column: "ExerciseId");

        migrationBuilder.CreateIndex(
            name: "IX_SessionExercises_SessionId_ExerciseId",
            table: "SessionExercises",
            columns: new[] { "SessionId", "ExerciseId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SessionExercises_SessionId_OrderNumber",
            table: "SessionExercises",
            columns: new[] { "SessionId", "OrderNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SessionExercises_UserId_SessionId",
            table: "SessionExercises",
            columns: new[] { "UserId", "SessionId" });

        migrationBuilder.CreateIndex(
            name: "IX_XpTransactions_UserId_CourseId",
            table: "XpTransactions",
            columns: new[] { "UserId", "CourseId" });

        migrationBuilder.CreateIndex(
            name: "IX_XpTransactions_UserId_SourceType_SourceId",
            table: "XpTransactions",
            columns: new[] { "UserId", "SourceType", "SourceId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AnswerAttempts");

        migrationBuilder.DropTable(
            name: "DailyActivities");

        migrationBuilder.DropTable(
            name: "LessonProgress");

        migrationBuilder.DropTable(
            name: "XpTransactions");

        migrationBuilder.DropTable(
            name: "SessionExercises");

        migrationBuilder.DropTable(
            name: "LearningSessions");

        migrationBuilder.DropTable(
            name: "Enrollments");
    }
}
