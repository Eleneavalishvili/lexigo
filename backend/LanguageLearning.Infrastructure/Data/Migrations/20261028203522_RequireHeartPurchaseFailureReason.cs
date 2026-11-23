using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class RequireHeartPurchaseFailureReason : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_HeartRefillPurchase_Values",
            table: "HeartRefillPurchases");

        migrationBuilder.AddCheckConstraint(
            name: "CK_HeartRefillPurchase_Values",
            table: "HeartRefillPurchases",
            sql: "\"Amount\" > 0 AND \"Amount\" <= 1000 AND \"Currency\" = 'GEL' AND \"HeartsBefore\" BETWEEN 0 AND 5 AND \"HeartsAfter\" BETWEEN 0 AND 5 AND ((\"Status\" = 'Succeeded' AND \"FailureCode\" IS NULL AND \"HeartsBefore\" < 5 AND \"HeartsAfter\" = 5) OR (\"Status\" = 'Failed' AND \"FailureCode\" IS NOT NULL AND \"FailureCode\" IN ('CardBlocked','CurrencyMismatch','InsufficientBalance','HeartsAlreadyFull') AND \"HeartsBefore\" = \"HeartsAfter\"))");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_HeartRefillPurchase_Values",
            table: "HeartRefillPurchases");

        migrationBuilder.AddCheckConstraint(
            name: "CK_HeartRefillPurchase_Values",
            table: "HeartRefillPurchases",
            sql: "\"Amount\" > 0 AND \"Amount\" <= 1000 AND \"Currency\" = 'GEL' AND \"HeartsBefore\" BETWEEN 0 AND 5 AND \"HeartsAfter\" BETWEEN 0 AND 5 AND ((\"Status\" = 'Succeeded' AND \"FailureCode\" IS NULL AND \"HeartsBefore\" < 5 AND \"HeartsAfter\" = 5) OR (\"Status\" = 'Failed' AND \"FailureCode\" IN ('CardBlocked','CurrencyMismatch','InsufficientBalance','HeartsAlreadyFull') AND \"HeartsBefore\" = \"HeartsAfter\"))");
    }
}
