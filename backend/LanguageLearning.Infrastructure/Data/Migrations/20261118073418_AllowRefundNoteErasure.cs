using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowRefundNoteErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReasonFingerprint",
                table: "Refunds",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql("""
                DROP TRIGGER protect_refund_evidence ON "Refunds";
                UPDATE "Refunds"
                SET "ReasonFingerprint" = encode(sha256(convert_to("Reason", 'UTF8')), 'hex');

                CREATE FUNCTION lexigo_protect_refund_evidence() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF NEW."Reason" = 'Personal refund note erased after account deletion.'
                            AND (to_jsonb(NEW) - 'Reason') = (to_jsonb(OLD) - 'Reason')
                            AND EXISTS (
                                SELECT 1 FROM "PaymentAttempts" payment
                                JOIN "Users" account ON account."Id" = payment."UserId"
                                WHERE payment."Id" = OLD."PaymentId" AND account."Status" = 'Deleted'
                            )
                        THEN
                            RETURN NEW;
                        END IF;
                    END IF;
                    RAISE EXCEPTION 'Refund evidence is immutable except note erasure for a deleted account.'
                        USING ERRCODE = '23514';
                END; $body$;
                CREATE TRIGGER protect_refund_evidence BEFORE UPDATE OR DELETE ON "Refunds"
                    FOR EACH ROW EXECUTE FUNCTION lexigo_protect_refund_evidence();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Refunds"
                        WHERE "Reason" = 'Personal refund note erased after account deletion.')
                    THEN
                        RAISE EXCEPTION 'Retain refund replay fingerprints after personal notes have been erased.';
                    END IF;
                END; $body$;
                DROP TRIGGER protect_refund_evidence ON "Refunds";
                DROP FUNCTION lexigo_protect_refund_evidence();
                CREATE TRIGGER protect_refund_evidence BEFORE UPDATE OR DELETE ON "Refunds"
                    FOR EACH ROW EXECUTE FUNCTION lexigo_protect_subscription_evidence();
                """);
            migrationBuilder.DropColumn(
                name: "ReasonFingerprint",
                table: "Refunds");
        }
    }
}
