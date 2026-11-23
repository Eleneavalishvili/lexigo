using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddUngradedPractice : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsGraded",
            table: "AnswerAttempts",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_AnswerAttempts_Ungraded",
            table: "AnswerAttempts",
            sql: "\"IsGraded\" OR NOT \"IsCorrect\"");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_AnswerAttempts_Ungraded",
            table: "AnswerAttempts");

        migrationBuilder.DropColumn(
            name: "IsGraded",
            table: "AnswerAttempts");
    }
}
