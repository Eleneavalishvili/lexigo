# Course and dialogue authoring — 2026-11-24

The workshop adds actual staff controls, not demonstration-only records. Existing
learner progress, published lesson IDs and started dialogue snapshots are retained.
The original Georgian specification remains the completion standard.

## Course creation

Administrators with an active MFA-verified session can open **Lesson workshop →
Create a course**, select two registered languages and create an unpublished course.
The database permits one course per source/target pair, including archived courses;
an existing pair must be edited or restored. The four supported English-based
courses already exist. Creating a different pair does not generate its curriculum
or advertise that it is ready. No language or course is created automatically by
this release.

`POST /api/v1/manage/courses` returns 201 with an authorized detail Location.
`GET /api/v1/manage/courses/{id}` is language-scoped. The admin-only
`GET /api/v1/manage/courses/languages` supplies the form's choices. Course insertions
use transaction advisory lock 730027030, an existing SQL unique pair constraint,
an audit entry, and server-selected publication state/order.

## Dialogue workshop

The lesson workshop links to `/manage/dialogues`. Authors can create their own
language-scoped scenarios, add up to 40 nodes and up to six replies per non-ending
node, edit feedback and connect replies to other nodes or an ending. The editor
supports optional independently reviewed recordings using the existing media
picker. It does not upload or purchase audio automatically.

The server requires one start, no cycles, no unreachable nodes, at least one
appropriate reply per non-ending node, no replies on ending nodes, and valid
same-language reviewed media. Malformed graphs roll back without replacing the
saved draft. Unreviewed and cross-language media IDs are rejected.

The process is **Draft → Submitted → Approved → Published**, with **Return for
changes → Draft** and **Archive** actions. Reviewers need the target-language
scope and cannot approve material they own, even if they also have an author
role. Only administrators publish. A reviewer does not gain access to another
author's private draft. Staff privileges always require MFA; learner access rules
are unchanged. No real user was promoted during implementation.

Every update uses the saved edit token and locks the course after the actor row.
An obsolete token conflicts rather than overwriting another staff member's work.
New revisions copy the graph into new rows with new node/choice IDs. Published
graphs cannot be edited in place; existing sessions keep their original nodes
and snapshots. Publishing a reviewed successor archives previous published
versions. Removing a dialogue archives it and returns 204.

List responses contain `items`, `page`, `pageSize`, `totalCount`, with server limits
of 1–100 rows per page. Detail keys and correct-reply flags are staff-only. Learner
DTOs retain the existing private answer-key boundary.

Additional routes beneath `/api/v1/manage/dialogues/{id}` are `submit`, `approve`,
`return`, `publish` and `versions` (POST). GET/POST collection and GET/PUT/DELETE
detail implement the documented resource operations.

## Privacy and migration

`AddDialogueAuthoring` extends the existing Dialogue entity with owner/reviewer
references, review date/comment and an optimistic edit token. Existing built-in
versions retain nullable staff provenance; no reviewer is invented for them.
Its downgrade refuses to remove staff authorship or review history. Application
rollback should retain the additive schema rather than discard new workflow data.

Unfinished dialogues must be published or archived before account deletion.
Archiving unpublished work releases optional media links so unused uploads can
be removed normally. Confirmed deletion erases archived unpublished dialogue
titles, scenario labels, node/reply text and feedback. Published authorship and
relational history remain. Reviewer free-text notes are erased on account deletion.

## Verification

- Course creation: three PostgreSQL HTTP tests cover admin enforcement,
  scope-hidden detail, duplicate/invalid language pairs, concurrent insertion,
  audit count and private initial state; `course-recovery.trx` also includes
  seven account-recovery tests (10 total passed).
- The focused authoring/content run `authoring-content-final.trx` passed 15
  cases, including all four whole starter-course journeys and vocabulary
  preservation. This preceded the additional full-alphabet recognition slice.
- `foundations-authoring-final.trx` passed 11 cases, including every new grammar
  question, all 33 written Russian letters, draft ownership/independent review,
  stale-edit rejection, invalid graph/audio rejection, concurrent course creation
  and archived-draft text erasure.
- Fourteen desktop/phone browser journeys passed for course creation, dialogue
  create/edit/reload/submit, grammar notes and the actual password-reset flow.
  Subsequent pagination/privacy changes require the final release run.
- Frontend: 393 tests/102 files passed, including recovery pending/failure and
  duplicate-submit states. The production build and lint passed before final
  release reconciliation.
- The first whole-backend attempt revealed that an older skill rollback test
  removed unrelated newer migrations from its shared fixture. It was stopped
  after the diagnosed failure. The test now executes only its own downgrade
  script inside a rollback transaction; that earlier run is not a pass.

The final regression/hosted/public verification results are recorded in
PROJECT_MEMORY.md and the release follow-up. None of the above establishes
independent native-speaker approval or completion of every PDF requirement.
