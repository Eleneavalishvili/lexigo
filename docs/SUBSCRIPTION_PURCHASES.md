# Free/Premium catalog and simulated subscription purchases

## Current scope

The PDF's exact demo prices are seeded: Premium Monthly9.99GEL and Annual79.99GEL, with Free core courses, vocabulary, basic practice, progress and in-lesson listening. /pricing compares the server catalog; /billing/subscribe reviews then pays a quoted order; /billing/subscription shows the current paid period; /billing/receipt/:paymentId shows and downloads an owned clearly labelled .txt test receipt. Account and wallet navigation link to the new screens. Existing retro fonts/palette/Pip/Sol remain.

Every charge uses internal test-card funds. There is no real money, provider/account, real PAN/CVV, paid subscription to an external service or email sending. Automatic renewal defaults OFF. Future cancellation/resume/renewal preferences/jobs, scheduled changes, refunds/billing administration and full subscription/financial history remain mandatory, not falsely exposed as working controls.

The catalog explicitly marks advanced analytics, extra dialogues, advanced practice, dedicated audio practice and character calls Planned until their actual features exist. Entitlements and availability are separate: a description in the plan does not manufacture a feature. Core learning/listening remains Free. UnlimitedHearts is actually enforced by the server for current paid periods.

## Persisted models and server decisions

A newly quoted Premium order returns 201 with its private order detail
URL. An identical retry returns 200 and reuses the original quote.
Reusing the same key with a different price returns 409.

Nine original entities are mapped: SubscriptionPlan, PlanPrice, Feature, PlanFeature, BillingOrder, PaymentAttempt, Subscription, SubscriptionEvent and PaymentReceipt. AddSubscriptionPurchases20261029100220 is additive migration19, including stable plan/feature seeds. Extra owner/key/current-price/quote-expiry/scenario columns provide explicit ownership, replay and access checks. Original70models/122API rows remain intact; refunds/jobs and aggregate model completion remain tracked separately.

Orders accept only a current Premium PlanPriceId and request UUID. The server copies amount/currency/period and gives the quote30minutes. Order creation charges nothing and grants no access. Catalog retirement blocks new orders; an existing valid quote keeps its saved price until expiry. Prices are versioned: changing historical amount/period/currency/plan/start is rejected by SQL; retire via validTo and add a new version. Existing paid rights remain through the paid period even if a plan is later hidden from new purchases.

Payment accepts only an owned order/card, request UUID and an explicit simulated-technical-decline flag. Active confirmed account/session/security stamp is rechecked inside the user-first billing transaction. Same-owner/card/order foreign keys and rate limits apply;20new orders and20new payment attempts/account/10minutes, with result replay still available. No browser price/status/owner/Premium flag is trusted. Successful payment creates the debit/card version, closes the order, activates one subscription, appends activation/audit events and issues one receipt atomically. Calendar UTC month/year boundaries are used (start<=now<end). An ended Active row is expired/flushed before a new period is inserted, preserving the one-Active-subscription unique index.

Blocked card, wrong currency, insufficient funds, explicit technical decline, expired order and another active subscription produce persisted uncharged failures. Failed payments have no activation/debit/receipt. Existing payment keys replay the same attempt; changed order/card/scenario conflicts. A failed attempt's replay remains historically failed and does not acquire a later successful attempt's subscription ID. First and replayed timestamps use compatible stored precision. A same-order successful payment is unique independently of request key.

## Database and review boundaries

Money uses decimal(18,2) with value/state/time checks. Owner/card/order composite FKs, unique active subscription, unique successful order payment and unique receipt/debit links backstop application rules. Order quotes/closed states, price versions, terminal payment attempts, receipts, subscription events and card ledgers resist rewriting. Deferred triggers require a successful payment's matching saved quote, same-card debit/currency, owned subscription, activation event and receipt; paid orders require a success, and unsuccessful attempts cannot debit/activate. Down refuses destructive removal once billing orders exist. This is not a claim that production backup/restore or an external financial audit is complete.

Future administrative event/refund writers must enforce the same payment/subscription subject checks. This slice creates activation events only inside the owner-locked service and checks them against the paid order. Catalog-admin overlap/version editing, renewal payment/event rules, refund/corrective entry policy (including spent heart products), billing-admin MFA/access/audit and production least privilege remain release gates.

## Unlimited hearts and expiry

HeartBalance wraps the real Free wallet with an effective Unlimited flag. Active Premium reads as5/unlimited with no recovery countdown, while incorrect graded answers and speech misses do not spend the stored Free balance. Underlying natural recovery can still run; subscription expiry falls back to the Free wallet without deleting progress. Both core and expired-Free listening entitlements remain available. Server grading/80percent passing/first-pass XP are unchanged: UnlimitedHearts never makes an incorrect answer correct or grants XP for failed work. A heart-refill purchase is refused without charge when Premium already provides unlimited hearts. Completed-session history is not re-labelled by a later upgrade.

## Client workflow and recovery

BillingStore owns plans/subscription/entitlements/cards/order/result/pending state. The user first reviews a server quote, then explicitly confirms a test-card payment. Unknown outcomes block another mutation and require owned by-request status lookup. Only404enables retry with the identical key/body; Pending stays pending and cannot be treated as a decline or success. A known saved payment followed by failed state refresh blocks fresh payment until current server state reloads.

Owner-scoped sessionStorage retains only request identifiers and test scenario, never credentials or prices. Reload looks up the saved request; it never automatically pays. Account changes and late replies clear/ignore private data. Receipt route changes ignore old results and account changes remove old receipt details/download access. Receipt blobs are downloaded only on user action. Pricing/checkout render untrusted names as text.

## Routes and verification

Original routes implemented in this slice: GET/plans, GET/me/subscription, GET/me/entitlements, POST/me/billing-orders, GET/me/billing-orders/{id}, POST/me/billing-orders/{id}/payments, GET/me/payments/{id}, GET/me/payments/{id}/receipt. All but plans require authenticated ownership. Two supporting GETby-request routes recover unknown order/payment outcomes. Global card history now carries payment references and receipt links, with Premium debits distinguished from heart refills; broader failed/refund filtering remains required.

Full323backend tests passed before the final added acceptance cases. Final36focused billing cases pass:17subscription cases,11heart cases and8wallet cases. They include exact prices/Free listening, monthly/yearly success/receipts,4declines, concurrency/owner/forged-price denial, quote boundary/price retirement, unlimited-versus-stored hearts/no-XP/expiry, expired repurchase, quote/payment immutability, orphan success/failed-debit SQL denial, transactional rollback and historical failed replay. An initial test tried to complete a default two-question fixture after one answer; the fixture was corrected to one question before passing, without weakening the lesson rule. No failure was waived.

Full174frontend tests/44files and production build pass. Stronger receipt-route assertions also pass in the two-case focused run. Tests cover review before pay, truthful planned labels, exact outbound DTOs, status/reload recovery without automatic payment, Pending, stale accounts/routes, saved-result refresh failure and safe receipts. C#format/Prettier pass. Actual authenticated phone visual/device/microphone checks and all remaining production/security requirements remain open. No whole-project completion claim.

## Management controls added — October29

Cancellation/resume and saved renewal/default-card/next-period choices are now available in SUBSCRIPTION_MANAGEMENT.md. They preserve current paid access and charge nothing. A fresh manual purchase resets old auto-renew consent OFF. Automatic renewal execution is still unavailable and explicitly labelled; no worker or recurring charge is implied by saving a preference. Earlier statements that all management controls were pending describe the purchase-only milestone.
# Unpaid checkout cancellation

`POST /api/v1/me/billing-orders/{id}/cancel` cancels an owned, unpaid,
unexpired purchase quote. Repeating a successful cancellation returns the
same order. An already paid order or an order with a pending payment cannot
be cancelled. The simulator normally settles synchronously. Persisted
pending records are readable by request key and prevent additional payment
attempts while unresolved.
