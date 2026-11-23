# Simulated test-card wallet

## Available now

The protected /billing/cards page supports creating a named test card, GEL/USD/EUR currency, a display-only last four digits, zero initial balance, list/status, rename, block/unblock, explicit default selection, bounded simulated top-ups and paged transaction history. Account navigation links to the wallet. Styling uses the existing Fraunces/DM Sans, paper/ink/sage/gold palette and responsive card layout. The page clearly states that no real bank card is issued and balances have no monetary value. It has no real PAN/CVV input, paid provider, bank connection or real transfer.

Initial operating limits:10cards/account;1–1000simulated units/top-up with up to2decimal places;10000maximum balance/card. Currency is immutable after creation. Card creation always starts at0; name/currency are the only user-supplied card details. A blocked card cannot be topped up or newly selected as default. Blocking an existing default preserves the preference; future renewal must reject its blocked status. Default selection alone does not opt into automatic renewal.

## Server and data boundaries

The six implemented card operations are GET/POST /api/v1/me/test-cards, PATCH /api/v1/me/test-cards/{id}, and POST block/unblock/top-ups subroutes. All use authenticated subject ownership, bounded input, existing learning rate limits and private/no-store responses. POST/PATCH mutations require an IdempotencyKey. The service rechecks active confirmed account/session under the same user-first PostgreSQL lock order as account and learning writes. Unmapped JSON fields (including balance/real-card data/owner overrides) are rejected. Foreign IDs return404 without exposing another account's details.

All mutations, card version changes, durable BillingOperation replay evidence and AuditLog entries commit atomically. Top-ups also append CardTransaction with amount, balanceAfter, currency-derived display, server reference and timestamp. Amounts/balances are decimal(18,2), never floating-point business arithmetic. A unique account/operation/request-key index prevents duplicate application; the same key with different normalized details conflicts. Retries return the original response, and the UI reloads authoritative current card/history state. Failed/invalid top-ups do not add funds. A deliberately failed ledger insert is tested to roll back balance, audit and replay writes, after which the same request can safely retry.

SQL checks constrain card status/currency/display digits/balance/version and top-up values. Composite user/default-card FK prevents cross-owner default selection. Restrictive FKs retain historical references. CardTransaction UPDATE/DELETE is rejected by an append-only PostgreSQL trigger; corrections must eventually use explicit offsetting debit/refund records, not history rewriting. PaymentId/RefundId columns currently must be NULL; payment/refund FKs and other ledger types require the subsequent billing migrations. BillingPreference.AutoRenew is mapped but no renewal/subscription behavior is claimed.

History endpoints currently show owned successful top-ups only: optional card/type/date range, deterministic descending timestamp/ID order,20rows/page. Date bounds are inclusive from/exclusive to, normalized to UTC. The UI filters by card and paginates. Payment/debit/refund/failed-attempt history and broader status filters remain required.

## Frontend recovery and privacy

TestCardsApi handles HTTP; the focused TestCardsStore owns card/history/loading/working/error/pending state. A network/5xx/429 uncertain mutation retains the identical method/path/body/request key, blocks another mutation and offers a safe same-request retry. A known4xx rejection releases the operation. A successful write followed by a failed reload is shown as saved and cannot be accidentally resubmitted. Account changes clear cards/history/pending references/form inputs and reject late replies. Concurrent history loads ignore older results. Server responses, rather than local arithmetic, establish balances/status.

## Setup and evidence

AddTestCardWallet is additive migration20261028200848; database/schema.sql includes its idempotent schema and ledger trigger. Existing users need no backfill or default financial records. It creates no real account/card, role grant or pre-funded wallet.

TestCardTests runs against real isolated PostgreSQL and HTTP, covering zero-balance ownership/masking, unknown fields, key conflicts, concurrent top-ups/replay/audit counts, invalid amounts/blocked cards/caps, default/rename/status, foreign-account denial, append-only SQL/negative balance/default ownership constraints, stable paged history/card limits, and deliberate rollback failure. Eight focused cases pass. The full backend suite passed297tests before the final eighth rollback case was added; that added case and all seven wallet cases pass in the final focused run. Angular full151tests/38files and production build pass, with wallet-specific replay/reload/account/form/escaping/currency/input cases; final account-selection cleanup is rechecked separately.

## Still required

This is the wallet foundation of the complete specified billing system. SubscriptionPlan/PlanPrice/Feature/PlanFeature, server-priced orders, payments/success/decline/technical-failure outcomes, receipts, subscriptions/events/renewals/scheduled changes, entitlements, authorized refunds/audit administration and simulated heart-refill checkout remain required. No premium access or extra hearts are granted by adding a test balance. Real-device visual checks and remaining production/security/recovery gates remain open. The original70models/122API baseline remains intact; BillingOperation is a separately tracked supporting model. No live payment integration or spending is authorized.

Final runtime: API readiness, wallet4200/billing/cards and phone4201 return200; database/mail/storage healthy. Global/card history OpenAPI parameters verified separately. These checks do not establish authenticated physical-device visual verification.

## One-time heart-debit extension — October29

The wallet now also displays successful heart-refill Debits as negative amounts, with their own same-card immutable purchase reference. Top-ups remain positive. Supporting HeartRefillPurchase, server-priced5GEL checkout, persistent declines and request-status recovery are described in HEART_CHECKOUT.md. Original subscription payment/refund/failed-attempt global history remains required; full heart-attempt history is provided separately at checkout. Prior top-up-only descriptions/evidence above refer to the wallet's initial milestone.
