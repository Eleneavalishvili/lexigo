using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class EnforceSubscriptionEventOwnership : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $body$ BEGIN
                IF EXISTS (SELECT 1 FROM "SubscriptionEvents" e WHERE e."RelatedPaymentId" IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM "PaymentAttempts" p JOIN "BillingOrders" o ON o."Id" = p."OrderId"
                    JOIN "Subscriptions" s ON s."Id" = o."SubscriptionId"
                    WHERE p."Id" = e."RelatedPaymentId" AND p."Status" = 'Succeeded'
                        AND s."Id" = e."SubscriptionId" AND s."UserId" = p."UserId")) THEN
                    RAISE EXCEPTION 'Existing event payment ownership must be repaired before migration.' USING ERRCODE = '23514';
                END IF;
            END; $body$;
            CREATE FUNCTION lexigo_check_subscription_event_subject() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
                IF NEW."RelatedPaymentId" IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM "PaymentAttempts" p JOIN "BillingOrders" o ON o."Id" = p."OrderId"
                    JOIN "Subscriptions" s ON s."Id" = o."SubscriptionId"
                    WHERE p."Id" = NEW."RelatedPaymentId" AND p."Status" = 'Succeeded'
                        AND s."Id" = NEW."SubscriptionId" AND s."UserId" = p."UserId") THEN
                    RAISE EXCEPTION 'A subscription event must reference its own successful payment.' USING ERRCODE = '23514';
                END IF;
                RETURN NULL;
            END; $body$;
            CREATE CONSTRAINT TRIGGER check_subscription_event_subject AFTER INSERT ON "SubscriptionEvents"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_subscription_event_subject();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER check_subscription_event_subject ON "SubscriptionEvents";
            DROP FUNCTION lexigo_check_subscription_event_subject();
            """);
    }
}
