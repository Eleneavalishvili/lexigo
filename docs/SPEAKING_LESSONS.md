# Guided speaking self-assessment

## User-approved policy

On 2026-10-28 the user approved ungraded Speaking completion: no pronunciation accuracy score, no heart loss and no contribution to scored accuracy. Mixed lessons retain normal XP based on graded questions; speaking-only practice earns zero XP. All exercises still require completion before a session can pass.

Speaking-only lesson completion records practice progress, with zero XP and no XP-goal activity. It is not a claim of correct pronunciation or skill mastery. A later first successful graded completion may earn the normal first-completion reward; the reward ledger prevents duplicates. Free Practice never unlocks lesson progress or awards XP. The UI labels speaking-only results and journey entries as ungraded, without a percentage.

## Authoring and delivery

Speaking is the tenth supported exercise type. Its prompt contains the phrase/instructions to practise; its explanation supplies reflection guidance. It requires an independently reviewed reference recording for the course language, no longer than 30 seconds. It has no choice options, accepted answers or automatic pronunciation key. The audio picker applies the duration limit; publication/start validation independently enforces it. Incomplete drafts may be stored but cannot be published.

The existing owned/scoped draft, independent lesson review, revision and immutable audio-reference rules apply. Source, license and duration are retained in the saved session. Core speaking self-assessment remains Free. Premium dedicated practice and AI character conversations are separate required features and are not implemented by this recorder.

The learner listens to the reference, records a short clip after explicit microphone permission, listens back and reflects. The local recorder keeps its 30-second/5-MB limits, error states and late-permission cancellation. Opening the reference stops capture/own playback; recording or own playback stops the reference. No recording starts automatically. An unavailable/denied microphone has a clearly labelled practice-out-loud fallback, followed by an explicit self-assessment confirmation.

Clips remain in browser memory. They are freed after listening, discard, replacement, hidden/page exit, account/exercise change and destruction. Actual native players pause, lose their source and unload; tracks, timers and object URLs are released. The standalone `/speaking` page now also explicitly unloads its player when clips/account/page lifecycle changes. This is lifecycle cleanup, not a forensic memory-erasure guarantee.

## Server contract and scoring

The existing answer endpoint accepts optional `practiceCompleted` (default false). Speaking requires true, with no text, choices, ordered tiles, matching pairs, recording or client score. This is an explicit self-report; the server does not claim that a microphone was used or pronunciation was verified. Ordinary graded exercises reject a practice confirmation. Unknown overposted grading fields are rejected by JSON binding.

The owner, current session/family, exercise order, idempotency and completed/exhausted-session checks still apply. Replaying an identical practice confirmation returns the stored response; changing its payload/key meaning is rejected. Speaking saves `AnswerAttempt.IsGraded=false` and `IsCorrect=false`. The latter is a required legacy Boolean placeholder, **not** an incorrect-answer verdict. Feedback and results expose `isGraded=false`, which consumers must check before displaying correctness.

PostgreSQL enforces `IsGraded OR NOT IsCorrect`, so an ungraded row cannot claim correctness. The additive migration defaults legacy rows to graded=true. EF's Boolean sentinel is explicitly true so new false values are actually written rather than replaced by the SQL default. Old response JSON without the new field still decodes as graded.

`SessionAssessment` derives graded exercise IDs from immutable exercise snapshots. Score is correct graded answers divided by all graded questions; accuracy is correct graded answers divided by answered graded questions. Speaking is excluded from both denominators and from heart loss. A wrong graded answer cannot become a pass because other steps were self-assessments. Results include graded exercise/answer counts and the number of saved self-assessments. With no graded questions, numeric score/accuracy fields remain zero for compatibility and the UI displays no accuracy percentage.

The all-exercises-complete rule remains. First graded successful lesson rewards are checked against the immutable XP ledger under the existing user lock, rather than assuming a prior ungraded progress record consumed the reward. No zero-valued XP row or daily XP-goal credit is created for speaking-only completion. Journey `hasGradedScore` is derived in one bounded query from completed graded attempts, with no extra persisted score flag.

Future mistake practice, mastery, achievements, leaderboards, teacher reporting and analytics must respect `IsGraded`. Do not classify a speaking confirmation as either a correct answer or a mistake, or infer pronunciation quality from `practiceCompleted`.

## Verification and remaining gates

- Full backend238 passed,0 failed/skipped (`work/test-results/speaking-final.trx`). Eight new PostgreSQL/Garage cases verify ungraded persistence and SQL correctness constraints, neutral progress, mixed accuracy/XP, self-assessment inflation denial, graded-payload forgery denial, replay behavior, free practice, first graded reward after ungraded completion, legacy JSON and required short reviewed references.
- Frontend111 tests across28files passed. Guided speaking tests cover explicit recording, no automatic completion, playback cleanup, permission fallback, account/exercise changes, reference/capture handoff and destruction. Parent tests cover a confirmation-only payload and neutral results; authoring/journey tests cover no fake answer key or percentage. Existing recorder lifecycle tests remain in the suite.
- An older publication test assumed Speaking was unsupported and supplied a fake answer key. It was updated to test unknown-format denial, missing Speaking reference and missing graded answers. A new journey assertion initially matched the course's unrelated80% threshold; it now checks the ungraded lesson entry directly. Neither failure was hidden or skipped.
- Production build, Prettier and C# whitespace checks passed; the final backend build has zero warnings/errors. No package or external provider was added.

Real microphone/browser/Safari/device tests, real licensed recordings and independent curriculum review remain required. No agent requested physical microphone permission or promoted a real account. Audio Matching, dedicated Russian sound association, adaptive practice, all Premium/AI features and the original release/security/deployment gates remain required. This is not automatic speech recognition or an AI call.

Migration20261028125159_AddUngradedPractice applied locally; database/schema.sql regenerated and EF reported no pending model changes. Runtime live/ready, speaking page and original phone preview returned200. OpenAPI exposes practiceCompleted and isGraded. Actual database:11migrations,4users,0lessons,0media,0sessions. No real privilege grant, microphone capture, outside email, purchase or public deployment occurred.

## Audio association update — October 28

Audio Matching and dedicated Russian sound-question controls are now implemented and tested separately; see AUDIO_ASSOCIATIONS.md. They remain graded formats and do not alter the user-approved ungraded Speaking policy. Real curriculum/device verification and all broader unchecked scope remain required.
