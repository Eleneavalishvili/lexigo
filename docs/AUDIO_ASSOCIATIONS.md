# Audio matching and Russian letter–sound questions

## Authoring

Matching keeps its 2–10 private pairs. Each left-side item may now be text or a reviewed recording, matched to a right-side word/translation/phrase. Captions remain required and distinct for accessibility. Use neutral captions such as “Recording one” when the sound itself is the question. Right-side recordings and a single whole-exercise Matching clip are not supported: attach recordings to the individual left items.

The editor retains the existing text pair fields and adds a recording selector per row. Only one selector is opened at a time. Existing private pair keys restore the correct right text and recording on edit; removed rows lose their references. Recording IDs are not displayed as product labels. Captions/recordings should be checked after reordering rows.

Russian Alphabet now has explicit visual and sound modes. Sound mode uses a reviewed Russian reference and the normal server-graded letter choices. Turning sound mode off detaches the recording. Existing visual case-recognition questions remain supported. Sound choices must distinguish letters: “А” and “а” cannot be competing sound answers. Ё and Е remain distinct. Non-Russian courses, Latin lookalikes and multi-character/digit choices remain rejected by the existing validator.

Individual matching and letter-sound references are limited to 30 seconds. They must be Ready, independently reviewed, in the lesson language and available to the author under the existing ownership/admin rules. Duplicate media IDs and byte-identical normalized recordings within one matching question are refused. This detects exact duplicates, not semantic homophones; actual linguistic quality still needs human review. Incomplete or malformed source rows cannot be published or started.

## Persistence and concurrency

`ExerciseOption.AudioMediaId` is a nullable restrictive FK, preserving old text options. Revisions copy file references into new option IDs and regenerate private pair keys. Public learning options contain caption/side plus source/license/duration when playable; they contain no match key, file key, hash or saved URL.

`SessionOptionAudio` is a supporting table with `(SessionExerciseId, OptionId)` as its primary key and restrictive FKs to the saved parent and MediaAsset. OptionId refers to the immutable snapshot's option identity, not a mutable source-option FK. This allows a saved session to keep its recording even if a later source lifecycle removes the original option reference. It extends the original 70-model register rather than replacing any original model; see additional-model-register.json.

Draft saving and session creation batch-lock referenced media using one parameterized PostgreSQL `ANY(uuid[])` query, ordered by database UUID. User→course→media ordering is maintained. Validation preloads reviewed metadata in one no-tracking query, avoiding a separate lookup for each audio item and stale tracked metadata. An administrator deletion and a concurrent attachment cannot both succeed.

Deletion/cleanup now protect four reference types: Exercise, ExerciseOption, SessionExercise and SessionOptionAudio. Saved option references are independently protected by PostgreSQL even after source references change. Future vocabulary/dialogue/avatar references still need to join the lifecycle rules when implemented.

## Private playback and assessment

Russian letter listening uses the reviewed recording as the prompt.
The player suppresses the usual vocabulary teaching card and choice
autoplay for this mode, so selecting a letter does not play it before
grading. Visual alphabet questions continue to use regular teaching and
pronunciation. Keep the recorded letter out of the exercise prompt and
the learner-visible audio source and permission fields.

For the Cyrillic basics starter unit, the local staff review bundle
`work/cyrillic-listening-review/lexigo-cyrillic-listening-review.zip`
contains six synthetic candidate recordings, neutral filenames, source
details and a private answer mapping. A Russian-language reviewer must
confirm the letter names, especially Е and Ё, and verify the source before
staff attach approved files to questions through the existing workshop.

The supporting route is:

`GET /api/v1/me/learning-sessions/{id}/exercises/{exerciseId}/options/{optionId}/audio`

It uses the same active owner/current-or-answered rules as whole-exercise playback, and the same explicit AudioPlaybackLink response and five-minute no-store signed link. A media UUID or foreign option/session is not accepted in place of the saved option identity. The link is generated from the saved reference, never an arbitrary supplied storage key.

Matching uses the existing complete-set, one-to-one server assessment and canonical replay rules. Audio does not change the private pairing, heart cost or reward rules. Russian sound recognition uses normal private choice-ID grading. Both are graded; Speaking alone remains ungraded under the separately approved policy. Playback events are not proof of listening and never award XP.

The matching board reuses the explicit native audio control and closes other recordings when a different item is opened, including cancelling late links. Current and completed-question review can play their original recordings. Completed review shows labels/audio without empty connection counts or editable answers; one result board expands at a time. Account/exercise/option change and destruction retain the shared pause/unload/no-stale-response protections. Existing retro colors, typography and characters remain.

## Verification and remaining scope

- Full backend244 passed,0 failed/skipped (`work/test-results/associations-final.trx`). Six new PostgreSQL/Garage cases cover actual matching/letter-sound draft→review→publication, revision references, private option download, complete/reordered matching submission and normal XP, foreign/media-ID denial, source-independent saved FK protection, bad language/owner/review/duplicate clips, administrator-delete races, Ё/Е and case-only/non-Russian sound rejection with visual compatibility.
- Frontend119 tests across31files passed. Tests cover recording row restoration/removal, sound-versus-visual mode, correct encoded route, stale option links, one active player and readonly result review. Existing text matching, speaking and all earlier suites remain intact. Production build, Prettier and C# whitespace checks passed. No dependency added.
- Additive migration20261028131902_AddMatchingAudio adds the nullable option FK and the saved-reference table/indexes. Legacy text data and snapshots remain compatible. Local migration/runtime counts are recorded in PROJECT_MEMORY.md.

All recordings/linguistic review facts in automated tests are synthetic isolated fixtures. No actual Russian/Spanish/etc. curriculum is declared reviewed or published. Real staff onboarding, licensed recordings, all four course curricula and authenticated browser/Safari/device/keyboard checks remain required. This is not automatic pronunciation scoring, semantic audio comparison or an AI service. Existing release/security, Premium, personalization and all other unchecked requirements remain required.

Local checkpoint: AddMatchingAudio applied; schema.sql regenerated; EF reported no pending model changes. API live/ready, website4200 and original phone4201 returned200; the option route and leftAudioMediaId appear in Swagger. Actual database:12migrations,4users,0lessons,0media,0saved-option-audio. No real privilege grant, published fixture, email, purchase or deployment.
