using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

public partial class AllowDepartingStaffNoteErasure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION lexigo_protect_refund_evidence() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
                IF TG_OP = 'UPDATE' THEN
                    IF NEW."Reason" = 'Personal refund note erased after account deletion.'
            AND (to_jsonb(NEW) - 'Reason') = (to_jsonb(OLD) - 'Reason')
            AND (EXISTS (SELECT 1 FROM "Users" account
                WHERE account."Id" = OLD."CreatedBy" AND account."Status" = 'Deleted')
                OR EXISTS (SELECT 1 FROM "PaymentAttempts" payment
                    JOIN "Users" account ON account."Id" = payment."UserId"
                    WHERE payment."Id" = OLD."PaymentId" AND account."Status" = 'Deleted'))
                    THEN RETURN NEW; END IF;
                END IF;
                RAISE EXCEPTION 'Refund evidence is immutable except note erasure for a deleted buyer or author.'
                    USING ERRCODE = '23514';
            END; $body$;
            CREATE OR REPLACE FUNCTION lexigo_check_refund_artifacts() RETURNS trigger LANGUAGE plpgsql AS $body$
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
                    OR evidence."CreatedBy" = evidence.owner_id OR (NOT EXISTS (
                    SELECT 1 FROM "Users" u WHERE u."Id" = evidence."CreatedBy" AND u."Status" = 'Active' AND u."EmailConfirmed" AND u."TwoFactorEnabled"
                    AND (EXISTS (SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" WHERE ur."UserId" = u."Id" AND r."Name" = 'SuperAdmin')
                      OR (EXISTS (SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" WHERE ur."UserId" = u."Id" AND r."Name" = 'Admin')
                        AND EXISTS (SELECT 1 FROM "AspNetUserClaims" c WHERE c."UserId" = u."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = 'BillingManage'))))
                AND NOT (TG_TABLE_NAME = 'Refunds' AND TG_OP = 'UPDATE'
                AND evidence."Reason" = 'Personal refund note erased after account deletion.'
                AND (to_jsonb(NEW) - 'Reason') = (to_jsonb(OLD) - 'Reason')
                AND EXISTS (SELECT 1 FROM "Users" account
                    WHERE account."Id" = evidence."CreatedBy" AND account."Status" = 'Deleted'))) THEN
                    RAISE EXCEPTION 'Refund needs an authorized actor, a successful payment and an available paid amount.' USING ERRCODE = '23514';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM "CardTransactions" t WHERE t."RefundId" = refund_id AND t."TestCardId" = evidence.card_id
                    AND t."Type" = 'Refund' AND t."Amount" = evidence."Amount" AND t."PaymentId" IS NULL AND t."HeartRefillPurchaseId" IS NULL)
                  OR NOT EXISTS (SELECT 1 FROM "SubscriptionEvents" e WHERE e."RefundId" = refund_id AND e."SubscriptionId" = evidence.subscription_id
                    AND e."RelatedPaymentId" = evidence."PaymentId" AND e."ActorId" = evidence."CreatedBy" AND e."OccurredAt" = evidence."CompletedAt")
                  OR NOT EXISTS (SELECT 1 FROM "AuditLogs" a WHERE a."EntityType" = 'Refund' AND a."EntityId" = refund_id::text
                    AND a."Action" = 'Billing.Refund.Succeeded' AND a."ActorId" = evidence."CreatedBy" AND a."Reason" = evidence."Reason")
                  OR (NOT EXISTS (SELECT 1 FROM "Users" account WHERE account."Id" = evidence.owner_id AND account."Status" = 'Deleted')
                    AND NOT EXISTS (SELECT 1 FROM "Notifications" n WHERE n."UserId" = evidence.owner_id AND n."Type" = 'PaymentRefunded' AND n."CreatedAt" = evidence."CompletedAt")) THEN
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
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $body$
            BEGIN
                IF EXISTS (SELECT 1 FROM "Refunds" refund JOIN "Users" account ON account."Id" = refund."CreatedBy"
                    WHERE account."Status" = 'Deleted') THEN
                    RAISE EXCEPTION 'Retain deleted-staff refund privacy support while that history exists.';
                END IF;
            END; $body$;
            CREATE OR REPLACE FUNCTION lexigo_protect_refund_evidence() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
                IF TG_OP = 'UPDATE' THEN
                    IF NEW."Reason" = 'Personal refund note erased after account deletion.'
            AND (to_jsonb(NEW) - 'Reason') = (to_jsonb(OLD) - 'Reason')
            AND EXISTS (SELECT 1 FROM "PaymentAttempts" payment
                JOIN "Users" account ON account."Id" = payment."UserId"
                WHERE payment."Id" = OLD."PaymentId" AND account."Status" = 'Deleted')
                    THEN RETURN NEW; END IF;
                END IF;
                RAISE EXCEPTION 'Refund evidence is immutable except note erasure for a deleted account.'
                    USING ERRCODE = '23514';
            END; $body$;
            CREATE OR REPLACE FUNCTION lexigo_check_refund_artifacts() RETURNS trigger LANGUAGE plpgsql AS $body$
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
                  OR (NOT EXISTS (SELECT 1 FROM "Users" account WHERE account."Id" = evidence.owner_id AND account."Status" = 'Deleted')
                    AND NOT EXISTS (SELECT 1 FROM "Notifications" n WHERE n."UserId" = evidence.owner_id AND n."Type" = 'PaymentRefunded' AND n."CreatedAt" = evidence."CompletedAt")) THEN
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
            """);
    }
}
