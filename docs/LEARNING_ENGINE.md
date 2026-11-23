# Interactive learning: first text-lesson slice

## Distinct XP policies — 2026-11-20

New graded lesson completion uses the server's session-saved lesson reward
(20 XP by default). Repeating a completed lesson and free review practice
earn 0 XP under the approved recovery policy. Conversation practice has a
separate policy: the first completed path with at least 80% correct responses
earns 10 XP by default. An unsuccessful path, unfinished path, repeated
scenario or a revised version of an already-rewarded scenario earns no
additional conversation XP.

Super Admin can change the conversation amount from 0 to 1000 in System
settings, with the existing revision/reason/audit controls. Zero pauses new
conversation awards. The amount applies on completion, and stored awards
are never recalculated after a settings change. Legacy settings without
the new field use 10. Repeat/review practice remains 0 XP.

Conversation awards reuse XpTransactions with source FirstDialogueCompletion
and the completing session ID. The account lock serializes concurrent
choices; the service checks previous awards across all versions of that
course/scenario before creating a reward. The final choice, completion,
ledger and local-date daily-goal update share one PostgreSQL transaction.
Conversation rewards do not increment completed-lesson counts or satisfy
the new-lesson challenge. Replaying the same request returns the saved
amount. A reward persistence failure rolls back the final choice so the
same request can safely retry.

The conversation summary shows the persisted amount and current reward
policy. The server supplies both values; the browser does not assign XP.

## Learner behavior

New lesson and practice sessions return 201 and their private detail
URL. Reopening an active session returns 200 with its saved progress.
The URL uses the same account and Premium rules as the normal player.

A first course enrollment returns 201 with the learner's private enrollment
detail URL. Joining again returns 200 and the original enrollment time.
An explicit starting-level update still works. A repeat without a level
keeps the saved starting point. The owner can read the enrollment after
the course leaves the public catalog.

`/learn` lists the signed-in learner's enrolled courses, with paging and an honest empty state. `/learn/:courseId` shows published units/lessons, owned progress, prerequisites and resumable sessions. Explicit joining preserves other courses. Enrollment/history remain stored when a course is archived; existing sessions can still be resumed. Unpublished courses are unavailable to a new learner.

`/learn/session/:sessionId` delivers MultipleChoice and Translation exercises. The server chooses the ordered exercise/version snapshot and exposes only the current question's prompt/options. Feedback appears after an accepted answer, then the learner continues. Responses persist after every answer; refresh restores feedback and the next unanswered question. Save & leave returns to the learner's journeys. Results show server pass status, score, accuracy, counts, earned XP, timestamps/duration and each submitted question's feedback. Incorrect answers reduce lesson hearts. Free practice can repeat an unlocked published lesson without losing hearts or granting XP/progress unlocks.

The other eight documented exercise formats remain mandatory. They are still blocked at publication until their validators/renderers exist. Personal mistake targeting, skill progress, placement, unit assessments, next-lesson shortcut, media delivery, all reward consumers and Premium entitlements remain required.

## Approved starting policy

On October 28 the user delegated the proposed starting numbers: 5 hearts per lesson, one heart lost per incorrect answer, all questions answered with score >= 80 to pass, first successful completion 20 XP, repeated lessons and free practice 0 XP for now. Numeric values are snapshotted on the session. No price/service/provider was activated. Administrator policy configuration and distinct review/dialogue/challenge rewards are still pending; these defaults do not complete those requirements.

Lesson score is correct answers divided by all session questions, rounded down to an integer percentage. Accuracy is correct divided by submitted answers. Passing additionally requires every question, so an exhausted unfinished session at score 80 does not pass. Best score does not overwrite a previous completion; first completion time is retained. A failed session can be completed after hearts reach zero. New attempts receive their own five hearts. Practice cannot mark a lesson completed or unlock dependent lessons.

Text rules use Unicode NFC, trim/collapse Unicode whitespace, optional ordinal case-insensitive comparison and optional removal of final .!?… punctuation and Spanish opening ¿¡ marks. Default: ignore case, preserve punctuation. Authors choose both flags in the draft editor; strict parsing rejects unknown/invalid stored rule fields before publication. Accepted alternatives are private. Curly/keyboard apostrophe typography is normalized without removing apostrophes. Accents, distinct letters and other punctuation within words are preserved; no blind accent removal or ß/ss substitution. Current rules are copied into private evaluation snapshots.

## Access, consistency and privacy

Every API requires an authenticated active confirmed account. Writes obtain/reload the user row and verify current session expiry/revocation/security stamp; existing JWT middleware also verifies roles/MFA. Learning data reads and mutations are owner-scoped; cross-owner session IDs return 404. A learner must enroll and satisfy prerequisites before starting. Previously completed basics remain available. Start locks user then root course, matching content publication order; answering/completion need only the owner lock. Role/status changes and concurrent reward writers must retain compatible ordering.

Migration 20261028065211_AddLearningSessions creates Enrollment, LessonProgress, LearningSession, SessionExercise, AnswerAttempt, DailyActivity and XpTransaction. Normalized relational keys coexist with JSONB exercise snapshots/payloads only. Composite ownership foreign keys link attempts to the same user's exercises/sessions. Positive/range constraints, one open user/lesson/mode session, one attempt/exercise and unique user/reward source are enforced by PostgreSQL.

Start reuses an existing active lesson/mode session. At most 20 open sessions/user and 100 exercises/session; maps are bounded at 5000 lessons and enrollment pages at 20. Requests are limited to 16 KiB, text 1000 characters and eight option IDs; learning has its own 180/minute/IP limiter. Private sessions/caches clear on account changes and late responses are ignored.

Answers use user-scoped UUID idempotency keys. Same key/payload/session returns the stored original feedback, including after later answers; a changed payload/session conflicts. Unknown option IDs, skipped questions, already-answered questions and completed/exhausted lesson writes are rejected before saving. UI retries an uncertain request with the same key; reopening reads durable server state rather than inventing progress.

Completion is naturally idempotent by owned session ID. Result, first-completion progress, XP ledger and timezone-local daily activity/goal are committed together under the user lock. Unique ledger source prevents duplicate first-lesson awards. Achievements/challenges do not exist yet and are not claimed to be included in this transaction; their eventual writers must join it before that aggregate requirement is complete.

No public or learner DTO contains accepted-answer lists, evaluation rules or the private snapshot. Returned feedback is limited to already-submitted exercises. Angular renders explanation text without HTML injection. Private answers are not persisted in browser localStorage; no recording/upload/email is performed by this slice.

## Verification — October 28, 2026

Final backend suite: 137 passed, 0 failed/skipped, using PostgreSQL Testcontainers. The 24 learning/normalization cases cover enrollment/ownership/paging, cross-course persistence, concurrent answer/start/completion, idempotency conflicts, SQL ownership/reward constraints, exact pass thresholds, exhausted free practice, unfinished high-score denial, timezone daily activity, prerequisites and immutable snapshots across source mutation/archive. Staff tests also verify persisted case/punctuation choices. First runs exposed a fixture violating the existing unique language-pair constraint and enrollment record-projection SQL translation; both were corrected before passing verification.

Angular: 58 tests across 15 files passed without unhandled errors; production build and format check passed. New component cases cover feedback/finish, request retry keys, refresh, account switching, exhausted hearts, joining and locked/free-practice actions. Full authenticated learning browser visual/mobile/keyboard validation remains pending reviewed actual curriculum; component tests are not that evidence.

The user-requested live phone-shaped preview runs only on loopback at localhost:4201, framing the actual localhost:4200 website in a 390 × 844 iframe. It is ignored development work, not a product feature. User may interact with it; do not hijack their navigation for unrelated tests. It is a size/layout preview, not proof of Safari/iOS device compatibility. Original retro colors/fonts are unchanged.

Actual curriculum remains empty until authors create and independent reviewers approve/publish lessons. Synthetic test lessons stay inside isolated test databases. No actual learner/staff account was promoted, no external email sent, and no paid/public service activated.

## Four-format and character update — October 28

FillBlank and SentenceBuilder now join MultipleChoice/Translation, with private token order snapshots, ordered-ID submission, strict permutation checks and revision ID remapping. Six exercise types remain required. Original Pip/Sol animated reactions and pause/reduced-motion controls are integrated into the player. See SENTENCE_EXERCISES.md and CHARACTER_DESIGN.md for current evidence and limits; earlier two-format limitations are superseded only by this verified expansion.

## Text matching update — October 28

Text Matching now joins the four earlier formats. Private paired-key snapshots, cryptographic option shuffling, complete-set server validation and canonical row-order retries are verified. Audio matching and five other exercise types remain required. See MATCHING_EXERCISES.md.

## Context and visual-letter update — October 28

Reading, single DialogueChoice and Russian visual Alphabet bring current supported text/visual formats to eight. Context is bounded and snapshotted, while choice grading stays on the server. Listening/Speaking, audio variants and branching dialogues remain required. See CONTEXT_EXERCISES.md.

## Listening and saved audio — October 28

Typed/choice Listening brings supported formats to nine. Reviewed matching-language audio can be attached to owned drafts, copied through revisions and retained by SessionExercise FK/public attribution snapshots. Free learner playback is scoped to current/answered owned exercises, with expiring on-demand links and actual element cleanup. Completed results can replay their original audio. See LISTENING_LESSONS.md for230backend/103frontend checks and remaining licensed-curriculum/device/Speaking/audio-matching/sound-association scope. Earlier Listening-pending notes describe previous checkpoints.

## Guided speaking update — October 28

Speaking is now the tenth named format. It saves self-assessment without a pronunciation score, heart loss or accuracy contribution. Mixed scoring uses graded questions; speaking-only completion earns no XP. Shared assessment logic, neutral journey/results and reward-ledger checks prevent false accuracy or duplicate rewards. See SPEAKING_LESSONS.md. Audio submodes and all existing unfinished requirements remain required.

## Current heart policy — October 28

Active regular lessons now share the account LearnerHearts balance; earlier per-session starting-heart descriptions are superseded by HEART_RECOVERY.md. Completed-session heart snapshots remain historical. Zero hearts starts a fixed one-hour server deadline, after which all five become available. Queries, answer/completion and reference playback use the same balance; new regular lessons cannot bypass exhaustion. Free Practice remains available. Purchase checkout/Premium UnlimitedHearts and the newly selected one-free-speaking-retry policy still require their respective integration.
