# Simulated refunds and delegated billing administration

Verified 2026-10-30: full381 backend tests,204 frontend tests across55files and the production build pass. The focused billing/administration/inbox run passes102 checks. Logs/TRX: ignored `work/refund-full.*`, `refund-billing.*`, `refund-frontend.log`, `refund-frontend-build.log`. The local AddSimulatedRefunds migration is applied (22migrations), with no EF model drift. No actual learner role, permission or refund was created by agent tools; privileged test accounts live only in isolated PostgreSQL fixtures.

## Who can access billing

Financial API access requires a current active, confirmed, two-factor-enabled account and a current MFA-verified session/security stamp. SuperAdmin inherits access. Admin requires an explicit individual `permission=BillingManage` identity claim; Admin alone and other staff roles do not grant access. Claim presence on a Learner/Teacher/author/reviewer/moderator is insufficient. JWT roles and a frontend route guard are not substitutes for current database authorization.

The account summary projects effective billing permissions so authorized users can find `/admin/billing`. No new role is invented. A SuperAdmin can select an account in People & Permissions and explicitly grant/revoke BillingManage. New grants require an eligible active, confirmed Admin enrolled in MFA. Grant/revoke requires current password, reason and an MFA sign-in within5minutes. Changes rotate the target security stamp and revoke existing sessions. Removing Admin also clears old delegation so re-adding a role cannot silently reactivate it. SuperAdmin's inherited access changes through role administration instead.

Permission requests use canonical actor/key replay without storing the password. An old successful grant replay after a later revoke returns its original response without regranting; the UI reads the real current state afterwards. Privileged role changes, grants and refunds share the existing administrative advisory guard and deterministic actor/customer row-lock order. Wrong-password counters persist without financial writes.

## Refund policy chosen under recommended-default authorization

- Only a successful subscription payment can be refunded. Amounts must be positive, at most2decimal places and no greater than the amount still refundable. Partial refunds are allowed; their cumulative amount cannot exceed the original payment.
- Credit always returns to the original test card and currency, including a blocked card. There is no destination/card field in the request. Self-approved refunds are denied; another authorized administrator must act.
- Partial refunds retain paid access. A cumulative full refund of the subscription's latest paid period marks that period Refunded, ends its Premium entitlement and stops its renewal/scheduled choice without resetting progress or the underlying Free hearts wallet.
- A refund of an earlier renewed period or an expired separate subscription preserves a newer paid period and its consent. Original order/payment/receipt evidence is never rewritten; the refund is a new credit/event/audit record.
- The previous10000ceiling is explicitly refined: it limits user top-ups, not the return of previously paid funds. A legitimate refund may restore a card above that ceiling. Top-ups still cannot increase a balance above10000; balances never become negative. The wallet explains this exception. No credit is silently capped or lost.

Refunds require current permission plus a fresh<=5-minute MFA sign-in, current password and reason. A request key is scoped to the acting administrator. Identical replay does not credit twice; changed amount/payment/reason conflicts. The original subscription period is identified from immutable payment/quote evidence, not today's catalog.

## Atomic SQL evidence

Credit, card version, Refund, refund ledger movement, linked SubscriptionEvent, AuditLog, private notification and applicable entitlement/consent changes commit together. A forced notification failure proves rollback of all earlier changes. Refunds and ledger/events are immutable; correction requires a new authorized record.

A payment row `FOR NO KEY UPDATE` lock serializes the database aggregate-refund cap independently of application locks. This lock mode remains compatible with foreign-key key-share locks. Deferred checks reject over-refunds, unsuccessful payments, self/unauthorized actors, wrong/missing original-card credit, event/actor/subject mismatches, missing audit/notice and incorrect current-period revocation. SQL verifies roles/enrollment; the application additionally verifies the current MFA session and password. The migration refuses downgrades that discard refund evidence or returned balances above the old ceiling.

## API and interface

Original operations now implemented:

- `GET /api/v1/admin/payments`: bounded status/customer/payment/date-filtered records, original amounts and aggregate refunded amounts.
- `GET /api/v1/admin/subscriptions`: bounded customer/status records, with effective UTC expiry.
- `POST /api/v1/admin/payments/{id}/refunds`: explicit authorized simulated refund.
- `GET /api/v1/admin/billing-audit`: only financial entity records, bounded date-filtered pages; unrelated account/security audit is not included.

Supporting operations, kept outside the original122-operation register:

- `GET /api/v1/admin/refunds/by-request/{key}`: current financial access, actor-owned request lookup, generic404.
- `GET/PUT /api/v1/admin/users/{id}/billing-permission`: SuperAdmin-only delegation read/write.

The retro billing desk opens with read-only records. Selecting a refundable payment is separate from entering amount/reason/password and explicitly confirming. Failed, fully refunded and self-approved payments cannot be selected for refund. Passwords clear after every attempt; no password is placed in logs, audit, replay snapshots or browser storage.

An uncertain refund blocks another intent. The browser retains only payment/request UUIDs, scoped to the actor, and uses GET status before permitting an identical-key retry. Reload never automatically POSTs a refund. After a404, re-entered details still use the same key; a concurrent original commit or changed payload cannot produce a duplicate credit. A confirmed4xx rejection of a fresh request clears only that uncommitted intent. Unavailable status checks remain uncertain. Late responses, selected records and forms clear across account/target changes. If browser recovery storage is unavailable, the interface asks the operator to keep the page open until resolution.

## Review and remaining gates

This slice's self-review covers default-denied staff access, MFA/fresh proof, permission revocation races, old grant replay, deterministic locks, current/historical periods, partial/full/concurrent refunds, blocked/overflow credit, immutable/atomic SQL evidence, private audit/lookup, no password persistence and GET-before-retry behavior. It is not an independent external audit.

Admin catalog creation/version-overlap controls, full seed/demo/setup reconciliation, other learning/notification features, CI/security/secret scans, production least privilege/backup restoration/incident procedures and physical-device/accessibility checks remain required. Real payments, public deployment, paid services and automatic real-account privilege assignment remain unauthorized. This is not a claim that the entire project is complete or immune to attack.

## Refund-note erasure during account deletion — 2026-11-18

The financial evidence remains immutable. Confirmed buyer deletion can replace
the refund's free-text reason with a fixed erased-note message. A database
trigger allows this only for a deleted buyer and refuses changes to every
other refund field. The related audit note is also erased in that transaction.

ReasonFingerprint preserves comparison with the original refund request
after erasure. Identical authorized retries return the existing refund with
the erased note. Changed details still conflict and no extra credit is made.
The migration backfills older refunds and protects that fingerprint during
rollback once notes have been erased. Refund amounts, original card/payment
references and ledger history stay available for reconciliation.

An authorized new refund for an already-deleted buyer uses the fixed erased
note and does not create a private account notification. The database still
requires the original-card credit, subscription event and audit. Active
accounts retain the ordinary atomic-notification requirement.
