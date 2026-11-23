using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class EnforcePlanPriceWindows : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PlanPrices_PlanId_Period_ValidFrom",
            table: "PlanPrices");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

        migrationBuilder.CreateIndex(
            name: "IX_PlanPrices_PlanId_Period_Currency_ValidFrom",
            table: "PlanPrices",
            columns: new[] { "PlanId", "Period", "Currency", "ValidFrom" },
            unique: true);

        // Half-open windows allow an exact handoff, but never two applicable prices.
        migrationBuilder.Sql("""
            ALTER TABLE "PlanPrices" ADD CONSTRAINT "EX_PlanPrice_Window"
            EXCLUDE USING gist (
                "PlanId" WITH =,
                "Period" WITH =,
                "Currency" WITH =,
                tstzrange("ValidFrom", "ValidTo", '[)') WITH &&
            );
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $body$ BEGIN
                IF EXISTS (SELECT 1 FROM "PlanPrices" GROUP BY "PlanId", "Period", "ValidFrom" HAVING count(*) > 1) THEN
                    RAISE EXCEPTION 'Reconcile simultaneous currency versions before rolling back the price-window migration.';
                END IF;
            END; $body$;
            ALTER TABLE "PlanPrices" DROP CONSTRAINT "EX_PlanPrice_Window";
            """);
        migrationBuilder.DropIndex(
            name: "IX_PlanPrices_PlanId_Period_Currency_ValidFrom",
            table: "PlanPrices");

        // Keep the trusted extension installed; another schema may use it.

        migrationBuilder.CreateIndex(
            name: "IX_PlanPrices_PlanId_Period_ValidFrom",
            table: "PlanPrices",
            columns: new[] { "PlanId", "Period", "ValidFrom" },
            unique: true);
    }
}
