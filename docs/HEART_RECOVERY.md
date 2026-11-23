# Shared hearts and one-hour recovery

Internal checkpoint, 2026-10-28. The user requested one free speaking retry followed by a heart charge, optional heart purchases, and a one-hour full-heart countdown. This supplements the authoritative PDF and earlier starting rules.

## Implemented recovery

Hearts are account-wide across active lessons. Starting/reloading another lesson cannot reset the balance. Accounts begin with five. An incorrect graded answer spends one heart; ungraded Speaking and Free Practice retain their existing no-loss behavior. Completed sessions retain their historical heart snapshot.

When the balance reaches zero, the server fixes `RefillAt` to exactly one hour later. Additional requests/replays do not move that deadline. At or after the deadline, server reads expose five hearts and the next permitted mutation persists the recovery. A client countdown never grants hearts. No always-running background timer is required for correctness. New regular lessons are blocked while exhausted; existing lesson answers/progress remain saved, and Free Practice stays available. Active saved-session reads and reference-audio access use the same current balance.

`LearnerHearts` is an additional supporting model keyed by UserId, with Count and nullable UTC RefillAt. SQL restricts counts to0–5, requires a deadline exactly when empty and restricts the user FK. Writes share the existing account-row transaction lock, preventing parallel lessons from spending the same heart or losing updates. Existing users are backfilled using the smallest unfinished regular-session balance; an exhausted legacy balance gets a fresh one-hour recovery deadline. Completed history is unchanged. New users receive a wallet on their first learning mutation, with the same server-enforced initial balance.

`GET /api/v1/me/hearts` returns explicit HeartStatus with count, maximum, refillAt and serverNow. It requires the active confirmed account/session, is private/no-store and has no selectable account ID. Query-string attempts to choose another owner do not alter ownership. No refill/purchase/client-time/price field is accepted from the browser.

## Countdown UI

Regular active lessons include an original-style recovery card at zero hearts. The HH:MM:SS countdown uses server timestamps plus elapsed monotonic time, avoiding device wall-clock changes. At expiry it asks the server, then reloads the saved session only after recovery is confirmed. Visibility changes recheck the balance; failures have retry/backoff and never manufacture hearts. Account changes/destruction discard private state and late responses. The timer is not announced every second to assistive technology. Existing colours/fonts/characters are preserved.

## Speaking rule and purchases

The user chose one free unsuccessful spoken-word check per question. Every subsequent unsuccessful spoken-word check costs one heart. Microphone/processing failures and silence are not scored wrong answers. Core HeartRecovery.SpeakingMissCostsHeart is tested, but the speech-attempt persistence/API/UI integration remains required; the existing ungraded Speaking exercise is not silently converted into graded speech.

The user also requires purchasing hearts through the previously approved simulated test-card/payment system. That checkout, server price/ledger/idempotency/entitlement behavior and heart-refill transaction have not been implemented. There is no fake purchase button granting free hearts and no real-money/provider integration. These purchase requirements remain explicitly unchecked, alongside Premium UnlimitedHearts and administrator-configurable learning policies.

## Verification

- Six HeartRecoveryTests cases: exact deadline/full refill, no premature recovery, zero-balance rejection/deadline preservation and the one-free-speaking-retry policy.
- Three SharedHeartsTests cases against isolated PostgreSQL: shared balances, blocked new lessons, free practice, canonical replay/no timer reset, preserved session resumption, parallel cross-lesson spending without lost updates, owner isolation and actual SQL check/FK rejection.
- Final full backend:274passed,0failed/skipped (shared-hearts-final.trx); C# whitespace verification passed.
- Five UI cases verify server-anchored full-hour display, wall-clock independence, server-confirmed refill, stale account response rejection, unavailable-state recovery and exact countdown-boundary rechecking. Full frontend:136passed/35files; production build and Prettier passed.
- The initial UI mock incorrectly said the wallet was full while its lesson stayed exhausted, causing repeated reloads; corrected the contradictory mock. A new one-second boundary test then caught a15second recheck delay; implementation fixed to recheck at the actual deadline. Failures were not waived.
- Migration AddSharedHeartRecovery applied locally; schema.sql regenerated; EF no pending model changes. Actual local DB:14migrations,4users/4heart balances,0lessons. No real learning history was manufactured for verification. API readiness/site/phone preview all HTTP200 and OpenAPI includes HeartStatus.

Actual authenticated browser/device/keyboard checks, real reviewed curriculum, microphone word-check integration and simulated billing remain required. Formal Georgian PDF unchanged.

## Speaking retry integration — October28–29

SpeakingCheck now records misses and heart charges durably within the same account-locked transaction. The first mismatch costs0, subsequent mismatches1; silence/technical failures0, Practice0. Concurrent retry of the same canonical recording/request key cannot double-charge. Six misses exhaust the five-heart wallet and start its existing one-hour recovery without advancing the unanswered question. Typed answers cannot bypass a voice check. See SPOKEN_WORD_CHECKS.md. Simulated purchase checkout, pricing, payment/refund ledger and Premium UnlimitedHearts remain required.

## Simulated purchasing added — October29

The user approved5simulated GEL for restoring the current shared balance to5. Owned /billing/hearts checkout now includes explicit test-card confirmation, atomic immutable debit/purchase/audit/refill, declines, status-before-retry recovery and history. Existing one-hour free recovery wins at payment time and prevents charging a full wallet. Replaying an earlier purchase never restores subsequently spent hearts. See HEART_CHECKOUT.md for setup, evidence and remaining subscription/refund/Premium/device gates. Earlier statements that purchase endpoints were pending describe earlier milestones.
