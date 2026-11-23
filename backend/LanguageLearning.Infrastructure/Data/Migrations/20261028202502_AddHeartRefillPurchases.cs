using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddHeartRefillPurchases : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_CardTransaction_TopUp",
            table: "CardTransactions");

        migrationBuilder.AddColumn<Guid>(
            name: "HeartRefillPurchaseId",
            table: "CardTransactions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "HeartRefillPurchases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TestCardId = table.Column<Guid>(type: "uuid", nullable: false),
                IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                OfferCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                FailureCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                HeartsBefore = table.Column<int>(type: "integer", nullable: false),
                HeartsAfter = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HeartRefillPurchases", x => x.Id);
                table.UniqueConstraint("AK_HeartRefillPurchases_TestCardId_Id", x => new { x.TestCardId, x.Id });
                table.CheckConstraint("CK_HeartRefillPurchase_Values", "\"Amount\" > 0 AND \"Amount\" <= 1000 AND \"Currency\" = 'GEL' AND \"HeartsBefore\" BETWEEN 0 AND 5 AND \"HeartsAfter\" BETWEEN 0 AND 5 AND ((\"Status\" = 'Succeeded' AND \"FailureCode\" IS NULL AND \"HeartsBefore\" < 5 AND \"HeartsAfter\" = 5) OR (\"Status\" = 'Failed' AND \"FailureCode\" IN ('CardBlocked','CurrencyMismatch','InsufficientBalance','HeartsAlreadyFull') AND \"HeartsBefore\" = \"HeartsAfter\"))");
                table.ForeignKey(
                    name: "FK_HeartRefillPurchases_TestCards_UserId_TestCardId",
                    columns: x => new { x.UserId, x.TestCardId },
                    principalTable: "TestCards",
                    principalColumns: new[] { "UserId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CardTransactions_HeartRefillPurchaseId",
            table: "CardTransactions",
            column: "HeartRefillPurchaseId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CardTransactions_TestCardId_HeartRefillPurchaseId",
            table: "CardTransactions",
            columns: new[] { "TestCardId", "HeartRefillPurchaseId" });

        migrationBuilder.AddCheckConstraint(
            name: "CK_CardTransaction_Values",
            table: "CardTransactions",
            sql: "\"Amount\" > 0 AND \"BalanceAfter\" BETWEEN 0 AND 10000 AND \"PaymentId\" IS NULL AND \"RefundId\" IS NULL AND ((\"Type\" = 'TopUp' AND \"Amount\" <= 1000 AND \"HeartRefillPurchaseId\" IS NULL) OR (\"Type\" = 'Debit' AND \"Amount\" <= 1000 AND \"HeartRefillPurchaseId\" IS NOT NULL))");

        migrationBuilder.CreateIndex(
            name: "IX_HeartRefillPurchases_Reference",
            table: "HeartRefillPurchases",
            column: "Reference",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_HeartRefillPurchases_UserId_CreatedAt",
            table: "HeartRefillPurchases",
            columns: new[] { "UserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_HeartRefillPurchases_UserId_IdempotencyKey",
            table: "HeartRefillPurchases",
            columns: new[] { "UserId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_HeartRefillPurchases_UserId_TestCardId",
            table: "HeartRefillPurchases",
            columns: new[] { "UserId", "TestCardId" });

        migrationBuilder.AddForeignKey(
            name: "FK_CardTransactions_HeartRefillPurchases_TestCardId_HeartRefil~",
            table: "CardTransactions",
            columns: new[] { "TestCardId", "HeartRefillPurchaseId" },
            principalTable: "HeartRefillPurchases",
            principalColumns: new[] { "TestCardId", "Id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.Sql("""
            CREATE FUNCTION lexigo_protect_heart_purchase() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Heart purchase evidence is append-only.' USING ERRCODE = '23514'; END;
            $body$;
            CREATE TRIGGER protect_heart_purchase BEFORE UPDATE OR DELETE ON "HeartRefillPurchases"
                FOR EACH ROW EXECUTE FUNCTION lexigo_protect_heart_purchase();
            CREATE FUNCTION lexigo_check_heart_debit() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE purchase_id uuid;
            BEGIN
                IF TG_TABLE_NAME = 'HeartRefillPurchases' THEN purchase_id := NEW."Id";
                ELSE purchase_id := NEW."HeartRefillPurchaseId"; END IF;
                IF purchase_id IS NULL THEN RETURN NULL; END IF;
                IF EXISTS (
                    SELECT 1 FROM "HeartRefillPurchases" p WHERE p."Id" = purchase_id AND (
                        (p."Status" = 'Succeeded' AND NOT EXISTS (
                            SELECT 1 FROM "CardTransactions" t JOIN "TestCards" c ON c."Id" = t."TestCardId"
                            WHERE t."HeartRefillPurchaseId" = p."Id" AND t."Type" = 'Debit'
                                AND t."TestCardId" = p."TestCardId" AND t."Amount" = p."Amount" AND c."Currency" = p."Currency"
                        )) OR (p."Status" = 'Failed' AND EXISTS (
                            SELECT 1 FROM "CardTransactions" t WHERE t."HeartRefillPurchaseId" = p."Id"
                        ))
                    )
                ) THEN RAISE EXCEPTION 'Heart purchase and debit must match.' USING ERRCODE = '23514'; END IF;
                RETURN NULL;
            END; $body$;
            CREATE CONSTRAINT TRIGGER check_heart_purchase_debit AFTER INSERT ON "HeartRefillPurchases"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_heart_debit();
            CREATE CONSTRAINT TRIGGER check_heart_debit_purchase AFTER INSERT ON "CardTransactions"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_heart_debit();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $body$ BEGIN
                IF EXISTS (SELECT 1 FROM "HeartRefillPurchases") THEN
                    RAISE EXCEPTION 'Preserve purchase evidence: use a forward fix or a verified database restore.';
                END IF;
            END; $body$;
            """);
        migrationBuilder.Sql("""
            DROP TRIGGER check_heart_purchase_debit ON "HeartRefillPurchases";
            DROP TRIGGER check_heart_debit_purchase ON "CardTransactions";
            DROP FUNCTION lexigo_check_heart_debit();
            DROP TRIGGER protect_heart_purchase ON "HeartRefillPurchases";
            DROP FUNCTION lexigo_protect_heart_purchase();
            """);
        migrationBuilder.DropForeignKey(
            name: "FK_CardTransactions_HeartRefillPurchases_TestCardId_HeartRefil~",
            table: "CardTransactions");

        migrationBuilder.DropTable(
            name: "HeartRefillPurchases");

        migrationBuilder.DropIndex(
            name: "IX_CardTransactions_HeartRefillPurchaseId",
            table: "CardTransactions");

        migrationBuilder.DropIndex(
            name: "IX_CardTransactions_TestCardId_HeartRefillPurchaseId",
            table: "CardTransactions");

        migrationBuilder.DropCheckConstraint(
            name: "CK_CardTransaction_Values",
            table: "CardTransactions");

        migrationBuilder.DropColumn(
            name: "HeartRefillPurchaseId",
            table: "CardTransactions");

        migrationBuilder.AddCheckConstraint(
            name: "CK_CardTransaction_TopUp",
            table: "CardTransactions",
            sql: "\"Type\" = 'TopUp' AND \"Amount\" > 0 AND \"Amount\" <= 1000 AND \"BalanceAfter\" BETWEEN 0 AND 10000 AND \"PaymentId\" IS NULL AND \"RefundId\" IS NULL");
    }
}
