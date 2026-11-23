using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LanguageLearning.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSubscriptionPurchases : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_CardTransaction_Values",
            table: "CardTransactions");

        migrationBuilder.CreateTable(
            name: "Features",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Features", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SubscriptionPlans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PlanFeatures",
            columns: table => new
            {
                PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                FeatureId = table.Column<int>(type: "integer", nullable: false),
                LimitValue = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlanFeatures", x => new { x.PlanId, x.FeatureId });
                table.CheckConstraint("CK_PlanFeature_Limit", "\"LimitValue\" IS NULL OR \"LimitValue\" >= 0");
                table.ForeignKey(
                    name: "FK_PlanFeatures_Features_FeatureId",
                    column: x => x.FeatureId,
                    principalTable: "Features",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PlanFeatures_SubscriptionPlans_PlanId",
                    column: x => x.PlanId,
                    principalTable: "SubscriptionPlans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PlanPrices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                Period = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlanPrices", x => x.Id);
                table.UniqueConstraint("AK_PlanPrices_PlanId_Id", x => new { x.PlanId, x.Id });
                table.CheckConstraint("CK_PlanPrice_Values", "\"Period\" IN ('Monthly','Annual') AND \"Currency\" IN ('GEL','USD','EUR') AND \"Amount\" > 0 AND \"Amount\" <= 10000 AND (\"ValidTo\" IS NULL OR \"ValidTo\" > \"ValidFrom\")");
                table.ForeignKey(
                    name: "FK_PlanPrices_SubscriptionPlans_PlanId",
                    column: x => x.PlanId,
                    principalTable: "SubscriptionPlans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Subscriptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                CurrentPlanPriceId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                CurrentPeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CurrentPeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CancelAtPeriodEnd = table.Column<bool>(type: "boolean", nullable: false),
                ScheduledPlanPriceId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Subscriptions", x => x.Id);
                table.UniqueConstraint("AK_Subscriptions_UserId_Id", x => new { x.UserId, x.Id });
                table.CheckConstraint("CK_Subscription_Values", "\"Status\" IN ('Active','Expired','Refunded') AND \"CurrentPeriodEnd\" > \"CurrentPeriodStart\"");
                table.ForeignKey(
                    name: "FK_Subscriptions_PlanPrices_PlanId_CurrentPlanPriceId",
                    columns: x => new { x.PlanId, x.CurrentPlanPriceId },
                    principalTable: "PlanPrices",
                    principalColumns: new[] { "PlanId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Subscriptions_PlanPrices_ScheduledPlanPriceId",
                    column: x => x.ScheduledPlanPriceId,
                    principalTable: "PlanPrices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                    column: x => x.PlanId,
                    principalTable: "SubscriptionPlans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Subscriptions_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "BillingOrders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                PlanPriceId = table.Column<Guid>(type: "uuid", nullable: false),
                SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                AmountSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                PeriodSnapshot = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                OrderType = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                RenewalPeriodKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BillingOrders", x => x.Id);
                table.UniqueConstraint("AK_BillingOrders_UserId_Id", x => new { x.UserId, x.Id });
                table.CheckConstraint("CK_BillingOrder_Values", "\"AmountSnapshot\" > 0 AND \"AmountSnapshot\" <= 10000 AND \"Currency\" IN ('GEL','USD','EUR') AND \"PeriodSnapshot\" IN ('Monthly','Annual') AND \"Status\" IN ('Created','Paid','Cancelled','Expired') AND \"OrderType\" IN ('Purchase','Renewal') AND \"ExpiresAt\" > \"CreatedAt\" AND (\"Status\" <> 'Paid' OR \"SubscriptionId\" IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_BillingOrders_PlanPrices_PlanPriceId",
                    column: x => x.PlanPriceId,
                    principalTable: "PlanPrices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_BillingOrders_Subscriptions_UserId_SubscriptionId",
                    columns: x => new { x.UserId, x.SubscriptionId },
                    principalTable: "Subscriptions",
                    principalColumns: new[] { "UserId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_BillingOrders_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PaymentAttempts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                TestCardId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                FailureCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                SimulateTechnicalFailure = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentAttempts", x => x.Id);
                table.UniqueConstraint("AK_PaymentAttempts_TestCardId_Id", x => new { x.TestCardId, x.Id });
                table.CheckConstraint("CK_PaymentAttempt_Values", "\"Amount\" > 0 AND \"Currency\" IN ('GEL','USD','EUR') AND ((\"Status\" = 'Pending' AND \"CompletedAt\" IS NULL AND \"FailureCode\" IS NULL) OR (\"Status\" = 'Succeeded' AND \"CompletedAt\" IS NOT NULL AND \"FailureCode\" IS NULL) OR (\"Status\" = 'Failed' AND \"CompletedAt\" IS NOT NULL AND \"FailureCode\" IS NOT NULL AND \"FailureCode\" IN ('CardBlocked','CurrencyMismatch','InsufficientBalance','TechnicalFailure','AlreadySubscribed','OrderExpired')))");
                table.ForeignKey(
                    name: "FK_PaymentAttempts_BillingOrders_UserId_OrderId",
                    columns: x => new { x.UserId, x.OrderId },
                    principalTable: "BillingOrders",
                    principalColumns: new[] { "UserId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentAttempts_TestCards_UserId_TestCardId",
                    columns: x => new { x.UserId, x.TestCardId },
                    principalTable: "TestCards",
                    principalColumns: new[] { "UserId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PaymentReceipts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                ReceiptNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentReceipts", x => x.Id);
                table.ForeignKey(
                    name: "FK_PaymentReceipts_PaymentAttempts_PaymentId",
                    column: x => x.PaymentId,
                    principalTable: "PaymentAttempts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SubscriptionEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                RelatedPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SubscriptionEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_SubscriptionEvents_PaymentAttempts_RelatedPaymentId",
                    column: x => x.RelatedPaymentId,
                    principalTable: "PaymentAttempts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SubscriptionEvents_Subscriptions_SubscriptionId",
                    column: x => x.SubscriptionId,
                    principalTable: "Subscriptions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SubscriptionEvents_Users_ActorId",
                    column: x => x.ActorId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.InsertData(
            table: "Features",
            columns: new[] { "Id", "Code", "Description" },
            values: new object[,]
            {
                { 1, "CoreCourses", "Core courses in all four languages" },
                { 2, "PersonalVocabulary", "Your personal word library" },
                { 3, "BasicPractice", "Core learning and free recovery practice" },
                { 4, "ProgressTracking", "Saved learning progress" },
                { 5, "CoreListening", "Listening inside core lessons" },
                { 6, "UnlimitedHearts", "Learn without losing hearts" },
                { 7, "AdvancedAnalytics", "Advanced learning analytics" },
                { 8, "ExtraDialogues", "Extra situational dialogues" },
                { 9, "AdvancedPractice", "Advanced personal practice" },
                { 10, "PremiumListeningPractice", "Dedicated listening and speaking practice" },
                { 11, "CharacterConversations", "Character conversation calls" }
            });

        migrationBuilder.InsertData(
            table: "SubscriptionPlans",
            columns: new[] { "Id", "Code", "CreatedAt", "IsActive", "Name" },
            values: new object[,]
            {
                { new Guid("70000000-0000-0000-0000-000000000001"), "FREE", new DateTimeOffset(new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Free" },
                { new Guid("70000000-0000-0000-0000-000000000002"), "PREMIUM", new DateTimeOffset(new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Premium" }
            });

        migrationBuilder.InsertData(
            table: "PlanFeatures",
            columns: new[] { "FeatureId", "PlanId", "LimitValue" },
            values: new object[,]
            {
                { 1, new Guid("70000000-0000-0000-0000-000000000001"), null },
                { 2, new Guid("70000000-0000-0000-0000-000000000001"), null },
                { 3, new Guid("70000000-0000-0000-0000-000000000001"), null },
                { 4, new Guid("70000000-0000-0000-0000-000000000001"), null },
                { 5, new Guid("70000000-0000-0000-0000-000000000001"), null },
                { 1, new Guid("70000000-0000-0000-0000-000000000002"), null },
                { 2, new Guid("70000000-0000-0000-0000-000000000002"), null },
                { 3, new Guid("70000000-0000-0000-0000-000000000002"), null },
                { 4, new Guid("70000000-0000-0000-0000-000000000002"), null },
                { 5, new Guid("70000000-0000-0000-0000-000000000002"), null },
                { 6, new Guid("70000000-0000-0000-0000-000000000002"), null },
                { 7, new Guid("70000000-0000-0000-0000-000000000002"), null },
                { 8, new Guid("70000000-0000-0000-0000-000000000002"), null },
                { 9, new Guid("70000000-0000-0000-0000-000000000002"), null },
                { 10, new Guid("70000000-0000-0000-0000-000000000002"), null },
                { 11, new Guid("70000000-0000-0000-0000-000000000002"), null }
            });

        migrationBuilder.InsertData(
            table: "PlanPrices",
            columns: new[] { "Id", "Amount", "Currency", "Period", "PlanId", "ValidFrom", "ValidTo" },
            values: new object[,]
            {
                { new Guid("71000000-0000-0000-0000-000000000001"), 9.99m, "GEL", "Monthly", new Guid("70000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                { new Guid("71000000-0000-0000-0000-000000000002"), 79.99m, "GEL", "Annual", new Guid("70000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 10, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
            });

        migrationBuilder.CreateIndex(
            name: "IX_CardTransactions_PaymentId",
            table: "CardTransactions",
            column: "PaymentId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CardTransactions_TestCardId_PaymentId",
            table: "CardTransactions",
            columns: new[] { "TestCardId", "PaymentId" });

        migrationBuilder.AddCheckConstraint(
            name: "CK_CardTransaction_Values",
            table: "CardTransactions",
            sql: "\"Amount\" > 0 AND \"BalanceAfter\" BETWEEN 0 AND 10000 AND \"RefundId\" IS NULL AND ((\"Type\" = 'TopUp' AND \"Amount\" <= 1000 AND \"HeartRefillPurchaseId\" IS NULL AND \"PaymentId\" IS NULL) OR (\"Type\" = 'Debit' AND \"Amount\" <= 10000 AND ((\"HeartRefillPurchaseId\" IS NOT NULL AND \"PaymentId\" IS NULL) OR (\"PaymentId\" IS NOT NULL AND \"HeartRefillPurchaseId\" IS NULL))))");

        migrationBuilder.CreateIndex(
            name: "IX_BillingOrders_PlanPriceId",
            table: "BillingOrders",
            column: "PlanPriceId");

        migrationBuilder.CreateIndex(
            name: "IX_BillingOrders_SubscriptionId_RenewalPeriodKey",
            table: "BillingOrders",
            columns: new[] { "SubscriptionId", "RenewalPeriodKey" },
            unique: true,
            filter: "\"RenewalPeriodKey\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_BillingOrders_UserId_IdempotencyKey",
            table: "BillingOrders",
            columns: new[] { "UserId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_BillingOrders_UserId_SubscriptionId",
            table: "BillingOrders",
            columns: new[] { "UserId", "SubscriptionId" });

        migrationBuilder.CreateIndex(
            name: "IX_Features_Code",
            table: "Features",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PaymentAttempts_OrderId",
            table: "PaymentAttempts",
            column: "OrderId",
            unique: true,
            filter: "\"Status\" = 'Succeeded'");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentAttempts_UserId_IdempotencyKey",
            table: "PaymentAttempts",
            columns: new[] { "UserId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PaymentAttempts_UserId_OrderId",
            table: "PaymentAttempts",
            columns: new[] { "UserId", "OrderId" });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentAttempts_UserId_TestCardId",
            table: "PaymentAttempts",
            columns: new[] { "UserId", "TestCardId" });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentReceipts_PaymentId",
            table: "PaymentReceipts",
            column: "PaymentId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PaymentReceipts_ReceiptNumber",
            table: "PaymentReceipts",
            column: "ReceiptNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlanFeatures_FeatureId",
            table: "PlanFeatures",
            column: "FeatureId");

        migrationBuilder.CreateIndex(
            name: "IX_PlanPrices_PlanId_Period_ValidFrom",
            table: "PlanPrices",
            columns: new[] { "PlanId", "Period", "ValidFrom" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SubscriptionEvents_ActorId",
            table: "SubscriptionEvents",
            column: "ActorId");

        migrationBuilder.CreateIndex(
            name: "IX_SubscriptionEvents_RelatedPaymentId",
            table: "SubscriptionEvents",
            column: "RelatedPaymentId");

        migrationBuilder.CreateIndex(
            name: "IX_SubscriptionEvents_SubscriptionId_OccurredAt",
            table: "SubscriptionEvents",
            columns: new[] { "SubscriptionId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_SubscriptionPlans_Code",
            table: "SubscriptionPlans",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Subscriptions_PlanId_CurrentPlanPriceId",
            table: "Subscriptions",
            columns: new[] { "PlanId", "CurrentPlanPriceId" });

        migrationBuilder.CreateIndex(
            name: "IX_Subscriptions_ScheduledPlanPriceId",
            table: "Subscriptions",
            column: "ScheduledPlanPriceId");

        migrationBuilder.CreateIndex(
            name: "IX_Subscriptions_UserId",
            table: "Subscriptions",
            column: "UserId",
            unique: true,
            filter: "\"Status\" = 'Active'");

        migrationBuilder.AddForeignKey(
            name: "FK_CardTransactions_PaymentAttempts_TestCardId_PaymentId",
            table: "CardTransactions",
            columns: new[] { "TestCardId", "PaymentId" },
            principalTable: "PaymentAttempts",
            principalColumns: new[] { "TestCardId", "Id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.Sql("""
            CREATE FUNCTION lexigo_protect_subscription_evidence() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Subscription payment evidence is append-only.' USING ERRCODE = '23514'; END;
            $body$;
            CREATE TRIGGER protect_subscription_events BEFORE UPDATE OR DELETE ON "SubscriptionEvents"
                FOR EACH ROW EXECUTE FUNCTION lexigo_protect_subscription_evidence();
            CREATE TRIGGER protect_payment_receipts BEFORE UPDATE OR DELETE ON "PaymentReceipts"
                FOR EACH ROW EXECUTE FUNCTION lexigo_protect_subscription_evidence();
            CREATE TRIGGER protect_payment_attempts BEFORE UPDATE OR DELETE ON "PaymentAttempts"
                FOR EACH ROW EXECUTE FUNCTION lexigo_protect_subscription_evidence();
            CREATE FUNCTION lexigo_protect_price_version() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
                IF ROW(OLD."Id",OLD."PlanId",OLD."Period",OLD."Amount",OLD."Currency",OLD."ValidFrom")
                    IS DISTINCT FROM ROW(NEW."Id",NEW."PlanId",NEW."Period",NEW."Amount",NEW."Currency",NEW."ValidFrom") THEN
                    RAISE EXCEPTION 'Create a new price version instead of editing a quoted price.' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END; $body$;
            CREATE TRIGGER protect_price_version BEFORE UPDATE ON "PlanPrices"
                FOR EACH ROW EXECUTE FUNCTION lexigo_protect_price_version();
            CREATE FUNCTION lexigo_protect_order_quote() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
                IF ROW(OLD."UserId",OLD."PlanPriceId",OLD."AmountSnapshot",OLD."Currency",OLD."PeriodSnapshot",OLD."IdempotencyKey",OLD."CreatedAt",OLD."ExpiresAt",OLD."OrderType",OLD."RenewalPeriodKey")
                    IS DISTINCT FROM ROW(NEW."UserId",NEW."PlanPriceId",NEW."AmountSnapshot",NEW."Currency",NEW."PeriodSnapshot",NEW."IdempotencyKey",NEW."CreatedAt",NEW."ExpiresAt",NEW."OrderType",NEW."RenewalPeriodKey")
                    OR (OLD."Status" <> 'Created' AND OLD."Status" <> NEW."Status")
                    OR (OLD."SubscriptionId" IS DISTINCT FROM NEW."SubscriptionId" AND NOT (OLD."Status" = 'Created' AND NEW."Status" = 'Paid')) THEN
                    RAISE EXCEPTION 'Order quotes and closed states cannot be rewritten.' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END; $body$;
            CREATE TRIGGER protect_order_quote BEFORE UPDATE ON "BillingOrders"
                FOR EACH ROW EXECUTE FUNCTION lexigo_protect_order_quote();
            CREATE FUNCTION lexigo_check_subscription_payment() RETURNS trigger LANGUAGE plpgsql AS $body$
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
            CREATE CONSTRAINT TRIGGER check_subscription_order AFTER INSERT OR UPDATE ON "BillingOrders"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_subscription_payment();
            CREATE CONSTRAINT TRIGGER check_subscription_payment AFTER INSERT ON "PaymentAttempts"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_subscription_payment();
            CREATE CONSTRAINT TRIGGER check_subscription_debit AFTER INSERT ON "CardTransactions"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_subscription_payment();
            CREATE CONSTRAINT TRIGGER check_subscription_receipt AFTER INSERT ON "PaymentReceipts"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_subscription_payment();
            CREATE CONSTRAINT TRIGGER check_subscription_event AFTER INSERT ON "SubscriptionEvents"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lexigo_check_subscription_payment();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $body$ BEGIN
                IF EXISTS (SELECT 1 FROM "BillingOrders") THEN
                    RAISE EXCEPTION 'Preserve billing evidence: use a forward fix or verified restore.';
                END IF;
            END; $body$;
            DROP TRIGGER check_subscription_order ON "BillingOrders";
            DROP TRIGGER check_subscription_payment ON "PaymentAttempts";
            DROP TRIGGER check_subscription_debit ON "CardTransactions";
            DROP TRIGGER check_subscription_receipt ON "PaymentReceipts";
            DROP TRIGGER check_subscription_event ON "SubscriptionEvents";
            DROP FUNCTION lexigo_check_subscription_payment();
            DROP TRIGGER protect_subscription_events ON "SubscriptionEvents";
            DROP TRIGGER protect_payment_receipts ON "PaymentReceipts";
            DROP TRIGGER protect_payment_attempts ON "PaymentAttempts";
            DROP FUNCTION lexigo_protect_subscription_evidence();
            DROP TRIGGER protect_price_version ON "PlanPrices";
            DROP FUNCTION lexigo_protect_price_version();
            DROP TRIGGER protect_order_quote ON "BillingOrders";
            DROP FUNCTION lexigo_protect_order_quote();
            """);
        migrationBuilder.DropForeignKey(
            name: "FK_CardTransactions_PaymentAttempts_TestCardId_PaymentId",
            table: "CardTransactions");

        migrationBuilder.DropTable(
            name: "PaymentReceipts");

        migrationBuilder.DropTable(
            name: "PlanFeatures");

        migrationBuilder.DropTable(
            name: "SubscriptionEvents");

        migrationBuilder.DropTable(
            name: "Features");

        migrationBuilder.DropTable(
            name: "PaymentAttempts");

        migrationBuilder.DropTable(
            name: "BillingOrders");

        migrationBuilder.DropTable(
            name: "Subscriptions");

        migrationBuilder.DropTable(
            name: "PlanPrices");

        migrationBuilder.DropTable(
            name: "SubscriptionPlans");

        migrationBuilder.DropIndex(
            name: "IX_CardTransactions_PaymentId",
            table: "CardTransactions");

        migrationBuilder.DropIndex(
            name: "IX_CardTransactions_TestCardId_PaymentId",
            table: "CardTransactions");

        migrationBuilder.DropCheckConstraint(
            name: "CK_CardTransaction_Values",
            table: "CardTransactions");

        migrationBuilder.AddCheckConstraint(
            name: "CK_CardTransaction_Values",
            table: "CardTransactions",
            sql: "\"Amount\" > 0 AND \"BalanceAfter\" BETWEEN 0 AND 10000 AND \"PaymentId\" IS NULL AND \"RefundId\" IS NULL AND ((\"Type\" = 'TopUp' AND \"Amount\" <= 1000 AND \"HeartRefillPurchaseId\" IS NULL) OR (\"Type\" = 'Debit' AND \"Amount\" <= 1000 AND \"HeartRefillPurchaseId\" IS NOT NULL))");
    }
}
