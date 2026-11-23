# Practice saved mistakes

The enrolled course journey shows **Review the tricky bits** and **Your skills map**. Review covers unresolved supported questions, including listening and exercises with reviewed recordings. Choose **Practise my mistakes** for the course or the review button for a specific skill. Batches contain up to ten questions and use no shared hearts, award no XP, and do not complete or unlock the original lessons.

## What counts as unresolved

Only the learner’s own graded answers from completed learning/practice sessions are considered. Placement is excluded. For each source ExerciseId, the latest completed outcome determines whether it remains unresolved; all historical incorrect outcomes determine the displayed miss count. A later correct answer in a completed session removes that source question from this list. Original incorrect answers/results remain unchanged. A correct answer in a still-open practice batch does not clear the list yet.

Results are ranked across the full owned history before paging: most frequently missed first, then older misses. The query does not silently discard older errors through a recent-history cutoff. Exact-time conflicting outcomes conservatively retain the incorrect one. Current selection is by source exercise, not semantic concept or skill mastery. A question in a new content version is a different source ID.

## Saved-version safety

Practice copies the original public/private question snapshots into new owned SessionExercise rows. Public question IDs are replaced; options are shuffled while their IDs and private matching/token keys remain consistent. Later source answer edits cannot change the saved evaluation. Public notes from the source sessions remain available for teaching; explanation content is bounded at20,000characters without slicing it mid-note. Available alternate exercise types interrupt runs of more than two of one type; a single-type candidate pool necessarily repeats that type.

The start transaction uses the existing user and course locks, requires enrollment in a published course, and resumes an open batch for the same scope. Saved exercise and option audio references are carried into the new session only if all referenced recordings are reviewed and ready for the course language. The service locks and rechecks selected recordings before creating the session. Playback uses the existing owned-session endpoint and rechecks review status each time it issues a short-lived link.

## API

- Original GET `/api/v1/me/mistakes?courseId=...&page=1`: owned20-row pages, exact total, private/no-store; no answer keys.
- Original POST `/api/v1/me/practice-sessions`: existing `{ lessonId }` remains supported; new `{ courseId, mistakesOnly: true }` starts a targeted batch. Mixed/ambiguous requests are rejected.
- Existing owned learning-session GET/answer/complete endpoints resume, grade and complete the batch.

Both operations accept an optional integer `skillId` for the selected course. Filtering uses the original skill snapshot, with an exercise-format fallback for older history. The GET accepts `includeDue=true` to return scheduled questions as well. Its default still lists unresolved mistakes only. Practice fills the batch with due questions after unresolved mistakes, keeping exercise variety within each group.

The due date starts 24 hours after a correct answer. Correct practice repetitions since the latest mistake extend the interval to 3, 7, 14 or 30 days. A new mistake puts the exercise back in the unresolved group and resets its repetition count. The app shows the due date with the recommendation. The schedule uses elapsed UTC hours and completed sessions only.

Unresolved questions from skills with more incorrect than correct answers move to the front. When a question has multiple matching skills, the highest recorded miss rate determines its priority. Variety works within the weak, other unresolved, and due groups, in that order.

Within equal weakness and miss-count priority, fewer than 60% correct answers favors easier unit levels, while 80% or above favors harder unit levels. The calculation uses the latest ten graded answers from completed sessions in the course and requires at least five. Middle scores keep the usual date ordering. This only orders eligible saved questions and leaves the original exercises and grading intact.

## Verification

`mistake-learning.trx`:101learning tests pass (previous98 plus3new review scenarios). New checks cover foreign-course/session denial, empty states, concurrent resume, original snapshot grading after source edits, miss counts, correct-answer resolution only after completion, preserved source score/XP/history, unchanged shared hearts, no original completion/unlock, pagination beyond the last page and bounded10-question batches. Source changes only occur in isolated test databases.

Frontend235tests/63files and productionbuild pass. Four new component cases cover explicit action/course-only payload, empty state, escaped prompts/server rejection and stale-account cleanup. APIbuild0warnings/errors, format/noEFdrift and offline source-secret-scan pass. No physical-browser walkthrough or independent adaptive-learning validation is claimed. Full project security/release and all other curriculum requirements remain open.
