using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExerciseSkills",
                columns: table => new
                {
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseSkills", x => new { x.ExerciseId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_ExerciseSkills_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExerciseSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SkillProgress",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    IncorrectCount = table.Column<int>(type: "integer", nullable: false),
                    LastPracticedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillProgress", x => new { x.UserId, x.SkillId });
                    table.CheckConstraint("CK_SkillProgress_Counts", "\"CorrectCount\" >= 0 AND \"IncorrectCount\" >= 0\nAND ((\"CorrectCount\" = 0 AND \"IncorrectCount\" = 0) = (\"LastPracticedAt\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_SkillProgress_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SkillProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseSkills_SkillId",
                table: "ExerciseSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillProgress_SkillId",
                table: "SkillProgress",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_CourseId_Name",
                table: "Skills",
                columns: new[] { "CourseId", "Name" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "Skills" ("CourseId", "Name", "Category")
                SELECT course."Id", skill.name, skill.category
                FROM "Courses" course
                JOIN "Languages" target ON target."Id" = course."TargetLanguageId"
                CROSS JOIN (VALUES ('Vocabulary', 'vocabulary'), ('Grammar', 'grammar'),
                    ('Listening', 'listening'), ('Reading', 'reading'), ('Alphabet', 'alphabet'))
                    AS skill(name, category)
                WHERE skill.category <> 'alphabet' OR target."Code" = 'ru';

                CREATE TRIGGER guard_deleted_account BEFORE INSERT OR UPDATE ON "SkillProgress"
                FOR EACH ROW EXECUTE FUNCTION lexigo_guard_deleted_account('UserId');

                CREATE FUNCTION lexigo_guard_exercise_skill_course() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM "Exercises" exercise
                        JOIN "LessonVersions" version ON version."Id" = exercise."LessonVersionId"
                        JOIN "Lessons" lesson ON lesson."Id" = version."LessonId"
                        JOIN "Units" unit ON unit."Id" = lesson."UnitId"
                        JOIN "Skills" skill ON skill."CourseId" = unit."CourseId"
                        WHERE exercise."Id" = NEW."ExerciseId" AND skill."Id" = NEW."SkillId"
                    ) THEN
                        RAISE EXCEPTION 'Exercise and skill must belong to the same course' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $body$;

                CREATE TRIGGER guard_exercise_skill_course BEFORE INSERT OR UPDATE ON "ExerciseSkills"
                FOR EACH ROW EXECUTE FUNCTION lexigo_guard_exercise_skill_course();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "ExerciseSkills") OR EXISTS (SELECT 1 FROM "SkillProgress") THEN
                        RAISE EXCEPTION 'Archive or migrate skill data before rolling back';
                    END IF;
                END;
                $body$;
                """);
            migrationBuilder.DropTable(
                name: "ExerciseSkills");
            migrationBuilder.Sql("DROP FUNCTION lexigo_guard_exercise_skill_course();");

            migrationBuilder.DropTable(
                name: "SkillProgress");

            migrationBuilder.DropTable(
                name: "Skills");
        }
    }
}
