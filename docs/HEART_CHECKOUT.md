# Simulated one-time heart checkout

## Approved product and current interaction

The user explicitly selected5simulated GEL for a full five-heart refill on2026-10-29. /billing/hearts shows the current server offer, current shared hearts, owned test-card balances/status, explicit confirmation, saved result and paged success/decline history. Links are available from the test wallet and unfinished regular lessons with fewer than five hearts. A valid lesson UUID preserves a safe return to /learn/session/{id}; arbitrary return URLs are rejected. Original retro palette/fonts/characters remain.

The purchase restores the current account-wide balance to5, rather than adding5beyond the maximum. The price is5GEL whether1or5hearts are missing, clearly disclosed before confirmation. There is no XP, subscription or Premium activation. Free practice and existing one-hour full recovery remain available. The server checks free recovery at purchase time: if hearts have already recovered, the attempt is recorded as HeartsAlreadyFull with0charged. No real money, real card credentials, live provider, external account or email is involved.

## HTTP and trusted decisions

Authenticated private/no-store supporting routes:

- GET /api/v1/me/hearts/refill-offer: full-hearts-gel-v1,5.00GEL, maximum5and current server-derived HeartStatus.
- POST /api/v1/me/hearts/purchases: only testCardId, offerCode and idempotencyKey.
- GET /api/v1/me/hearts/purchases/by-request/{key}: owned durable result or404.
- GET /api/v1/me/hearts/purchases?page=1: owned history,20rows/page with stable descending timestamp/ID order.

The versioned server offer supplies price/currency; the client cannot post a price, owner, heart count or successful-payment flag. Different/retired offer codes require a fresh review. Same key with different card/offer conflicts; historical matching requests replay their saved result before checking today's hearts/price. Foreign cards/results return404. Active confirmed account/session/security stamp are rechecked under the shared billing user-first row lock. Existing JWT authorization, request-size and learning rate limits apply.

## Atomic finances and hearts

HeartRefillPurchase is supporting evidence for the additional one-time heart product, not a fabricated completion of original subscription BillingOrder/PaymentAttempt models. It records owner/card/request key, immutable server offer/amount/currency, success/failure, reference, before/after heart counts and creation time. ChargedAmount is0for declines. Timestamp precision is normalized before persistence so original/replayed typed results agree with PostgreSQL precision.

One transaction covers purchase, optional test-card debit/version, shared heart refill/deadline clearing and audit. Concurrent same/different requests serialize with all wallet/learning/account writes. Only one successful refill can occur while hearts are full; retry does not charge again or restore hearts spent after the original success. CardTransaction gains HeartRefillPurchaseId and a same-card composite FK/unique link. Deferred SQL constraint triggers require every successful purchase to have its matching currency/amount debit, reject debits for failures and reject orphan successful purchases. Purchase and card-ledger evidence is append-only. Failed rows require a non-null known reason; a follow-up constraint explicitly closes PostgreSQL CHECK's nullable-expression loophole.

Declines: CardBlocked, CurrencyMismatch, InsufficientBalance and HeartsAlreadyFull. They persist as completed failures without debit or heart grant, and replay the same failure. A deliberate ledger-insert failure is tested to roll back purchase, money, hearts and audit; recovery lookup then returns404and the same key may safely retry. No real failed financial transaction is manufactured in the user's account; integration fixtures use isolated databases.

## Network recovery and privacy

HeartCheckoutStore distinguishes pending, checking, known result and saved-result/reload-failure states. After an uncertain HTTP/network outcome, another purchase is blocked. The user first queries the saved request's status. A known result is restored without POST; only404enables an identical-key/body retry. Uncertain status never automatically enables retry. Server idempotency also protects a race between status lookup and an original request still committing.

A small owner-scoped sessionStorage reference contains only card ID, offer code and request UUID, not credentials/price/tokens. Page reload restores it and performs status lookup only, never automatic payment. Malformed metadata is discarded; unavailable storage retains the in-memory request and displays a keep-page-open notice. Account changes remove pending/private state and ignore late responses. Successful writes followed by failed balance reload preserve the result and block a fresh purchase until current offer/hearts reload. Purchase history shows historical charged amounts; it never grants current hearts in the browser.

## Verification and setup

Apply AddHeartRefillPurchases (20261028202502) and RequireHeartPurchaseFailureReason (20261028203522); schema.sql is regenerated. AddHeartRefillPurchases refuses destructive Down migration once any purchase evidence exists: use a forward fix or a separately verified database restore. This safeguard is not a claim that production restore procedures have been tested.

Full308backend tests passed. Final19focused billing cases (11heart-purchase cases including theory rows,8wallet cases) reverify the final SQL constraints/shared access after the reason-integrity migration. They cover atomic success/real saved-lesson resumption, spending after replay, same/different-key concurrency, four declines, free expiry, owner/forged-price denial, immutable/orphan/failed-debit/missing-reason SQL checks and rollback. Full162frontend tests/40files and production build pass, including recovery-before-retry, reload lookup without payment, late account replies, known declines, full-heart/no-price override, explicit confirmation, escaped card names and negative debit display. C# and frontend formatting pass. No physical microphone/device or authenticated phone visual test was performed.

## Remaining mandatory scope

The original subscription price catalog, BillingOrder/PaymentAttempt/Subscription/receipts, scheduled renewals, entitlements, billing-admin/refund operations and broader global financial history remain required. This one-time addition does not replace them. Refund/corrective-entry policy for spent heart products must be resolved with those administration flows; history must never be edited to simulate a refund. Premium UnlimitedHearts compatibility, real-device/accessibility verification, native speech hardening and all other unchecked production/security/content requirements remain open. No public deployment or absolute security claim.

New completed attempts are bounded to20/account/10minutes under the account lock, in addition to the shared IP limiter. Existing result replay/status lookups remain available at the quota. The final19focused PostgreSQL cases verify the limit and replay exception. The earlier full308backend run preceded this last quota addition; it is not a claim of a full309test run.

Resumed runtime verification: API readiness, local heart checkout and phone preview return200, all three distinct offer/purchase/status paths appear in Development OpenAPI, and database/mail/storage are healthy. The prior approval-review usage-limit interruption was resolved by an authorized retry; final19focused billing tests pass. This does not claim authenticated physical-phone visual verification.

## Premium compatibility — October29

Active paid subscription periods now supply server-controlled UnlimitedHearts. Effective hearts stay full without consuming the underlying Free wallet; one-time refill checkout therefore declines without charge. Grade/XP rules remain unchanged. Current plan/entitlements are derived at request time; expiry immediately returns Free access and retained progress. SUBSCRIPTION_PURCHASES.md. Remaining cancellation/renewal/refund/administration/device gates stay required.
