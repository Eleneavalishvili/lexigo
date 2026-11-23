using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSimulatedRefunds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_TestCard_Values",
            table: "TestCards");

        migrationBuilder.DropCheckConstraint(
            name: "CK_CardTransaction_Values",
            table: "CardTransactions");

        migrationBuilder.AddColumn<Guid>(
            name: "RefundId",
            table: "SubscriptionEvents",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Refunds",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedPremium = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Refunds", x => x.Id);
                table.CheckConstraint("CK_Refund_Result", "\"Amount\" > 0 AND \"Amount\" <= 10000 AND length(btrim(\"Reason\")) BETWEEN 3 AND 500 AND \"Status\" = 'Succeeded' AND \"CompletedAt\" IS NOT NULL AND \"CompletedAt\" >= \"CreatedAt\"");
                table.ForeignKey(
                    name: "FK_Refunds_PaymentAttempts_PaymentId",
                    column: x => x.PaymentId,
                    principalTable: "PaymentAttempts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Refunds_Users_CreatedBy",
                    column: x => x.CreatedBy,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "CK_TestCard_Values",
            table: "TestCards",
            sql: "\"Balance\" >= 0 AND \"Version\" > 0 AND \"Status\" IN ('Active','Blocked') AND \"Currency\" IN ('GEL','USD','EUR') AND \"DisplayLastFour\" ~ '^[0-9]{4}$'");

        migrationBuilder.CreateIndex(
            name: "IX_SubscriptionEvents_RefundId",
            table: "SubscriptionEvents",
            column: "RefundId",
            unique: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_SubscriptionEvent_Refund",
            table: "SubscriptionEvents",
            sql: "\"RefundId\" IS NULL OR \"RelatedPaymentId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_CardTransactions_RefundId",
            table: "CardTransactions",
            column: "RefundId",
            unique: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_CardTransaction_Values",
            table: "CardTransactions",
            sql: "\"Amount\" > 0 AND \"BalanceAfter\" >= 0 AND ((\"Type\" = 'TopUp' AND \"Amount\" <= 1000 AND \"BalanceAfter\" <= 10000 AND \"RefundId\" IS NULL AND \"HeartRefillPurchaseId\" IS NULL AND \"PaymentId\" IS NULL) OR (\"Type\" = 'Debit' AND \"Amount\" <= 10000 AND \"RefundId\" IS NULL AND ((\"HeartRefillPurchaseId\" IS NOT NULL AND \"PaymentId\" IS NULL) OR (\"PaymentId\" IS NOT NULL AND \"HeartRefillPurchaseId\" IS NULL))) OR (\"Type\" = 'Refund' AND \"RefundId\" IS NOT NULL AND \"HeartRefillPurchaseId\" IS NULL AND \"PaymentId\" IS NULL))");

        migrationBuilder.CreateIndex(
            name: "IX_Refunds_CreatedBy_IdempotencyKey",
            table: "Refunds",
            columns: new[] { "CreatedBy", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Refunds_PaymentId",
            table: "Refunds",
            column: "PaymentId");

        migrationBuilder.AddForeignKey(
            name: "FK_CardTransactions_Refunds_RefundId",
            table: "CardTransactions",
            column: "RefundId",
            principalTable: "Refunds",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SubscriptionEvents_Refunds_RefundId",
            table: "SubscriptionEvents",
            column: "RefundId",
            principalTable: "Refunds",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.Sql("""
            CREATE FUNCTION lexigo_lock_refund_payment() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
                PERFORM 1 FROM "PaymentAttempts" WHERE "Id" = NEW."PaymentId" FOR NO KEY UPDATE;
                RETURN NEW;
            END; $body$;
            CREATE TRIGGER lock_refund_payment BEFORE INSERT ON "Refunds" FOR EACH ROW EXECUTE FUNCTION lexigo_lock_refund_payment();
            CREATE TRIGGER protect_refund_evidence BEFORE UPDATE OR DELETE ON "Refunds" FOR EACH ROW EXECUTE FUNCTION lexigo_protect_subscription_evidence();
            CREATE FUNCTION lexigo_check_refund_artifacts() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE refund_id uuid; evidence record; total_refunded numeric; paid_start timestamptz; period record;
            BEGIN
                IF TG_TABLE_NAME = 'Refunds' THEN refund_id := NEW."Id"; ELSE refund_id := NEW."RefundId"; END IF;
                IF refund_id IS NULL THEN RETURN NULL; END IF;
                SELECT r.*,p."Status" AS paid_status,p."Amount" AS paid_amount,p."UserId" AS owner_id,p."TestCardId" AS card_id,
                    p."CreatedAt" AS paid_at,o."SubscriptionId" AS subscription_id,o."OrderType" AS order_type,o."RenewalPeriodKey" AS period_key
                    INTO evidence FROM "Refunds" r JOIN "PaymentAttempts" p ON p."Id" = r."PaymentId"
                    JOIN "BillingOrders" o ON o."Id" = p."OrderId" WHERE r."Id" = refund_id;
                IF NOT FOUND THEN RAISE EXCEPTION 'Refund evidence is missing.' USING ERRCODE = '23514'; END IF;
                SELECT COALESCE(SUM("Amount"),0) INTO total_refunded FROM "Refunds" WHERE "PaymentId" = evidence."PaymentId";
                IF evidence.paid_status <> 'Succeeded' OR evidence.subscription_id IS NULL OR total_refunded > evidence.paid_amount
                    OR evidence."CreatedBy" = evidence.owner_id OR NOT EXISTS (
                    SELECT 1 FROM "Users" u WHERE u."Id" = evidence."CreatedBy" AND u."Status" = 'Active' AND u."EmailConfirmed" AND u."TwoFactorEnabled"
                    AND (EXISTS (SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" WHERE ur."UserId" = u."Id" AND r."Name" = 'SuperAdmin')
                      OR (EXISTS (SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" WHERE ur."UserId" = u."Id" AND r."Name" = 'Admin')
                        AND EXISTS (SELECT 1 FROM "AspNetUserClaims" c WHERE c."UserId" = u."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'BillingManage')))) THEN
                    RAISE EXCEPTION 'Refund needs an authorized actor, a successful payment and an available paid amount.' USING ERRCODE = '23514';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM "CardTransactions" t WHERE t."RefundId" = refund_id AND t."TestCardId" = evidence.card_id
                    AND t."Type" = 'Refund' AND t."Amount" = evidence."Amount" AND t."PaymentId" IS NULL AND t."HeartRefillPurchaseId" IS NULL)
                  OR NOT EXISTS (SELECT 1 FROM "SubscriptionEvents" e WHERE e."RefundId" = refund_id AND e."SubscriptionId" = evidence.subscription_id
                    AND e."RelatedPaymentId" = evidence."PaymentId" AND e."ActorId" = evidence."CreatedBy" AND e."OccurredAt" = evidence."CompletedAt")
                  OR NOT EXISTS (SELECT 1 FROM "AuditLogs" a WHERE a."EntityType" = 'Refund' AND a."EntityId" = refund_id::text
                    AND a."Action" = 'Billing.Refund.Succeeded' AND a."ActorId" = evidence."CreatedBy" AND a."Reason" = evidence."Reason")
                  OR NOT EXISTS (SELECT 1 FROM "Notifications" n WHERE n."UserId" = evidence.owner_id AND n."Type" = 'PaymentRefunded' AND n."CreatedAt" = evidence."CompletedAt") THEN
                    RAISE EXCEPTION 'Refund credit, subscription event, audit and account notice must commit together.' USING ERRCODE = '23514';
                END IF;
                paid_start := CASE WHEN evidence.order_type = 'Purchase' THEN evidence.paid_at ELSE evidence.period_key::timestamptz END;
                SELECT "Status","CurrentPeriodStart","CurrentPeriodEnd","CancelAtPeriodEnd" INTO period FROM "Subscriptions" WHERE "Id" = evidence.subscription_id;
                IF total_refunded = evidence.paid_amount AND period."CurrentPeriodStart" = paid_start
                    AND (period."Status" <> 'Refunded' OR NOT period."CancelAtPeriodEnd"
                      OR EXISTS (SELECT 1 FROM "BillingPreferences" b WHERE b."UserId" = evidence.owner_id AND b."AutoRenew" AND b."RenewalConsentSubscriptionId" = evidence.subscription_id)) THEN
                    RAISE EXCEPTION 'A fully refunded current period cannot keep subscription or renewal access.' USING ERRCODE = '23514';
                END IF;
                IF evidence."RevokedPremium" AND (total_refunded <> evidence.paid_amount OR period."Status" <> 'Refunded'
                    OR period."CurrentPeriodStart" <> paid_start OR evidence."CompletedAt" < period."CurrentPeriodStart" OR evidence."CompletedAt" >= period."CurrentPeriodEnd") THEN
                    RAISE EXCEPTION 'Refund cannot claim to revoke another paid period.' USING ERRCODE = '23514';
                END IF;
                RETURN NULL;
            END; $body$;
            CREATE CONSTRAINT TRIGGER check_refund AFTER INSERT ON "Refunds" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_refund_artifacts();
            CREATE CONSTRAINT TRIGGER check_refund_credit AFTER INSERT ON "CardTransactions" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_refund_artifacts();
            CREATE CONSTRAINT TRIGGER check_refund_event AFTER INSERT ON "SubscriptionEvents" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_refund_artifacts();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $body$ BEGIN
                IF EXISTS (SELECT 1 FROM "Refunds") OR EXISTS (SELECT 1 FROM "TestCards" WHERE "Balance" > 10000) THEN
                    RAISE EXCEPTION 'Preserve refund evidence and balances; recover with a verified backup instead.';
                END IF;
            END; $body$;
            DROP TRIGGER check_refund ON "Refunds";
            DROP TRIGGER check_refund_credit ON "CardTransactions";
            DROP TRIGGER check_refund_event ON "SubscriptionEvents";
            DROP TRIGGER protect_refund_evidence ON "Refunds";
            DROP TRIGGER lock_refund_payment ON "Refunds";
            DROP FUNCTION lexigo_check_refund_artifacts();
            DROP FUNCTION lexigo_lock_refund_payment();
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_CardTransactions_Refunds_RefundId",
            table: "CardTransactions");

        migrationBuilder.DropForeignKey(
            name: "FK_SubscriptionEvents_Refunds_RefundId",
            table: "SubscriptionEvents");

        migrationBuilder.DropTable(
            name: "Refunds");

        migrationBuilder.DropCheckConstraint(
            name: "CK_TestCard_Values",
            table: "TestCards");

        migrationBuilder.DropIndex(
            name: "IX_SubscriptionEvents_RefundId",
            table: "SubscriptionEvents");

        migrationBuilder.DropCheckConstraint(
            name: "CK_SubscriptionEvent_Refund",
            table: "SubscriptionEvents");

        migrationBuilder.DropIndex(
            name: "IX_CardTransactions_RefundId",
            table: "CardTransactions");

        migrationBuilder.DropCheckConstraint(
            name: "CK_CardTransaction_Values",
            table: "CardTransactions");

        migrationBuilder.DropColumn(
            name: "RefundId",
            table: "SubscriptionEvents");

        migrationBuilder.AddCheckConstraint(
            name: "CK_TestCard_Values",
            table: "TestCards",
            sql: "\"Balance\" BETWEEN 0 AND 10000 AND \"Version\" > 0 AND \"Status\" IN ('Active','Blocked') AND \"Currency\" IN ('GEL','USD','EUR') AND \"DisplayLastFour\" ~ '^[0-9]{4}$'");

        migrationBuilder.AddCheckConstraint(
            name: "CK_CardTransaction_Values",
            table: "CardTransactions",
            sql: "\"Amount\" > 0 AND \"BalanceAfter\" BETWEEN 0 AND 10000 AND \"RefundId\" IS NULL AND ((\"Type\" = 'TopUp' AND \"Amount\" <= 1000 AND \"HeartRefillPurchaseId\" IS NULL AND \"PaymentId\" IS NULL) OR (\"Type\" = 'Debit' AND \"Amount\" <= 10000 AND ((\"HeartRefillPurchaseId\" IS NOT NULL AND \"PaymentId\" IS NULL) OR (\"PaymentId\" IS NOT NULL AND \"HeartRefillPurchaseId\" IS NULL))))");
    }
}
