# Personal data export and deletion

## Staff-written notes — 2026-11-20

Confirmed deletion replaces the account's lesson-review comments, report
resolutions, teacher-feedback messages and audit reasons with an erasure
notice. Vocabulary-review comments are cleared. Decisions, timestamps,
other contributors' notes, learner submission IDs/scores/XP and published
lesson authorship remain. Audit notes targeting the deleted user and their
completion cards are also redacted. Moderation and account deletion share
the administration lock order so concurrent decisions cannot reintroduce
identifying card notes.

These changes run inside the existing password/MFA-verified transaction.
A wrong password leaves the notes untouched. They do not delete another
learner's submission history or erase published teaching material.

The final staff-retention follow-up also erases notes written by departing
billing staff for active buyers, including the matching refund audit notes.
Amounts, references, status, fingerprints and ledger evidence stay immutable.
Refund creation still requires an active authorized actor. PostgreSQL permits
only the fixed note-erasure value for an already-deleted buyer or author;
the deferred evidence check continues to require matching credit, event,
audit and notification records. A rollback refuses to remove this support
while deleted-author refund history exists.

Archived classes still owned by the departing account receive generic names
and blank descriptions. Their assignments and copied notification bodies
receive a generic title. Membership, submissions, feedback decisions, scores
and XP survive. Handed-over classes remain with their current teacher and
are outside this ownership-scoped operation. Published learning content and
its required source/license attribution remain retained contributions.

Evidence: final-staff-privacy.trx passed all 31 account, staff-note, Extra Study
directory and refund checks on 2026-11-20, including
wrong-password denial, other contributors' preservation, protected monetary
fields and guarded migration rollback. The deletion-page component test and
production build passed. No personal account was deleted in these checks.

## Export coverage

Profile & Settings → Privacy downloads JSON from GET /api/v1/me/export.
Every section uses the authenticated account ID and the same repeatable-read
PostgreSQL snapshot. Queries stream sequentially to avoid overlapping readers.

The file includes profile and preferences, enrollments, lesson and exercise
history, daily activity, earned rewards, vocabulary collections and reviews,
classroom participation, received feedback, Extra Study requests, notifications,
reports and simulated billing records. Account history adds hearts, speech-check
outcomes, study reminder delivery status and sign-in metadata.

The projections omit password and security material, recording fingerprints,
private grading snapshots, internal retry keys and email transport bodies.
The export also includes the account's authored lessons and versions, written
lesson reviews, uploaded-media metadata, vocabulary drafts, taught classes and
Extra Study offers. It excludes class rosters and other learners' submissions
from those staff sections. Import status/history and summaries of the account's
administrative actions are included. Raw import payloads, internal audit reasons
and other accounts' data are excluded.

### Export reconciliation — 2026-11-18

| Personal records | Export sections |
| --- | --- |
| Account and study settings | Profile, preferences, enrollment, reminder and sign-in metadata |
| Learning | Lesson progress, saved sessions and attempts, XP history, skills, dialogue activity and completion cards |
| Motivation | Daily activity, earned achievements and challenge progress |
| Vocabulary | Saved words, collections, memberships and review history |
| Classes | Own memberships, submissions, received feedback and Extra Study requests |
| Communications | Own notifications and content reports, with reported learners' private IDs removed |
| Simulated billing | Own wallet, subscriptions, events, orders, payments, receipts, refunds and heart purchases |
| Staff contributions | Own lesson/version text, written reviews, uploaded-media metadata, vocabulary revisions, taught classes, offers, imports and administrative summaries |

The reconciled projection keeps private evaluation snapshots, credentials,
transport bodies, raw imports, internal retry keys and other learners'
identifiers out of the file. This records the authenticated account's
documented personal-data export. Account erasure and staff responsibility
transfer have their own acceptance checks.

## Deletion dependencies

The user approved immediate private-data erasure with anonymous historical
billing and published-content authorship retained. Staff must resolve active
teaching responsibilities first.

DELETE /api/v1/me requires the current password, the text DELETE, request
verification and a second-factor code when enabled. The transaction removes
private learning records and credentials and anonymizes the account profile.
An internal user row remains to preserve financial and published-content foreign
keys. Historical XP and daily activity retain the anonymized account reference.

Active teaching, unfinished content revisions and unused uploads currently block
deletion. These gates keep unresolved private material from being left behind.
Vocabulary authors can withdraw an unpublished revision from Vocabulary drafts.
Withdrawn history is removed with the author's private data during deletion.
Lesson authors can erase never-published private wording, exercises and review
notes from the workbench after confirmation. A tombstone and erasure timestamp
preserve version identity. Published material and versions used in learning or
reports reject this action. Unused recordings can then be removed through the
media library. Private lessons containing another staff member's unfinished
work require resolution before owner deletion. An MFA administrator can
hand over a never-published lesson to an eligible author through the
workbench, preserving each version's authorship and the remaining drafts.
This is blocked after publication or learner activity. The server rejects
stale ownership and records a single reasoned audit for repeat requests.
The audio library offers Finish deletion after a confirmed removal request.
This removes the private object before committing SQL removal; storage failures
leave the request available for retry. A synthetic-author integration test
verified that completed cleanup clears the unused-upload account-deletion gate.
Teachers can hand over an active class through its management screen.
The receiving teacher accepts a protected 24-hour link while signed in with
MFA. Existing members, assignments and feedback stay with the class.
Old invitation codes are revoked. Editing or archiving the class invalidates
outstanding handovers. The teacher can also archive a class.
Staff content responsibilities and retained free-text evidence are reconciled
in the 2026-11-20 follow-up above.

The implementation follows these dependencies.

- Refresh sessions and Identity security material must be revoked and removed.
- Queued email must be cancelled before personal addresses are erased. Reminder
  delivery rows reference the email outbox.
- Saved words, word reviews and collection membership form a dependency chain.
- Submissions and received teacher feedback reference learning sessions.
  Speech checks and answer attempts reference session exercises.
- Published lessons, versions, media review and vocabulary revision history
  reference the author or reviewer through restrictive foreign keys.
- Payment attempts, receipts, refunds, wallet ledger entries and heart purchases
  have database triggers protecting historical evidence. Their records must
  retain structural integrity as personal identifiers are anonymized.
- Teacher classes and Extra Study offers may have other participants. Their
  access and data must survive deletion of an unrelated account.
- Privileged account removal must preserve the last active Super Admin rule.

The database rejects new private records for a deleted account. Outgoing email
for a deleted or missing account is suppressed and its queued recipient/body
are cleared. Blocked-account security notifications retain their delivery path.

The self-service endpoint is exercised only with synthetic accounts in tests.
No real account deletion was performed during implementation.

## Responsibility checklist

Public-name reports about a deleted account are redacted inside the deletion
transaction. Their comments, decision text and target identifiers are erased.
Closed reports keep their outcome and creation date. Open reports become
rejected with an account-deleted explanation, so moderators cannot act on
an erased target. The reporter keeps a receipt with generic text.

Name-report creation shares the account-deletion lock order. A concurrent
report retry therefore either finishes before the erasure or is rejected
after it. PostgreSQL tests verified the stored rows and the reporter's API
view. Staff-authored text follows the 2026-11-20 policy above; shared draft
responsibilities use the verified workbench handover and private-text erasure.

### Refund notes and private billing snapshots — 2026-11-18

Confirmed deletion erases refund notes and their corresponding audit-note
text for the buyer's refunds. Amounts, original payment/card references,
status, timestamps and ledger history remain. A private fingerprint retains
the original request comparison, so an authorized staff retry returns the
same refund with its erased note and creates no additional credit.

The PostgreSQL trigger permits only the fixed erasure text after the
buyer's account is marked deleted in the same transaction. It continues to
reject changes to all other refund fields and rejects note erasure for an
active account. The migration backfills fingerprints for existing refunds.
Rollback refuses to discard fingerprints after any note has been erased.
The original text cannot be restored.

The deleted account's private billing-operation snapshots are removed.
Other accounts' snapshots and the ledger remain. This removes the previous
refund-note privacy blocker from self-service deletion.

The deletion page loads GET /api/v1/me/deletion-responsibilities using the current
session. It lists only the account's unresolved classes, teacher listings or
requests, lesson/vocabulary drafts and unused recordings. Links lead to their
existing management screens; failed loading offers retry without claiming the
check passed. This is guidance, not permission to bypass password, MFA, last
administrator or retained-billing checks.

The shared service runs again inside the deletion transaction. A PostgreSQL
test archived the synthetic teacher's class, disabled their listing and closed
an accepted Extra Study request through the normal APIs, then deleted that
teacher successfully. The archived class and closed request remained intact.
Another account's query could not inspect that teacher's checklist. Actual
ownership-transfer controls for classes and shared private lessons are now
available; the final retained-staff-text checks are recorded above.

Completion cards are included in owned export without their active public sharing
capabilities. Deletion removes the cards before other private learning data in
the same transaction, revoking public lookups. Cards do not retain a copied
display name: private/public rendering uses the current active profile. Sharing
audits contain generic reasons rather than names or codes. CompletionCardTests
verified export, deletion, revoked public links and denied late inserts.

AllowVocabularyWithdrawal keeps review history for an active account and allows
a new revision after withdrawal. Rollback refuses to remove the withdrawn state
while such history exists, requiring an explicit data migration first.
