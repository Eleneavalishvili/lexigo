# Reading, dialogue choices and visual alphabet — October 28, 2026

## Current scope

Eight types now support the author/reviewer/publisher/learner flow: MultipleChoice, Translation, FillBlank, SentenceBuilder, text Matching, Reading, DialogueChoice and Russian visual Alphabet. Listening, Speaking, audio matching and sound-based alphabet practice remain mandatory. DialogueChoice is a single contextual exercise; it does not replace the separate branching café/hotel/tickets/directions modules or Premium AI conversations.

REQ-047 (single dialogue-response selection) is checked with API/component evidence. Reading/listening and alphabet/sound requirements remain partial. Actual course curriculum, full browser/device checks and all other pending requirements remain required; no whole-project completion claim is made.

## Context and publication

Migration 20261028075441_AddExerciseContext adds nullable Exercise.ContextText, varchar(4000). Existing exercises remain null, preserving earlier content. Authors enter a passage or situation separately from the English question and feedback. Reading and DialogueChoice require nonblank context before submission/publication, 2–8 distinct choices and valid private accepted answers. The staff form requires context before saving; the API may save an incomplete draft but cannot submit it. Context over 4000 characters, embedded NUL or irrelevant nonblank context on another type is rejected. Several Reading exercises can share the same passage to ask multiple questions.

Context is public learning material, not an evaluation key. It is separate from EvaluationRules. A revision copies context, then may edit it without changing already-started sessions. Session public snapshots and completed question results retain their original passage/situation. Older snapshots without this field deserialize as null.

Both staff preview and learner cards render plain escaped text with preserved newlines and long-word wrapping. HTML-looking passages cannot create DOM images/scripts. Questions submit selected option UUIDs; learner requests cannot supply a score or rewrite source context. Choices are cryptographically shuffled once per session and retain that order on resume. All prior owner/scope/MFA/review/state/idempotency/heart/XP rules remain in force.

## Russian visual-letter recognition

Alphabet publication is restricted to courses whose target language code is ru, resolved from SQL relationships rather than hardcoded language IDs. Each choice must be one standard Russian Cyrillic letter (А–я or Ё/ё), with 2–8 choices and accepted keys matching those choices. Latin lookalikes, digits and multi-letter choices are rejected. Ё and Е stay distinct; no transliteration/accent stripping is used for grading. This is visual recognition with English instructions; no recorded audio or speech-accuracy claim is made.

The same native-choice UI and server evaluation are used with a distinct alphabet heading. Audio association remains required when the media pipeline is implemented. The broader four-course alphabet/basics curriculum is tracked separately.

## Consistent format rules

The first alphabet success test caught its publication/revision allowlists lagging behind the newly added validator. The gates were corrected and full tests rerun. ExerciseFormats now centralizes known/supported, contextual and choice capabilities across draft validation, publication, revision and session assessment, avoiding independent lists drifting again. Frontend headings use an exhaustive typed map rather than a growing nested template expression.

## Verification

Final counts and runtime checks are saved in PROJECT_MEMORY and context-final.trx. Integration cases cover Reading/DialogueChoice independent publication, required context, old versus revised passages/keys, completed-result context, invalid answer shapes and wrong-choice heart cost. Actual PostgreSQL enforces the 4000-character bound and retains nullable earlier exercises. Russian cases verify positive Ё publication/assessment/revision, non-Russian-course denial and Latin/digit/multi-letter rejection. Legacy public snapshots without context still decode.

Angular tests cover safe context rendering, required/restored author passages, both choice payloads, Cyrillic choice IDs and existing retry/state/character flows. Full authenticated learner browser/mobile/keyboard/theme validation is still pending reviewed curriculum; component tests and CSS inspection are not real-device evidence.

The real app database still has no published curriculum. Synthetic tests remain in isolated PostgreSQL fixtures; no personal account was promoted and no fictional human review was inserted into the actual application. No new dependency or external service, email, purchase, recording or deployment was introduced. The original retro palette/characters and user's phone preview are preserved.

## Audio association update — October 28

Russian Alphabet now supports reviewed sound-to-letter questions alongside visual recognition. Sound mode rejects same-letter upper/lowercase decoys, preserves Ё/Е and remains Russian-course-only. Actual content/device checks remain required. See AUDIO_ASSOCIATIONS.md.
