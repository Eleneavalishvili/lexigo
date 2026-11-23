# Subscription management controls

## Implemented scope

The protected subscription page now lets the current paid member request cancellation at period end, resume the same period, save a renewal preference/default test card, and choose or clear a next-period Monthly/Annual price. Every action targets an explicit owned subscription ID and request UUID. It charges no funds, creates no new paid period, and cannot alter current start/end, price snapshots, learning progress or Premium access before the paid end.

Cancellation sets cancelAtPeriodEnd while preserving a previously saved renewal preference. Resuming clears only the cancellation flag; it does not opt an off preference into renewal or charge funds. Cancellation takes precedence. Saving renewal ON while cancellation is scheduled is rejected until explicit resume. Turning OFF stays possible with an already selected blocked card, or its card can be cleared. Selecting a new default must use an owned active card; ON additionally requires the next selected price currency. Foreign/unknown cards get the same generic rejection.

A scheduled price must currently be available for the same active plan. A saved auto-renew preference requires an active matching-currency default card before a different price is scheduled. Clearing the next-period choice is supported. The response shows the scheduled period/price separately from the current paid period. Retired/invalid price IDs are rejected; current money/dates/entitlements are unchanged.

A fresh manual subscription purchase now explicitly resets previous account-level auto-renew consent to OFF while preserving the selected default card. This prevents an expired or previously canceled subscription's consent silently following a new manual purchase. Its reset event is included in the existing payment transaction. Public manual payment rejects renewal-order types, reserving those for the future server processor.

## Processing availability is explicit

RenewalProcessingAvailable is currently false. The UI says these are saved preferences and no automatic renewal processing is available yet; it does not claim that a scheduler runs. The account summary says Saved:on/off rather than falsely asserting an operational recurring charge. Actual durable renewal jobs, period-key uniqueness handling, scheduled price application, failures/notifications/retry and refund/admin workflows remain required next. Enabling a saved preference alone never charges a test card or extends Premium. Expiry continues to return Free access using the existing server UTC entitlement boundary.

## Atomicity, authorization and recovery

POST /api/v1/me/subscription/cancel and /resume; PUT /renewal and /scheduled-plan use authenticated subject ownership, confirmed active/session/security-stamp checks under the shared user-first lock, bounded strict DTOs, no-store and existing IP rate limits.20new management changes/account/10minutes bound replay/event growth; replay is allowed before quota and current-period checks. Same account/operation/key with different normalized body conflicts. Explicit subscription IDs keep an old request from targeting a replacement period.

Subscription flags/preferences, append-only SubscriptionEvent, AuditLog and existing BillingOperation replay evidence commit in one transaction. No-op settings do not create duplicate change events; the successful request itself is still audited/replayable. The service first persists changes to read a real current summary, then stores that snapshot within the same uncommitted transaction. Injected replay-insert failure proves even the earlier save rolls back flags/events/audit. Historical replay returns its original snapshot without applying the mutation again; UI then reloads authoritative current state, so replaying a cancellation after resume does not re-cancel it.

SubscriptionSettingsStore is separate from payment state. Unknown outcomes retain exact kind/body/request key and block another edit; identical retry is safe because these controls never transfer money and the server stores replay evidence. Known rejections clear pending state. The parent reloads current server subscription/cards after success. Private state/form inputs clear when account identity changes; late replies cannot update another account. No credentials or real-card data are collected.

## Verification

Full334backend tests passed0fail/skip (subscription-management-full.trx), including8new management cases. Tests cover cancellation/resume/access/date preservation, original-state replay after later changes, concurrent duplication, owned/blocked/missing-card rules, ability to disable with a blocked existing default, no charge, scheduled price/current-period separation, clearing/conflicting keys, foreign/expired denial, consent reset on new purchase, and injected SQL rollback. Full180frontend tests/46files and production build pass;6new state/UI cases verify explicit actions, retry-key retention, known rejection, account cleanup, name escaping and truthful unavailable-scheduler text. C#format/Prettier pass. No schema change/migration is needed for these existing fields.

Actual authenticated phone visual/a11y verification, renewal execution, notifications, plan-change execution, refunds/admin/history, native speech/production security/restore gates and all other original requirements remain outstanding. Original70model/122API baseline remains intact; no real charge, external message, role grant or deployment occurred.


## Processor follow-up — October29

The subsequent SUBSCRIPTION_RENEWALS.md slice implements durable processing and account notices. Earlier verification above describes the saved-preference phase. Current processing availability follows the explicit BillingRenewals configuration. Old saved ON preferences require fresh subscription-bound confirmation; they are not backfilled into consent. Late cancellation/renewal OFF is now permitted before the worker, without the positive-edit business quota. After an ended-period cancellation, consent is cleared. Before-end cancellation still preserves the preference for resume. A newer active subscription cannot be targeted through an older expired one. Full evidence and any remaining gates are in PROJECT_MEMORY.md.
