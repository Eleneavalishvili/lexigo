# Course placement check

From a course journey, choose **Find my starting point**. An undecided learner is enrolled without inventing a self-reported level. Existing learners can use the same action. A check already in progress resumes; a new one is limited to once per course per24hours.

Placement samples five existing questions per assessed unit. The current
starter courses provide 35 questions each. Units without eligible text
questions stay outside the assessment, including the optional Cyrillic
practice unit. Reading, translation and multiple choice are sampled
across lesson versions with shuffled choices. The result is a
course-specific starting recommendation.

## Scoring and explicit application

The server saves immutable public/private exercise snapshots and a private unit/question/lesson mapping. Existing owned answer endpoints persist each submission and handle retries. Placement runs on the existing Practice engine with a distinct placement flag: no shared heart changes, lesson completion rows, daily-activity rewards or XP. The database also requires zero placement XP.

Complete every question, then view the recommendation. The recommended unit is the first sampled unit below80%; if all units pass, it is the last available unit. A high score in a later unit cannot compensate for a gap in an earlier one. The result page shows per-unit counts and lets the learner keep their current start or explicitly apply the server-computed recommendation. Apply accepts no client score or unit choice and is idempotent by session ID. It adds an audit event once.

Applying grants prerequisite access for the earlier teaching-lesson IDs captured in that diagnostic snapshot. Skipped lessons remain uncompleted and earn no XP; earlier material remains available for study/review. Within the recommended unit, the normal sequence still applies. New content IDs added later are not silently bypassed. Later retests cannot revoke previously earned access, while the newest applied recommendation controls the next-step card. Existing saved lesson sessions and progress are retained.

Earlier unit checks can be attempted when their teaching prerequisites are either completed or covered by applied placement. The current unit’s check still requires its normal teaching sequence. Placement does not auto-complete a check or issue rewards.

## Endpoints and storage

- POST `/api/v1/courses/{id}/placement-sessions` returns 201 with the private learner session URL for a new check, and 200 when resuming an open check.
- Existing owned learning-session GET/answer/complete endpoints run the quiz.
- Supporting GET `/api/v1/me/placement-sessions/{id}` reads the recommendation.
- Supporting POST `/api/v1/me/placement-sessions/{id}/apply` saves it explicitly.

Original LearningSession gains private nullable PlacementMap JSON, RecommendedUnitId and PlacementAppliedAt. No additional entity is introduced. A filtered unique index allows only one open placement per user/course. A composite course/unit FK rejects cross-course recommendations. Check constraints reject placement XP and inconsistent apply fields. The Down migration refuses to discard existing placement history.

Apply migration `20261102123435_AddPlacementSessions`. Use the normal Development `--initialize-database` command with existing User Secrets. The design-time EF factory intentionally uses LEXIGO_MIGRATION_CONNECTION and otherwise a password-free fallback; do not print credentials or mistake that fallback for runtime configuration. Migration generation and offline model checks do not require exporting secrets.

## Verification

- `placement-learning.trx`: all98learning cases pass, including four new language-specific placement cases and all existing132lesson/check steps.
- New cases cover concurrent resume, incomplete apply denial, wrong/first-gap/all-pass recommendations, private mapping/key omission, foreign result/apply404, every-answer reload, zero hearts/XP/progress grants, idempotent apply, retake cooldown, current-unit locks and SQL cross-course/XP rejection.
- `placement.trx` initially ran12cases because the filter also matched eight authenticator-replacement tests. Only four were the new PlacementTests; do not call it12newplacementcases.
- Frontend224tests/62files pass, including explicit apply, resumable incomplete results, stale-account cleanup, join-before-placement and the recommended next card.
- Production build, final backendbuild0warnings/errors, formatting, noEFmodeldrift and offline source-secret scan pass. APIreadiness200 and migration applied locally.

No physical-browser walkthrough or independent diagnostic/linguistic validation is claimed for this slice. The bank currently supports up to12published units and rejects an unconfigured larger course rather than silently ignoring units. A larger configured diagnostic bank, advanced curriculum, listening/speaking assessment and independent quality review remain required as the courses expand. Security/release gates remain open.
