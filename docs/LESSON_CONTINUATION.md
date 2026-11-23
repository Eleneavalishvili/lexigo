# Continuing after a lesson

Completed lesson results offer a next-step panel. After a passed ordinary
lesson, it reads the current server progress map, sorts by unit and lesson order,
and recommends an unfinished unlocked step after the completed lesson. If there
is no later candidate, it can return to an earlier unfinished unlocked step.
Completed and locked lessons are excluded. A saved session is resumed directly;
otherwise the existing server start operation creates or resumes the lesson.

The learner can retry the finished lesson or start free practice from the same
screen. Failed lessons offer those actions without recommending a later lesson.
Course-wide mistake practice uses the course operation rather than an empty
lesson ID. Placement results retain their separate recommendation flow.

These controls do not grant completion, unlocks, hearts or rewards. Existing
server checks remain authoritative. Free practice still consumes no hearts,
awards no XP and does not unlock lessons. Repeated graded lessons retain the
existing best-score and one-time reward rules.

The panel guards duplicate clicks, clears stale recommendations during refresh,
offers retry after read/open failures and discards responses after destruction,
session changes or account changes. A result with no remaining available
candidate describes the currently published steps, not completion of a full
language level. The journey link remains available.

## Verification

The initial 35 frontend cases covered the new panel plus the existing player and
journey. Final focused cases cover ordering, locked/completed exclusion, saved
session reuse, failed-lesson practice, original-lesson retry, duplicate submission,
refresh failure, account changes and empty-ID mistake practice. PostgreSQL checks
reverified private/idempotent starts, prerequisite unlocking and exhausted-heart
free practice without rewards. No backend API or database schema was changed.
Production build and final focused results are recorded in PROJECT_MEMORY.md.
Authenticated browser/mobile walkthrough remains pending.

## Source changes

frontend/src/app/features/learning/lesson-next-step.ts, .html, .scss and .spec.ts;
lesson-player.ts, .html and .spec.ts. The panel reuses LearningApi and existing
server-owned course progress. Requirement, model/API evidence and project memory
track the change without checking unfinished aggregate requirements.
