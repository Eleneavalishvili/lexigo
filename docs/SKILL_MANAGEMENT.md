# Course skill management

Administrators open **Lesson workshop → Course skills** at `/manage/skills`.
The screen keeps LexiGo's existing fonts, colors and shared controls. It
supports course selection, name/category search, 10/20/50/100-row pages,
creation, editing, archiving, restoration and reloading after a failed request.

## Access and integrity

- Reads require an active, confirmed MFA staff session. Authors and reviewers
  can read only skills in their assigned target languages; administrators
  can read all courses. Learners and anonymous callers cannot use this API.
- Creation, editing and visibility changes require Admin or SuperAdmin.
  The server checks the current session, account status and roles again.
- Names are unique within a course, including archived records. Course ID
  cannot be changed through editing. Names/categories have 100/40-character
  limits and control characters are rejected.
- Editing, archiving and restoring require the selected revision and a
  reason. Concurrent edits cannot overwrite the accepted change. Each
  accepted change records an audit entry in the same transaction.
- Creation locks the course before checking its 500-skill catalog limit.
  Archived history counts toward that limit, bounding the workshop's existing
  skill chooser as well as the new paged screen. Pages are limited to 100.

## Archive behavior

Archiving retains the skill row, exercise associations and learner progress.
It prevents new manual attachments in the exercise editor; an existing tag
can be kept or removed when editing that exercise. It cannot be newly added
to another exercise while archived. Restoration makes it available again.

Existing published lessons and saved session snapshots retain their tracking.
The established default category tracking for untagged exercises is preserved;
archiving does not silently rewrite old learning material or scores. Custom
topic categories, such as Travel, can be used for explicitly tagged exercises.

Migration `20261120085844_AddSkillManagement` preserves every documented Skill
field and adds `IsArchived` and positive `Revision`. Downgrade refuses to
discard archive/revision history once it has been used.

## API

| Operation | Contract |
| --- | --- |
| GET `/api/v1/manage/skills` | Course, search, includeArchived, page and pageSize; returns items/page/pageSize/totalCount |
| GET `/api/v1/manage/skills/{id}` | Private detail; hidden outside the actor's language scope |
| POST `/api/v1/manage/skills` | Administrator creation; 201 and a readable private Location |
| PUT `/api/v1/manage/skills/{id}` | Name/category update with revision and reason; 200 |
| DELETE `/api/v1/manage/skills/{id}` | Archive with revision and reason; 204, preserving history |
| PUT `/api/v1/manage/skills/{id}/visibility` | Archive or restore with revision and reason; 200 |

The implementation uses focused controller, service, contracts, API client
and screen files. Original model fields and foreign keys remain intact.

## Verification

The first three PostgreSQL cases verified creation/detail links, scoped
reads, write denial, bounded search/pages, concurrent duplicate/edit handling,
archive preservation and restrictions on newly attaching archived tags.
The initial wider publishing/learning/schema run passed 131 tests.

After the browser check found a native search-form reload, the form was
corrected and a regression test added. All 15 targeted frontend checks passed.
The final desktop/phone skill journey passed both projects (46.8 seconds),
including create, edit, archive, page reload, restore and search:
work/learner-browser-35bd9234b0824a718d4a7ecb99754f9a.

The final wider skill/learning/schema run passed 132 tests in 5m32s, including
concurrent creation at the 500-row course limit and guarded downgrade:
work/test-results-20261120/final-skills-and-learning.trx. A later 16-case catalog
run also verified the separate archive request contract and the reward API
updates. content-catalog-verified.trx passed all 16 final checks, including
trimmed-reason validation. The migration is applied locally. Expanded staff
accessibility checks passed both desktop and phone projects after fixing
tablet navigation wrapping. These checks do not complete the remaining
generic resource-management requirements.
