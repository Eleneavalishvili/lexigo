# Owned financial and subscription history

Verified 2026-10-30: full360 backend tests,192 frontend tests across50files and production build pass. The focused billing/inbox run passes70 cases, including the real hosted renewal timer, the financial-history unions and subscription-event ownership checks. Evidence: ignored local `work/history-full.log`, `history-billing.log`, `history-frontend.log`, `history-frontend-build.log` and their TRX files.

## Card activity and declined attempts

The original `GET /api/v1/me/transactions` and per-card history operation return bounded pages of owned activity, newest first, with a deterministic time/UUID/source order. Card, type, status and UTC date-range filters apply to every source branch. Foreign and unknown card filters return the same404; malformed states, reversed ranges and out-of-bound pages return400.

The query combines immutable successful ledger movements with non-succeeded subscription PaymentAttempt and HeartRefillPurchase projections. Failed or pending attempts are never inserted into CardTransaction. Their `BalanceAfter` is null, their amount is explicitly requested rather than debited, and they never receive a receipt link. Each successful subscription debit and failed attempt carries the original order ID and stable payment reference. Currency comes from the saved quote for an attempted payment and from the card for actual balance movements. Success is not duplicated between attempts and ledger rows.

BillingActivityView/Page describe this mixed read model. CardTransactionView remains the non-null-balance view of a real ledger movement in mutation responses; the original SQL ledger model is unchanged. `EntryKind` distinguishes ledger, payment and refill IDs. The wallet retains filters during pagination and clears them across account changes. UI dates mean local calendar days, converted to UTC with the through-date exclusive at the following midnight. Backend dates also normalize offsets to UTC. All names/references remain escaped.

## Subscription chapters and event timeline

Two supporting read operations serve original REQ-121 without changing the baseline122-operation register:

- `GET /api/v1/me/subscriptions?page=1`: owned current and past subscriptions,20per page, no Premium requirement. Stored Active rows past their UTC end display Expired even before the worker runs.
- `GET /api/v1/me/subscriptions/{id}/events?page=1`: owned event timeline,20per page, generic404 for unknown/foreign subjects. Actor account IDs are not disclosed; the UI distinguishes an account action from a system update.

`/billing/history` is accessible from Account, Subscription and Wallet. It shows each subscription's latest paid period and its saved lifecycle updates. For activation/renewal payment events, immutable order snapshots provide the amount/currency/period. Purchase boundaries come from the original successful payment timestamp; renewal boundaries come from its original due-period key. Historical terms are not reconstructed from the current mutable subscription price or the live catalog. Receipt links point only to owned successful payments.

SubscriptionHistoryStore separately guards page loads and selected-event requests so an old selection or an old account cannot overwrite the new pane. Loading/error/empty/retry/page states remain read-only; opening history performs no cancellation, renewal or charge.

## Integrity and release review

EnforceSubscriptionEventOwnership is a SQL-only migration after AddSubscriptionRenewals. It validates existing rows before adding a deferred insert constraint: a related payment must be successful and belong to that subscription/account. This closes the earlier cross-subject event gap for future writers. Immutable events cannot later be repointed. Read joins independently restrict payment/order ownership and successful status so an inconsistent legacy relation cannot expose another account's terms or invent a paid period.

The review includes foreign IDs/counts, pending/declined balance truth, currency mismatch, page bounds/tied timestamps, offset dates, stale accounts/selections, receipt eligibility and failed/cross-subject event insertion. PostgreSQL tests verify these boundaries and preserve original ledger rows. This is self-review, not an external security audit.

Local database now has21migrations. API5187, Angular4200 and iPhone preview4201 are healthy; no tests seed actual learner cards, hearts or privileged roles. SMTP remains disabled; no real funds or providers are involved.

Refund administration, refunded activity/type additions, billing permissions/MFA/audit, catalog administration/version-overlap rules, broader notification producers, physical-device/accessibility checks and production recovery/security gates remain required. This history slice is not completion of the complete project.
