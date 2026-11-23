# Foundation threat review

## Current feature threat and control map — 2026-11-17

The browser, local preview and public web origin are untrusted clients.
The API makes access and scoring decisions. PostgreSQL enforces ownership
and transaction constraints. Private object storage and the mail service
are separate trust boundaries. The following map consolidates current
feature controls. The dated entries below retain implementation details.

| Feature | Risk and current controls | Verification and remaining boundary |
| --- | --- | --- |
| Registration, sign-in, recovery and sensitive settings | A stranger may try to learn whether an account exists or reuse a reset token. Use generic public responses, throttling, antiforgery, one-use tokens and live session-family validation. Privileged operations check current roles and MFA. | Account and session regression suites. Staff recovery with every factor lost and production secret rotation still require verification. |
| Profiles, account export, deletion and avatar | One account could read another's private data or retain an obsolete picture. Server queries filter by owner. Avatar links expire. Erasure requires fresh proof and revokes sessions. | Profile and deletion integration tests and private storage walkthroughs. Retained free text and staff transfer remain open. |
| Catalog, authoring, publishing and imports | Unpublished content or grading keys could reach a learner. Staff reads require role, language scope or ownership, and publication requires independent approval outside the built-in curriculum. Import accepts bounded, validated data. | Publishing and import integration cases. Production content rights and language quality are reviewed separately. |
| Learning, placement, practice, dialogues and rewards | Clients may supply false scores, future choices, another user's session or duplicate rewards. The server snapshots grading, checks owners and the current question, and applies rewards atomically. | Session, placement, practice, dialogue and ledger tests. Current owner URLs and replay paths are recorded in the API register. |
| Recordings, reference audio and microphone | Private objects or learner recordings could be exposed. Validate PCM bytes, require media ownership and language review, and issue expiring links for owned sessions. Self-assessment uses transient browser audio. Explicit spoken checks use a separate bounded local processing path. | Storage, speech and object-lifecycle tests. Physical device testing and reviewed course recordings remain open. |
| Vocabulary, achievements, ranking and public cards | Public views could leak private progress or a capability could be guessed. Filters are owner-scoped, leaderboard requires opt-in, and public cards use separate revocable random codes with consent. | Vocabulary, ranking, challenge and card tests. Uploaded avatar use in ranking and production caching remain open. |
| Classrooms, reminders, teacher feedback and moderation | A class outsider could read a roster, consume a code, see feedback or receive private notices. Require current membership or scoped staff access, consume capped expiring codes atomically, and deduplicate notifications. | Classroom and reminder tests. Public-name moderation remains a separate gap. |
| Simulated billing and entitlements | Repeated or foreign requests could change balances or obtain Premium. Validate owner and price server-side, lock writers, and enforce idempotency and state transitions in PostgreSQL. Test-card funds stay inside the simulator. | Billing order, payment, refund and renewal tests. External payments have no approved implementation. |
| Hosting, logs, backups and mail | Secrets, private data and action tokens could spill into logs or external services. Use trusted proxy configuration, minimal request diagnostics, local mail fixtures and explicit restore rehearsals. | Trusted-proxy and diagnostics tests, source secret scan and disposable PostgreSQL restore. Live exposure, permissions and incident drills are release gates. |

Premium character calls are still planned. Their separate trust boundary
requires explicit decisions on audio processing, consent, provider access,
usage caps, transcripts, retention and failure behavior before implementation.
This map is maintained as a project self-review. The required separate
sensitive-flow review and release gates remain tracked under SEC-005 to SEC-008.

Historical foundation scope: local scaffold and PostgreSQL development container. Current account mitigations are recorded in the dated updates below; learning/business endpoints and billing remain pending.

- Database is bound to 127.0.0.1:5433, not all host interfaces; it is a separate LexiGo volume/project, leaving Vacay services untouched.
- Development password is randomly generated in ignored .env; no committed default password or production credentials.
- Local bootstrap database role can initialize its development database; production requires a separate migration role and least-privilege runtime role before deployment.
- Foundation originally exposed liveness only. Registration milestone added database readiness against the actual Users table.
- No internet deployment, live transactions or external accounts are created by this foundation.
- Dependencies and container image must undergo security review/pinning before public release. Template generation is not evidence that security requirements are complete.

## Dependency review (2026-10-27)

Initial npm install identified transitive undici advisories in Angular build tooling (GHSA-4qv8-2p9j-2685, GHSA-f72v-6w45-q472, GHSA-xjj6-f33q-929c). Applied same-major override undici 7.30.1; reinstall audit reported zero vulnerabilities and production Angular build succeeded. This is a snapshot, not a guarantee; keep scans in the eventual CI and review/remove the override when upstream tooling fixes its dependency.

## Registration threat review update (2026-10-27)

See REGISTRATION_SECURITY.md for the active milestone's mitigations/evidence and remaining release gates. All 16 registration integration tests passed against isolated PostgreSQL. Public registration assigns Learner only. Login and sessions were added in the session milestone below. Production deployment is still gated by authenticated session handling, 2FA, full authorization policies, secure production keys/secrets, HTTPS/proxy settings and the complete security checklist.

## Session/recovery threat review (2026-10-27)

See ACCOUNT_SECURITY.md for current decisions, tests and remaining gates. Treat stolen refresh cookies, refresh replay/races, cross-user session IDs, credential guessing, CSRF, reset token theft and account-role/status changes as explicit threats. Mitigations include hashed rotation, family revocation, user-row lock ordering, strict cookies, framework antiforgery validation, per-account/IP controls and server-side session checks. Privileged access fails closed without two-factor verification; enrollment/recovery is still required. This is an internal implementation review, not an independent external audit.

## Password/contact/authenticator update (2026-10-27)

New passwords require uppercase A–Z and a punctuation/symbol on registration/reset/change; whitespace is insufficient. UI and server validators have negative and Unicode-symbol cases. Existing sign-in remains compatible with previously created accounts.

Authenticator enrollment, challenges, one-use recovery, encrypted secret storage, replay history and mandatory privileged enforcement are now implemented/tested. See TWO_FACTOR_SECURITY.md for evidence and remaining privileged provisioning/authenticator-replacement gates. A password reset does not remove MFA. Contact Us contains user-supplied public details; the fictional address is explicitly marked Demo address. Its mailto link opens the visitor's mail application; no automatic external sending was introduced.

## Brevo preparation (2026-10-27)

External SMTP settings now fail startup validation when delivery is enabled with missing credentials, invalid sender/host, implicit-TLS port 465 or a test-domain sender. Production cannot use loopback test SMTP. The Brevo setup script uses hidden input and JSON stdin into local User Secrets, leaving delivery disabled. It has not been executed with a real key. Mailpit stays active. Activation must review queued messages/recipient consent and the frontend origin; localhost links cannot serve remote users. Domain/sender verification and actual receipt remain unverified. No account creation, key generation, outside message or paid service was performed.

## Profile and confirmed email-change review (2026-10-27)

See PROFILE_SETTINGS.md and EMAIL_CHANGE_SECURITY.md for current threats, access rules and verified evidence. These supersede the old profile/email-change pending notes only for their implemented slices. Stale authenticated user records are reloaded after obtaining row locks. External SMTP is staged but disabled again following the approved first inbox test; key storage, authentication, receipt and account confirmation were verified. Original production storage, privileged provisioning/recovery, protected keys, deployment and full release gates remain pending.

## Initial administrator and authenticator-replacement review (2026-10-27)

See PRIVILEGED_SETUP.md. Threats include unauthorized first-role claims, concurrent bootstrap, factor bypass, last-recovery-code exhaustion, stale/rotated sessions, stolen pending keys, cross-session setup, wrong-code partial writes and replay. Mitigations are operator-only stdin provisioning, password/TOTP eligibility, advisory serialization, original-MFA-sign-in age, encrypted session-bound pending state, owner locks, transactional key replacement, session revocation and audit/security notices. No actual personal privilege grant or authenticator change occurred. All-factor-loss assisted recovery, other administrator policies and production security/restore gates remain pending.

## Account administration review — 2026-10-28

See ADMINISTRATION_SECURITY.md for threats, access matrix, transaction/lock order, tested denials and limits. Current scopes are persisted but future content/class policies must consume them; no full authorization completion is claimed. Last-admin protection covers implemented role/status operations; deletion remains pending. No real privilege assignment or public deployment occurred.

## Content publication review — 2026-10-28

See CONTENT_PUBLISHING.md. Reviewed threats: cross-owner/scope access, self-approval through combined roles, stale staff sessions, cross-version foreign keys, answer-key exposure, editing published material, concurrent approval/cycles, unsafe archive dependencies and unverified media references. Server role/session checks, independent review, root-course serialization, PostgreSQL constraints, version snapshots, private/public DTO separation and bounded requests cover the current text-content slice. Eight remaining exercise validators, media pipeline, scoring/session snapshot behavior and complete release/security gates remain pending. This internal review is not an independent audit.

## Learning session and reward review — 2026-10-28

See LEARNING_ENGINE.md. Threats reviewed: cross-user IDs, answer-key leaks, client-provided score/XP, skipped/duplicate answers, changed idempotency payloads, concurrent completion/reward farming, prerequisite bypass, exhausted unfinished high scores, stale source content and late cross-account UI responses. Owner locks, composite PostgreSQL FKs, unique reward sources, stored immutable evaluation snapshots, ordered answer checks, explicit pass status, active-session validation and bounded inputs mitigate the current two-format slice. Premium entitlements, remaining exercise/media formats, achievement writers and complete browser/release verification remain open. This is an internal implementation review.

## Token-order and character review — October 28

Reviewed missing/duplicate/foreign tile IDs, changed-order replay, cross-version keys, leaking accepted order through public DTOs, unsafe revision copying, invalid/null order shapes and stale frontend selection. Permutation validation, owner/session checks, snapshot-only evaluation, new-ID remapping and existing locks/replay rules cover the current four formats. Original SVG reactions use local CSS with accessible text, pause and reduced-motion alternatives; they make no AI/service claim. Full learner browser/a11y/theme/device checks and all existing release gates remain pending.

## Text matching update — October 28

Reviewed matching-key/order exposure, ambiguous labels, invalid/duplicate/reversed/foreign IDs, canonical replay changes, stale version pairs and client correctness claims. Private mappings, cryptographic shuffling, complete membership validation, owner/session locks, revision key regeneration and neutral UI address the current text slice. Audio matching and full browser/media/release checks remain pending. See MATCHING_EXERCISES.md.

## Context and visual-letter update — October 28

Reviewed context overposting/size/NUL/XSS, source-version drift, private key exposure, unsupported format gate drift, Russian-versus-Latin lookalikes and wrong-course publication. Bounded typed staff requests, plain text rendering, snapshots, shared capabilities, language-code validation and existing owner/review/session rules address this slice. Full audio/media/recording and browser/release review remains pending. See CONTEXT_EXERCISES.md.

## Private object storage foundation — October 28

Reviewed unsigned access, altered/expired bearer links, path/key injection, caller-selected overwrites, unbounded streams, unsafe endpoint configuration and accidental cloud-credential fallback. Random internally generated keys, strict key/lifetime/stream bounds, explicit credentials, clean-origin validation, production HTTPS and loopback-only local ports address this internal adapter slice. Signed links must never enter logs; permission changes cannot revoke an already issued link before expiry without deleting bytes. MIME checks here are not file-content inspection. Authenticated upload ownership/MFA/language rules, licensing/review, session playback authorization, SQL/object consistency, orphan cleanup, production least privilege, backup/restore and image/license/security review remain open release gates. Browser self-recordings are not uploaded by this foundation. See PRIVATE_AUDIO_STORAGE.md. This is an internal implementation review, not an independent security audit.


## Authenticated PCM media workflow — October 28

Reviewed filename/path injection, forged MIME/length, malformed/overflowing RIFF chunks, excess metadata/frame/duration allocation, metadata leakage, cross-owner/language access, revoked or non-MFA sessions, combined-role self-approval, client-controlled verification/hash/IDs, upload/quota races, cleanup/review races, references from old versions, interrupted PUT/commit and object-delete failure. Canonical bounded PCM parsing, server IDs/hash/duration, live scoped actor checks, user→media row locks, independent reviewer plus SQL self-review constraint, restrictive exercise FK, immutable review/upload metadata and durable Pending/Deleting state address this slice. Publication rejects legacy flags without matching-language review and file evidence. Tests use real isolated PostgreSQL/Garage, including parallel review/deletion/cleanup and failed cleanup retry. Private UI data/URLs clear on account change/destruction; no microphone clips upload automatically.

Open release gates: broader codec validation, real authenticated browser/device playback, licensed curriculum/attribution, attachment locking and additional module references, learner session playback rights, queue capacity/alerts/failure isolation, image/decoder dependency scans, production credentials/TLS/storage redundancy/restore and separate human/security review. An issued link remains a bearer credential until expiry or deletion. This is an internal implementation review and does not assert independent audit or full-project security.


## Saved lesson audio and Listening — October 28

Reviewed foreign/wrong-language/unreviewed file attachment, matching-mode bypass, attachment-versus-delete races across different actors, source replacement during learning, cached signed URLs, future/foreign/session-mixed requests, revoked sessions, withdrawn media, client correctness/playback claims and stale browser responses. User→course→media locks, shared review-readiness predicates, source and saved-session restrictive FKs, immutable public attribution/private scoring snapshots, current/answered-only owned playback, on-demand no-store bearer links and explicit native-element unload address this slice. Learning does not accept browser-provided scores or require a claimed play event for grading. Both typed and choice Listening retain canonical retries and server hearts/XP. A same-account token refresh does not interrupt the player; account/exercise changes cancel stale links.

Open gates remain real licensed curriculum/attribution review, device/browser/Safari checks, broader codec/decoder scans, Speaking and dedicated audio Matching/Alphabet behavior, future module reference/deletion coverage, production CSP/media origins/TLS/storage backup and all existing release gates. Links already issued can remain usable for up to five minutes after a session change; this is documented, not presented as instant revocation. This is an internal implementation review, not independent audit.


## Speaking self-assessment and score separation — October 28

Reviewed practice-flag forgery on graded questions, client-provided score/grade fields, replay changes, false correctness/accuracy, mixed-denominator inflation, speaking-only XP farming, zero-valued reward rows, first graded rewards after ungraded progress, stale microphone permission and overlapping reference/capture playback. Strict typed payloads and unknown-field rejection, server-written IsGraded, the PostgreSQL ungraded-correctness check, immutable type-based assessment, existing user locks/reward uniqueness and explicit local playback/capture lifecycle address this slice. The server stores a self-report, not proof of recording or correct pronunciation. No audio leaves the browser. Manual fallback remains explicitly ungraded.

Future mistakes/mastery/reporting must filter ungraded rows. Human microphone/browser/Safari verification, actual licensed curriculum, audio submodes, Premium/AI, production operations and all existing security/deployment release gates remain open. This is an internal implementation review, not an independent audit.


## Per-option audio and Russian sound association — October 28

Reviewed foreign/media-ID substitution, option/session mixing, unknown/unreviewed/wrong-language/deleting clips, duplicated media or normalized bytes, right-side/whole-Matching attachment bypass, case-only sound decoys, copied keys/references, multi-file lock ordering, source mutation and deletion of saved clips. Reviewed owner/language predicates, strict pair shape and private keys, PostgreSQL-ordered batch locks, per-source and saved restrictive FKs, parent-owned current/answered lookups and explicit no-store links cover this slice. Source/media validation is preloaded without tracking to avoid stale cached evidence and per-item query loops. Playback still awards no XP and makes no proof-of-listening claim.

Remaining gates: actual licensed/linguistic curriculum review, authenticated browser/device/keyboard/Safari checks, semantic homophone ambiguity beyond exact-byte detection, future vocabulary/dialogue/avatar/account-retention references and all existing production/security/Premium requirements. This is an internal implementation review, not an independent audit.

## Personal vocabulary library — October 28

Reviewed foreign save IDs, owner overposting, unpublished-course leakage, simultaneous duplicate saves, client-controlled schedules/XP, SQL wildcard expansion, NUL/oversized search, unbounded lists, stale browser results and referenced media deletion. Session validation and user-row mutation locks, owned joins/deletes, published-only discovery/new saves, server timestamps, unique/FK/check constraints, literal parameterized ILIKE, bounded validation/paging, capacity limit, no XP side effects, no-store and account/generation guards address this slice. Actual PostgreSQL negative/concurrency tests and UI escaping/account tests passed.

Remaining gates: reviewed staff vocabulary and audio attachment/playback, collections/reviews/retention, real browser/device checks, query performance at production scale and all existing security/release gates. This is internal review, not independent audit.

## Zero-cost local speech experiment — October 28

Reviewed audio resource bounds, silent false acceptance, wrong-language/path substitution, expected-answer grammar bias, invented pronunciation scores, native model parsing, worker resource pressure, model-download provenance/ZIP traversal/symlinks and external audio leakage. Trial uses canonical bounded PCM, fixed model mappings, no expected-answer grammar, no cloud fallback, no learner audio/log persistence, one active recognizer/one cached model and fail-closed disabled defaults. Setup checks source/archive hashes and bounded safe extraction; downloaded model files remain trusted-operator inputs, not public uploads. No HTTP route/active lesson grading is exposed.15 boundary cases/full265backend pass; phrase-quality experiment fails and blocks using speech recognition to grade lessons. Native-binary security review, process-level CPU/memory/deadline containment, consented real-voice accuracy/noise evaluation and any eventual session/answer endpoint review remain open release gates. NuGet reported no known advisories for current managed package sources; this is not a native security audit.

### Human-voice feasibility follow-up

No expected phrase/grammar was passed to either recognizer. Four known human positives, four actual different spoken words and four deterministic noise files pass the local Vosk feasibility gate. Source content has pinned hashes/license/credit metadata and remains non-curriculum fixtures. Browser PCM encoder rejects invalid profiles/lengths/NaN, bounds recorded blobs and discards decoding results after AbortSignal cancellation. No microphone permission, voice upload, native worker HTTP endpoint, lesson reward or role mutation was activated. Real browser/accents/short-word/confusable-word evaluation and server session/answer gates still apply; a12casefeasibility set does not prove safe grading for all users.

## Shared hearts and recovery — October 28

Reviewed lesson-switch/reset bypass, simultaneous cross-lesson spending, duplicate answer charges, edited device clocks, forged refill/owner inputs, exhaustion/playback inconsistency, account-switch stale requests and countdown auto-granting. Server account-row transactions, shared constrained/FK-backed balance, canonical existing answer replay, server UTC recovery deadline, authenticated subject-only no-store status and monotonic UI elapsed time/server-confirmed expiry address this slice. PostgreSQL concurrency, expiry/resume, own-account and constraints tests passed. Initial UI boundary delay exposed by test was fixed; it was not waived.

Open gates: simulated refill purchase price/payment/ledger/refund/idempotency rules, Premium entitlement boundaries, persistent speech retries/charges, physical device/account-browser checks and existing production/security gates. No purchase endpoint, client-driven wallet mutation or real payment provider was enabled.

## Owned in-lesson voice checks — October28–29

Reviewed forged correctness/typed-text bypass, current-question/ownership checks, expected-answer leakage/grammar bias, replay/different-audio collisions, duplicate heart/XP writes, concurrent account use, silence charging/quota abuse, multipart field/file overposting, cancelled/late browser results, recording retention and native resource pressure. The new SpeakingCheck route has bounded canonical PCM, authenticated server-derived targets, private snapshots, account locking and atomic ledger/answer/wallet transaction, separate trusted internal answer entry, strict form shape, IP/account quotas, no raw-audio persistence/cloud fallback and one active recognizer. Browser explicit submission and abort/generation/account guards prevent late application and automatic upload. PostgreSQL integration tests use a controlled recognizer; independent native human fixtures exercise actual recognition separately. Neither substitutes for real microphone/device/content checks.

Open release gates: exact phoneme pronunciation is not implemented; short/confusable/accent/noise accuracy needs broader real voices; native security/source review, out-of-process CPU/memory/deadline containment and production infrastructure remain required. Simulated billing/heart purchases, Premium entitlements and all other unchecked security requirements remain outstanding. No absolute security or public-release claim.

## Test-card wallet — October29

Reviewed forged balance/currency/owner inputs, real-card-data overposting, foreign wallet/history access, simultaneous top-ups, duplicate request/different payload replay, blocked-card changes, default-card ownership, decimal rounding/caps, mutable ledger, partial money writes and stale browser/account responses. Explicit DTOs/unmapped JSON rejection, subject filtering, active-session user locks, atomic decimal updates/version tokens, operation replay records, SQL checks/composite FKs and append-only ledger trigger cover this slice. Eight focused PostgreSQL cases include an injected ledger-insert failure proving balance/audit/replay rollback. Browser pending-operation reuse, saved-versus-reload-error distinction, input escaping and account/form/late-response cleanup are tested.

Outstanding financial gates: actual simulator debit/refund/price/order/entitlement concurrency and immutability, payment-status recovery, receipts/renewals/jobs, billing-admin permission/MFA tests, wallet retention/incident restore and production DB least privilege/trigger-owner hardening. The simulator has no real credentials or monetary value; wallet delivery does not complete billing or user-requested heart checkout.

## Simulated heart checkout — October29

Reviewed price/heart/owner overposting, foreign cards/results, stale versioned offers, zero-balance/blocked/wrong-currency cards, free-recovery race, duplicate/different-key concurrency, replay after subsequent spending, orphan/failed/mismatched debits, NULL failure-code CHECK behavior, destructive rollback, lost HTTP responses, reload auto-payment and stale cross-account metadata. Server offer5GEL, active-session user locks, atomic debit/refill/audit/evidence, unique request and ledger links, restrictive same-owner/card FKs, append-only evidence and deferred debit consistency checks address this slice. Explicit non-null failed reasons have a separate tightening migration and actual SQL rejection test. Down migration preserves existing purchase evidence by refusing destructive rollback. Browser status-before-retry, owner-scoped non-secret recovery metadata, no auto-POST on reload and account/late-response guards are verified.

Remaining gates: subscription/real simulator payment attempts, refund/corrective-entry policy including spent heart products, Premium UnlimitedHearts, billing-admin/MFA, full financial history, verified backup/restore/incident paths and production least-privilege/security checks. No real payment or external account/data transfer occurred.

## Subscription purchase review — October29

Self-review covered forged price/plan/owner inputs, foreign cards/orders/payments/receipts, price retirement/quote rewrites, same/different-key races, one-active/one-success constraints, failed/technical/expired attempts, orphan/mismatched debits and missing artifacts, partial money/subscription/receipt writes, expiry/free downgrade, stored-versus-effective hearts, incorrect grading/XP, lost-response/reload/Pending handling and stale account/receipt routes. Versioned immutable quotes, owned locked transactions, constrained decimal models, deferred artifact checks, server plan/period entitlements and tested recovery address this slice. A historical failed-payment replay incorrectly picked up a later successful order's subscription ID during review; fixed to associate a subscription only with a successful attempt, with an explicit regression test. This is self-review, not an independent external audit.

Still required: admin/refund/event same-subject enforcement for future writers, renewal/scheduled-change and notification paths, full financial history, catalog administration/version overlap rules, independent sensitive-flow review, verified backup/incident restore, production least privilege/security scans and physical device/a11y checks. No public release or attack-immunity claim.

## Subscription preference review — October29

Reviewed foreign/expired target IDs, replay against replacement periods, changed payload/key reuse, duplicate/no-op events, current-period shortening, enabling while canceled, blocked/foreign/wrong-currency cards, stale scheduled prices, partial flag/event/audit/replay writes, unknown network outcomes and stale account forms. Active owned target checks and user locks, bounded canonical replay, strict DTOs, existing composite card FKs, one transaction and authoritative client reload address this slice. Deliberate replay-insert failure rolls back the earlier save too. Review also fixed opt-out with an already blocked default and old recurring consent following a fresh manual purchase. This is self-review, not an external audit.

RenewalProcessingAvailable remains false and is visible in the product. Future processor work must enforce cancellation precedence, UTC due periods, idempotent unique jobs/orders, current ownership/currency/consent and notification/refund rules; saved preferences are not evidence that automatic renewal exists. Management/IP rate-limit behavior near renewal cutoff, durable recovery, admin event-subject constraints, physical-device/a11y and production/security gates remain open.


## Renewal processor and inbox self-review — October29

Reviewed unapproved saved consent becoming active on deployment, canceled/expired opt-out races, newer subscription targeting, settings quota near cutoff, duplicate workers and commit-response loss, delayed catch-up charging, replacement prices, blocked/missing/foreign/currency/funds checks, partial period/debit/job/notification writes, failed-job recharging, rewriting evidence, account suspension/unconfirmed email, cursor tampering/time zones, foreign inbox/read IDs, escaped message content and stale account responses.

Mitigations in this slice: default-disabled processor, fresh subscription-bound consent (no migration backfill), user-first SQL locks and reread state, late opt-out with no business-quota block, cancellation precedence, bounded15-minute default grace, exact selected price versions, unique period jobs/orders, shared payment artifact recorder, deferred consistency constraints and immutable job evidence. Successful artifacts/period/event/audit/notice commit together. Completed declines/skips clear consent and expire without extension; uncommitted infrastructure rollback can recover safely. Private bounded inbox queries scope every read/write to the JWT subject; cursor dates normalize to UTC, read timestamps are idempotent, Angular escapes text and discards cross-account/destroyed responses. System jobs do not impersonate a learner session or send email.

This is a separate self-review pass, not an external audit. Final checks are recorded in PROJECT_MEMORY.md. Production activation, independent sensitive-flow review, privileged refund/admin authorization, catalog/version overlap enforcement, complete financial history, restore/incident exercises, security scans and physical-device/accessibility checks remain release gates. Defaults/process availability are configuration state, not a guarantee of uninterrupted processing or attack immunity.


## Financial/subscription history design review — 2026-10-30 (before implementation)

Scope: owned history of successful ledger movements plus declined/pending payment and heart-refill attempts, card/type/status/date filters, linked original orders/receipts, and owned subscription/event history. Declines must be query projections with null balance-after, never fake debit ledger rows. Union branches must independently enforce account/card ownership; no foreign-card count or order/reference leakage. Use bounded validated pages, UTC dates, deterministic time/ID/source ordering and no-store responses. Receipt links are only offered for successful subscription payments. Client filters/page/cross-account requests need generation guards and escaped labels. Ended/refunded history must remain readable to its owner, independent of current Premium entitlement. New read-only supporting endpoints stay separate from original122 operations. Source/integration checks must exercise foreign resources, failures without balance change, filter combinations, pagination, current/ended periods and delayed account responses before marking completion. Refund mutations/admin access remain a later reviewed slice.


## Simulated refund/billing administration design review — 2026-10-30 (before implementation)

Unattended recommended defaults under the user's explicit authorization: only current SuperAdmin or Admin with explicit BillingManage user claim may access financial administration. All require active confirmed MFA-backed sessions; refunds and permission grants additionally require a fresh<=5-minute MFA sign-in and current password. No actual learner receives a role/claim from agent tools. Use existing identity claims, grant/revoke only by SuperAdmin, rotate target stamp/revoke sessions, and clear old billing delegation if Admin role is removed.

Refund policy: allow positive2-decimal partial refunds up to the remaining successful-payment amount. Preserve the original payment/receipt; append refund/credit/event/audit/notice atomically. Credit only the original card even when blocked. A cumulative full refund of the subscription's current paid period terminates that period and renewal without altering learning progress; partial refunds and refunds of older periods do not cancel a different/newer paid period. Deny self-approved refunds. Recommended cap exception:10000 remains the user top-up ceiling, not a reason to trap a legitimate refund; refund credits can restore balances above it, while top-ups still cannot. Document this explicit policy refinement instead of silently capping/loss of credit.

Threats: missing/stale permission, teacher/author/reviewer/moderator access, revocation races, ID enumeration, password logging/storage, old consent, duplicate/changed-key/concurrent refunds and over-refund, blocked/wrong-card credit, overflow/decimal rounding, fake/missing refund artifacts, historical-period revocation, global preference targeting, partial writes, stale admin panes and unknown network results. Serialize administrative role changes/refunds using existing advisory guard and deterministic actor/customer locks; independently lock original payment for aggregate SQL cap enforcement, tie refund/credit/event/audit subjects and make evidence immutable. Read joins/minimal DTOs/no-store/validated bounded pages. Keep passwords only in transient form/request memory, never snapshots/logs/storage; uncertain mutation recovery must GET status before any identical retry. SMTP remains disabled. Verify meaningful PostgreSQL/multi-role/MFA/replay/concurrency/rollback/UI cases before checking scope. Production hardening/recovery/external review remain gates.


### Plan catalogue read and price-window foundation —2026-10-30

Catalogue reads require the same current database financial-role/delegation/MFA checks as the billing desk, bounded filters/pages, no-store and a consistent plan/price/feature snapshot. Server writes remain unavailable. Review checks cover unauthorized roles, lost MFA, inactive/public separation, complete history and stale/cross-account responses. No learner data belongs in the catalogue DTO.

Before enabling catalogue mutation, enforce one effective price per plan/period/currency directly in PostgreSQL, including concurrent inserts and boundary updates. Use half-open timestamp ranges: adjacent periods are valid; overlaps are rejected; different currencies/periods/plans remain independent. Retain immutable price identity/amount/period/currency/start and immutable quote snapshots. A new index includes currency to permit legitimate simultaneous currency offers. The bundled trusted btree_gist extension belongs to migration setup, not a runtime application privilege. Refuse a downgrade that would lose simultaneous-currency semantics; do not delete/rewrite pricing evidence or drop a potentially shared extension. Test on PostgreSQL before local migration. Future mutation work still needs fresh proof, audit/idempotency, concurrent revisions, historical feature protection and explicit renewal consent; a database price constraint alone does not complete catalogue administration.


## Placement access review — 2026-11-02

Self-review by the implementation agent; not an independent audit. Threats: forged starting unit/score, reading another learner’s answers, bypassing arbitrary lesson prerequisites, collecting skipped XP, duplicate/concurrent attempts and applying stale responses after account changes. Mitigations: active/session-stamp user lock and course lock at start, authenticated owned result/apply, no client unit/score fields, immutable private mapping/answer snapshots, one open diagnostic per course,24hour new-attempt limit, explicit idempotent apply and single audit event. Access grants are limited to prior teaching IDs in the captured mapping, not live client-supplied IDs or a blanket level flag. Existing unit sequence remains enforced within/after the target. SQL course/unit FK and zero-XP/apply-state constraints complement service checks.

The UI discards late owner/route responses and applies only after a click; no credential, speech upload or financial operation is involved. Tests cover foreign404, incomplete409, correct/incorrect scoring across allfourlanguages, source/private omission, retake/concurrent resume, zero rewards/hearts/progress, within-unit denial and SQL cross-course/XP rejection. A short sampled diagnostic is not a certified language assessment. No guarantee of learner proficiency or complete attack resistance. Future larger diagnostic banks, linguistic validation, production review/recovery and other release gates remain required.

## 2026-11-03 — Weekly ranking privacy and account context

WEEKLY_RANKING.md records server-owned enrolled-course scope, current confirmed/active opt-in filtering, no email/private-ID/individual-result fields, bounded pages/no-store/rate limits, parameterized SQL and explicit global/history participation notice. Narrow participation setter locks the user/family and preserves unrelated preferences. Current opt-out applies to historical reads too. Initials avatars avoid exposing unreviewed media URLs; uploaded avatars remain pending.

Sensitive-flow code review found an account-switch race in the shared client interceptor: a pending token acquisition/401 refresh could dispatch an earlier UI intent with a newly signed-in account token. Fixed by binding signed-in requests and private responses to the starting user identity. Four regression cases cover pending token, pre-refresh identity change, pending refresh, late response and valid same-owner refresh. Risk was cross-account intent replay, not a server authorization bypass. Server permissions/session checks remain primary. This local fix/test review does not fulfill independent review or all SEC release gates.

## 2026-11-03 — Achievement award flow review
Implementation self-review, not independent certification. Awards derive exclusively from owned persisted lesson/activity/vocabulary evidence. GET is private/read-only; POST rejects client subject/progress fields and requires current user/family under the same account lock used by lesson completion and vocabulary saves. Composite award key prevents duplicates; award and notification share the parent transaction. Practice/placement and incomplete/no-graded sessions cannot create perfect-lesson awards. Future daily rows do not extend historical streaks. Explicit historical collection avoids writes on navigation. Frontend generation/owner guards discard stale responses; escaped interpolation prevents stored title HTML execution. Five dedicated PostgreSQL cases cover concurrency, authorization and relevant constraints. Full sensitive-flow independent review and production release gates remain outstanding.

## 2026-11-03 — Challenge reward flow review
Implementation self-review; independent sensitive-flow/security release gates remain open. Threats include client XP/course spoofing, reward recursion, repeat/concurrent lesson farming, another account's progress, expired-window rewards and partial commits. Mitigations: no client award endpoint, owned first-completion ledger only, UTC inclusive/exclusive bounds, user/family lock inherited from completion, composite state key and unique XP source, atomic lesson/state/ledger/daily activity/notification transaction. Queries are private/no-store/rate-limited/bounded and parameterized. Worker creates catalog only, never user rewards, with stable IDs/insert-on-conflict; no user data or secret details logged. Six PostgreSQL tests cover the critical award paths including forced rollback/retry. Frontend escapes titles and rejects stale owner responses. New general collection no-store header also covers achievement POST responses.

## 2026-11-03 — Staff reward-definition review
Self-review by implementation agent, not independent certification. Global reward management denies non-admin roles and rechecks active/confirmed/MFA-verified session/current database roles. Writes lock actor then catalog row, require revision and reason, preserve earned badge definitions and reject changes/cancellation after challenge start. Award readers now take shared definition locks so an edit cannot race recognition and rewrite earned semantics. Codes remain unique after retirement; active challenge exact windows are unique; worker conflict handling preserves explicit staff schedules. Archive metadata retains learner/history evidence. No user award-grant endpoint or client XP/subject fields introduced. DTOs are bounded/private/no-store, inputs constrained/rate-limited and SQL parameterized. Audit and data change commit together; duplicate/stale writes return conflict. Tests exercise disallowed roles, MFA/session changes, concurrent edits, archived earned visibility, future/started challenge rules, validation, conflict recovery and real collection/edit races. General independent review and production gates remain required.

## 2026-11-04 — Classroom isolation review

Implementation review verified cross-class access denials, MFA and current session checks, body-only invite codes, hash-only storage, atomic rotation and concurrent last-use enforcement. Read DTOs hide roster and invitations from learners. A concurrent restore and join in separate classes may exceed the learner class cap because restore does not lock the target learner. This is a remaining capacity-rule issue for the classroom follow-up. Independent security review remains pending.

## 2026-11-04 — Assignment submission review

The learner account and classroom are locked in the same order as class mutations. The server checks a current session, active membership, lesson assignment, course, session ownership and a graded answer. Learning and practice session scores are computed by the existing learning flow. Submission accepts only a session identifier. The composite foreign key enforces learner ownership at the database boundary. Receipts are unique per assignment, learner and session. The deadline and lesson rules become fixed once work is shared. Assignment details show only the requesting learner's available and submitted work. Teacher results and feedback require separate scoped access and review.

## 2026-11-06 — Account deletion review

Implementation review. Deletion uses signed-in request verification, password
and MFA proof and the existing administrator lock order. It serializes erasure
with ordinary account mutations and protects the last Super Admin. A database
trigger rejects new records or ownership reassignment to a deleted account. Anonymous structural
history remains, with financial evidence protected by its existing triggers.

Known email/name matches in retained refund notes block deletion for privacy
review. Published content, shared feedback, old audit text and stored-media
attribution still need a field-level retention review. Staff transfer paths and
unused-upload cleanup need a signed-in walkthrough. Independent sensitive-flow
review remains a public-release gate. These open items keep REQ-028 and API-011
partial.

Vocabulary withdrawal follow-up. Ordinary edits that retain their owner no
longer take a second owner lock in the database trigger. This preserves the
actor/course lock order when an admin publishes another author's work. A
concurrent author withdrawal and admin publication must have one successful
transition, with the other receiving a conflict. Private child records removed
by deletion cannot be recreated through a late insert or ownership change.

Private lesson erasure uses actor and course locks in the same order as
publication. The version ID bounds the action, and the database constrains an
erased version to archived/unpublished state. Tests cover concurrent publication,
ownership denial, old learning history with missing publication metadata and
retention of another staff member's unfinished work.

Explicit unused-media cleanup shares the worker's reference checks and bounded
object deletion. Live MFA and actor/language ownership precede media locking.
Repeat-after-removal responses require administrative access or an owned audit
record. Storage failure leaves the durable Deleting row. Concurrent requests
produce one completion audit. Twelve focused storage/deletion tests passed;
independent sensitive-flow review and browser walkthrough remain release gates.

The pre-deletion responsibility checklist takes identity from the current session,
ignores caller-supplied account identifiers and sets private/no-store. It returns
category messages without student names or content text. A revoked session is
denied. The deletion action reruns the same gates under its user lock, so clearing
a checklist earlier cannot authorize deletion after new work is created. Password,
MFA, last-administrator and billing-retention checks remain enforced separately.

Guest trials expose only a bounded set of current published multiple-choice
questions and introductory wording. Grading keys remain server-side until
feedback for a submitted option. Course/version/question/option membership is
checked on every grading request. Requests are rate-limited, use read-only
behavior and cannot award XP, consume/refill hearts, create accounts or write
authenticated learning history. Changed/hidden publications reject further
checks. Trial response caching is private/no-store. Browser progress is temporary
and discarded after route changes; stale responses cannot replace a newer trial.

Lesson-result continuation consumes only the current owned progress map. It
cannot unlock steps or award XP and uses existing server start/practice checks.
The result panel binds to the account present when created, ignores late
responses after account/session changes, and clears its recommendation before
refresh. Duplicate clicks cannot initiate repeated requests from the panel.
Saved-session URLs remain subject to the normal server owner check. Three
PostgreSQL regressions reverified starts, prerequisites and free-practice rules.

Receipt recovery performs reads only; it cannot resubmit payment. Missing/foreign
payments use a generic unavailable response, while network/server failures offer
retry. Original order details are read through the existing owner-scoped API.
Receipt/order state and pending responses are discarded after account/route
changes. The client not-found screen never echoes path/query data and grants no
access to protected routes. Production HTTP error handling remains a separate gate.

Weekly summaries take the account from the validated session and expose only
owned aggregates, never grading keys or raw submitted answers. A repeatable-read
snapshot and parameterized time-zone conversion keep totals consistent. Week
input is limited to 0–12; future events are excluded. Ungraded speaking and
placement cannot inflate graded accuracy. Responses are private/no-store, and
the page discards data after account changes. Reading a report creates no XP or
progress. Practice is an explicit action through the existing protected endpoint.
Tests cover foreign-query isolation, revoked sessions and DST boundaries.

Completion cards are private by default and generated only from server-owned
completion records. Stable verification references are separate from public
sharing capabilities. Public access requires explicit consent, a random current
share code and an active/confirmed owner. No private identifiers or grading keys
appear in its payload. Revocation removes the share code; re-enabling rotates it.
Revision checks prevent stale tabs from re-enabling a card after revocation.
No-store/noindex/no-referrer reduce retention, without promising to erase copies.

Issuance and account deletion use the existing learner lock/transaction. Unique
user/unit rows prevent duplicate cards; SQL guards protect snapshots/revisions
and deleted-account inserts. Deletion erases the card and public capability.
Export omits sharing capabilities. Public/owner UI ignores stale responses and
requires reloading after uncertain sharing writes. Nine focused PostgreSQL cases
and full backend/frontend regressions passed. Native browser, clipboard/print
and independent sensitive-flow review remain release gates.

## 2026-11-11 — Avatar and dialogue self-review

Profile uploads require a live authenticated session and exactly 65,536 RGBA
bytes. The server generates a new PNG and stores it under a server-assigned
private key. PostgreSQL rejects cross-owner avatar assignments. Replacement
and removal clear the active reference and queue the old object for deletion.
A previously issued link can remain usable until cleanup or its two-minute
expiry. The signed URL must stay out of public DTOs, logs and leaderboard rows.
Disposable PostgreSQL and object-storage tests verified upload, ownership,
replacement, removal and old-link denial after cleanup.

Dialogue starts check current course publication and learner enrollment.
Read and choice operations scope sessions to the caller. The server locks the
learner while recording a choice and validates it against the current node
in the saved graph. Future branches, scoring flags and feedback stay private
until chosen. Database constraints reject cross-graph references and duplicate
request keys. Integration tests cover foreign-session denial, concurrent
retries and snapshot preservation after content edits.

This is a project self-review. Signed-in browser walkthroughs and the
separate release review of sensitive flows remain open.

### Shared leaderboard photo review — 2026-11-17

The enrolled viewer receives a signed, private no-store link only for a
ranked participant whose current ready upload matches explicit consent.
Anonymous and unenrolled accounts cannot read the board. The query leaves
private account IDs out of the response. Withdrawal prevents
new links, while links already issued expire within 30 seconds. Test with
disposable PostgreSQL and private object storage verified delivery and denial.

### Deleted targets in public-name reports — 2026-11-18

Public-name receipts could retain a deleted target's former name in text
submitted by another account. Deletion now redacts the corresponding report
comments, resolution text and target identifiers in its SQL transaction.
Open cases close before their targets disappear. Name-report creation and
account deletion share the same global/user lock order, avoiding a late
report restoring erased text. PostgreSQL tests verified the stored result,
reporter view, and concurrent retry. Other retained free-text references
remain in the account-deletion review.
