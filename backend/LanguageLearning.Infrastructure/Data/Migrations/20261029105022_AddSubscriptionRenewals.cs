using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSubscriptionRenewals : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RenewalConsentAt",
            table: "BillingPreferences",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RenewalConsentSubscriptionId",
            table: "BillingPreferences",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "BillingJobExecutions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                PeriodKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                FailureCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BillingJobExecutions", x => x.Id);
                table.CheckConstraint("CK_BillingJobExecution_Result", "\"NextAttemptAt\" IS NULL AND ((\"Status\" = 'Succeeded' AND \"FailureCode\" IS NULL) OR (\"Status\" IN ('Failed','Skipped') AND \"FailureCode\" IS NOT NULL))");
                table.ForeignKey(
                    name: "FK_BillingJobExecutions_Subscriptions_SubscriptionId",
                    column: x => x.SubscriptionId,
                    principalTable: "Subscriptions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
                table.CheckConstraint("CK_Notification_ReadTime", "\"ReadAt\" IS NULL OR \"ReadAt\" >= \"CreatedAt\"");
                table.ForeignKey(
                    name: "FK_Notifications_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BillingPreferences_UserId_RenewalConsentSubscriptionId",
            table: "BillingPreferences",
            columns: new[] { "UserId", "RenewalConsentSubscriptionId" });

        migrationBuilder.AddCheckConstraint(
            name: "CK_BillingPreference_Consent",
            table: "BillingPreferences",
            sql: "(\"RenewalConsentSubscriptionId\" IS NULL AND \"RenewalConsentAt\" IS NULL) OR (\"AutoRenew\" AND \"RenewalConsentSubscriptionId\" IS NOT NULL AND \"RenewalConsentAt\" IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_BillingJobExecutions_SubscriptionId_PeriodKey",
            table: "BillingJobExecutions",
            columns: new[] { "SubscriptionId", "PeriodKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_UserId_CreatedAt_Id",
            table: "Notifications",
            columns: new[] { "UserId", "CreatedAt", "Id" });

        migrationBuilder.AddForeignKey(
            name: "FK_BillingPreferences_Subscriptions_UserId_RenewalConsentSubsc~",
            table: "BillingPreferences",
            columns: new[] { "UserId", "RenewalConsentSubscriptionId" },
            principalTable: "Subscriptions",
            principalColumns: new[] { "UserId", "Id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION lexigo_check_subscription_payment() RETURNS trigger LANGUAGE plpgsql AS $body$
                            DECLARE payment_id uuid; evidence record;
                            BEGIN
                                IF TG_TABLE_NAME = 'BillingOrders' THEN
                                    SELECT "Id" INTO payment_id FROM "PaymentAttempts" WHERE "OrderId" = NEW."Id" AND "Status" = 'Succeeded';
                                    IF NEW."Status" = 'Paid' AND payment_id IS NULL THEN
                                        RAISE EXCEPTION 'Paid order requires a successful payment.' USING ERRCODE = '23514';
                                    END IF;
                                ELSIF TG_TABLE_NAME = 'PaymentAttempts' THEN payment_id := NEW."Id";
                                ELSIF TG_TABLE_NAME = 'SubscriptionEvents' THEN payment_id := NEW."RelatedPaymentId";
                                ELSE payment_id := NEW."PaymentId"; END IF;
                                IF payment_id IS NULL THEN RETURN NULL; END IF;
                                SELECT p."Id",p."Status",p."Amount",p."Currency",p."UserId",p."TestCardId",o."Status" AS order_status,
                                    o."AmountSnapshot",o."Currency" AS order_currency,o."SubscriptionId",o."PlanPriceId",o."OrderType"
                                    INTO evidence FROM "PaymentAttempts" p JOIN "BillingOrders" o ON o."Id" = p."OrderId" WHERE p."Id" = payment_id;
                                IF evidence."Amount" <> evidence."AmountSnapshot" OR evidence."Currency" <> evidence.order_currency THEN
                                    RAISE EXCEPTION 'Payment must match its saved quote.' USING ERRCODE = '23514';
                                END IF;
                                IF evidence."Status" = 'Succeeded' THEN
                                    IF evidence.order_status <> 'Paid' OR NOT EXISTS (
                                        SELECT 1 FROM "Subscriptions" s WHERE s."Id" = evidence."SubscriptionId" AND s."UserId" = evidence."UserId"
                                            AND s."PlanId" = (SELECT "PlanId" FROM "PlanPrices" WHERE "Id" = evidence."PlanPriceId")
                                    ) OR NOT EXISTS (
                                        SELECT 1 FROM "CardTransactions" t JOIN "TestCards" c ON c."Id" = t."TestCardId"
                                        WHERE t."PaymentId" = payment_id AND t."TestCardId" = evidence."TestCardId"
                                            AND t."Type" = 'Debit' AND t."Amount" = evidence."Amount" AND c."Currency" = evidence."Currency"
                                    ) OR NOT EXISTS (SELECT 1 FROM "PaymentReceipts" r WHERE r."PaymentId" = payment_id)
                                    OR NOT EXISTS (SELECT 1 FROM "SubscriptionEvents" e WHERE e."RelatedPaymentId" = payment_id
                                        AND e."SubscriptionId" = evidence."SubscriptionId" AND e."EventType" = CASE WHEN evidence."OrderType" = 'Renewal' THEN 'Renewed' ELSE 'Activated' END) THEN
                                        RAISE EXCEPTION 'Successful payment needs its debit, subscription, event and receipt.' USING ERRCODE = '23514';
                                    END IF;
                                ELSIF EXISTS (SELECT 1 FROM "CardTransactions" t WHERE t."PaymentId" = payment_id)
                                    OR EXISTS (SELECT 1 FROM "PaymentReceipts" r WHERE r."PaymentId" = payment_id)
                                    OR EXISTS (SELECT 1 FROM "SubscriptionEvents" e WHERE e."RelatedPaymentId" = payment_id AND e."EventType" IN ('Activated','Renewed')) THEN
                                    RAISE EXCEPTION 'Unsuccessful payment cannot debit or activate a subscription.' USING ERRCODE = '23514';
                                END IF;
                                RETURN NULL;
                            END; $body$;
            
            CREATE FUNCTION lexigo_check_renewal_job() RETURNS trigger LANGUAGE plpgsql AS $body$
            DECLARE target_id uuid; target_period text; result text; order_status text; payment_status text;
            BEGIN
                IF TG_TABLE_NAME = 'BillingOrders' THEN
                    IF NEW."OrderType" <> 'Renewal' THEN RETURN NULL; END IF;
                    target_id := NEW."SubscriptionId"; target_period := NEW."RenewalPeriodKey";
                ELSE target_id := NEW."SubscriptionId"; target_period := NEW."PeriodKey"; END IF;
                SELECT "Status" INTO result FROM "BillingJobExecutions" WHERE "SubscriptionId" = target_id AND "PeriodKey" = target_period;
                SELECT o."Status",p."Status" INTO order_status,payment_status FROM "BillingOrders" o
                    LEFT JOIN "PaymentAttempts" p ON p."OrderId" = o."Id"
                    WHERE o."SubscriptionId" = target_id AND o."RenewalPeriodKey" = target_period AND o."OrderType" = 'Renewal';
                IF result IS NULL OR (result = 'Succeeded' AND (order_status IS DISTINCT FROM 'Paid' OR payment_status IS DISTINCT FROM 'Succeeded'))
                    OR (result = 'Failed' AND (order_status IS DISTINCT FROM 'Expired' OR payment_status IS DISTINCT FROM 'Failed'))
                    OR (result = 'Skipped' AND order_status IS NOT NULL) THEN
                    RAISE EXCEPTION 'Renewal job and order/payment result must agree.' USING ERRCODE = '23514';
                END IF;
                RETURN NULL;
            END; $body$;
            CREATE CONSTRAINT TRIGGER check_renewal_job AFTER INSERT ON "BillingJobExecutions"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_renewal_job();
            CREATE CONSTRAINT TRIGGER check_renewal_order AFTER INSERT OR UPDATE ON "BillingOrders"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_renewal_job();
            CREATE TRIGGER protect_renewal_job BEFORE UPDATE OR DELETE ON "BillingJobExecutions"
                FOR EACH ROW EXECUTE FUNCTION lexigo_protect_subscription_evidence();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $body$ BEGIN
                IF EXISTS (SELECT 1 FROM "BillingJobExecutions") OR EXISTS (SELECT 1 FROM "Notifications") THEN
                    RAISE EXCEPTION 'Preserve renewal and notification evidence; restore from a verified backup instead.';
                END IF;
            END; $body$;
            DROP TRIGGER check_renewal_order ON "BillingOrders";
            DROP TRIGGER check_renewal_job ON "BillingJobExecutions";
            DROP TRIGGER protect_renewal_job ON "BillingJobExecutions";
            DROP FUNCTION lexigo_check_renewal_job();
            CREATE OR REPLACE FUNCTION lexigo_check_subscription_payment() RETURNS trigger LANGUAGE plpgsql AS $body$
                            DECLARE payment_id uuid; evidence record;
                            BEGIN
                                IF TG_TABLE_NAME = 'BillingOrders' THEN
                                    SELECT "Id" INTO payment_id FROM "PaymentAttempts" WHERE "OrderId" = NEW."Id" AND "Status" = 'Succeeded';
                                    IF NEW."Status" = 'Paid' AND payment_id IS NULL THEN
                                        RAISE EXCEPTION 'Paid order requires a successful payment.' USING ERRCODE = '23514';
                                    END IF;
                                ELSIF TG_TABLE_NAME = 'PaymentAttempts' THEN payment_id := NEW."Id";
                                ELSIF TG_TABLE_NAME = 'SubscriptionEvents' THEN payment_id := NEW."RelatedPaymentId";
                                ELSE payment_id := NEW."PaymentId"; END IF;
                                IF payment_id IS NULL THEN RETURN NULL; END IF;
                                SELECT p."Id",p."Status",p."Amount",p."Currency",p."UserId",p."TestCardId",o."Status" AS order_status,
                                    o."AmountSnapshot",o."Currency" AS order_currency,o."SubscriptionId",o."PlanPriceId"
                                    INTO evidence FROM "PaymentAttempts" p JOIN "BillingOrders" o ON o."Id" = p."OrderId" WHERE p."Id" = payment_id;
                                IF evidence."Amount" <> evidence."AmountSnapshot" OR evidence."Currency" <> evidence.order_currency THEN
                                    RAISE EXCEPTION 'Payment must match its saved quote.' USING ERRCODE = '23514';
                                END IF;
                                IF evidence."Status" = 'Succeeded' THEN
                                    IF evidence.order_status <> 'Paid' OR NOT EXISTS (
                                        SELECT 1 FROM "Subscriptions" s WHERE s."Id" = evidence."SubscriptionId" AND s."UserId" = evidence."UserId"
                                            AND s."CurrentPlanPriceId" = evidence."PlanPriceId"
                                    ) OR NOT EXISTS (
                                        SELECT 1 FROM "CardTransactions" t JOIN "TestCards" c ON c."Id" = t."TestCardId"
                                        WHERE t."PaymentId" = payment_id AND t."TestCardId" = evidence."TestCardId"
                                            AND t."Type" = 'Debit' AND t."Amount" = evidence."Amount" AND c."Currency" = evidence."Currency"
                                    ) OR NOT EXISTS (SELECT 1 FROM "PaymentReceipts" r WHERE r."PaymentId" = payment_id)
                                    OR NOT EXISTS (SELECT 1 FROM "SubscriptionEvents" e WHERE e."RelatedPaymentId" = payment_id
                                        AND e."SubscriptionId" = evidence."SubscriptionId" AND e."EventType" = 'Activated') THEN
                                        RAISE EXCEPTION 'Successful payment needs its debit, subscription, event and receipt.' USING ERRCODE = '23514';
                                    END IF;
                                ELSIF EXISTS (SELECT 1 FROM "CardTransactions" t WHERE t."PaymentId" = payment_id)
                                    OR EXISTS (SELECT 1 FROM "PaymentReceipts" r WHERE r."PaymentId" = payment_id)
                                    OR EXISTS (SELECT 1 FROM "SubscriptionEvents" e WHERE e."RelatedPaymentId" = payment_id AND e."EventType" = 'Activated') THEN
                                    RAISE EXCEPTION 'Unsuccessful payment cannot debit or activate a subscription.' USING ERRCODE = '23514';
                                END IF;
                                RETURN NULL;
                            END; $body$;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_BillingPreferences_Subscriptions_UserId_RenewalConsentSubsc~",
            table: "BillingPreferences");

        migrationBuilder.DropTable(
            name: "BillingJobExecutions");

        migrationBuilder.DropTable(
            name: "Notifications");

        migrationBuilder.DropIndex(
            name: "IX_BillingPreferences_UserId_RenewalConsentSubscriptionId",
            table: "BillingPreferences");

        migrationBuilder.DropCheckConstraint(
            name: "CK_BillingPreference_Consent",
            table: "BillingPreferences");

        migrationBuilder.DropColumn(
            name: "RenewalConsentAt",
            table: "BillingPreferences");

        migrationBuilder.DropColumn(
            name: "RenewalConsentSubscriptionId",
            table: "BillingPreferences");
    }
}
