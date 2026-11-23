using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddTestCardWallet : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BillingOperations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Operation = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                RequestSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                ResponseSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BillingOperations", x => x.Id);
                table.ForeignKey(
                    name: "FK_BillingOperations_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "TestCards",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Nickname = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                DisplayLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TestCards", x => x.Id);
                table.UniqueConstraint("AK_TestCards_UserId_Id", x => new { x.UserId, x.Id });
                table.CheckConstraint("CK_TestCard_Values", "\"Balance\" BETWEEN 0 AND 10000 AND \"Version\" > 0 AND \"Status\" IN ('Active','Blocked') AND \"Currency\" IN ('GEL','USD','EUR') AND \"DisplayLastFour\" ~ '^[0-9]{4}$'");
                table.ForeignKey(
                    name: "FK_TestCards_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "BillingPreferences",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                DefaultTestCardId = table.Column<Guid>(type: "uuid", nullable: true),
                AutoRenew = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BillingPreferences", x => x.UserId);
                table.ForeignKey(
                    name: "FK_BillingPreferences_TestCards_UserId_DefaultTestCardId",
                    columns: x => new { x.UserId, x.DefaultTestCardId },
                    principalTable: "TestCards",
                    principalColumns: new[] { "UserId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_BillingPreferences_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CardTransactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TestCardId = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                RefundId = table.Column<Guid>(type: "uuid", nullable: true),
                Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CardTransactions", x => x.Id);
                table.CheckConstraint("CK_CardTransaction_TopUp", "\"Type\" = 'TopUp' AND \"Amount\" > 0 AND \"Amount\" <= 1000 AND \"BalanceAfter\" BETWEEN 0 AND 10000 AND \"PaymentId\" IS NULL AND \"RefundId\" IS NULL");
                table.ForeignKey(
                    name: "FK_CardTransactions_TestCards_TestCardId",
                    column: x => x.TestCardId,
                    principalTable: "TestCards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BillingOperations_UserId_Operation_IdempotencyKey",
            table: "BillingOperations",
            columns: new[] { "UserId", "Operation", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_BillingPreferences_UserId_DefaultTestCardId",
            table: "BillingPreferences",
            columns: new[] { "UserId", "DefaultTestCardId" });

        migrationBuilder.CreateIndex(
            name: "IX_CardTransactions_Reference",
            table: "CardTransactions",
            column: "Reference",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CardTransactions_TestCardId_CreatedAt",
            table: "CardTransactions",
            columns: new[] { "TestCardId", "CreatedAt" });
        migrationBuilder.Sql("""
            CREATE FUNCTION lexigo_protect_card_ledger() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
                RAISE EXCEPTION 'Card transactions are append-only.' USING ERRCODE = '23514';
            END;
            $body$;
            CREATE TRIGGER protect_card_ledger BEFORE UPDATE OR DELETE ON "CardTransactions"
                FOR EACH ROW EXECUTE FUNCTION lexigo_protect_card_ledger();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER protect_card_ledger ON \"CardTransactions\"; DROP FUNCTION lexigo_protect_card_ledger();");
        migrationBuilder.DropTable(
            name: "BillingOperations");

        migrationBuilder.DropTable(
            name: "BillingPreferences");

        migrationBuilder.DropTable(
            name: "CardTransactions");

        migrationBuilder.DropTable(
            name: "TestCards");
    }
}
