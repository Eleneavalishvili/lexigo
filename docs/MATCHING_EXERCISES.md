# Text matching exercises — October 28, 2026

Matching now joins MultipleChoice, Translation, FillBlank and SentenceBuilder in the author/review/publish/session/answer flow. This slice covers words, translations and phrases as text. Audio matching remains mandatory and explicitly blocked until the media pipeline is implemented; Listening, Reading, Alphabet, Speaking and DialogueChoice also remain required. The full project is not complete.

## Authoring, review and storage

The staff editor accepts 2–10 left/right pairs through two textareas with one item per line and the same number of lines. Each label is nonempty, at most 300 characters and contains no control characters. Each side must have distinct normalized labels (NFC, collapsed whitespace, invariant case comparison); accents are not removed. If meanings could be ambiguous, authors add context such as “you (formal)”. Separate options, accepted-answer text and token orders cannot be mixed into a matching draft.

The server creates two ExerciseOption rows per pair with a random private MatchKey. Positions 1..N describe the left side and N+1..2N the right; these storage positions/keys are staff-only. Matching uses those paired keys rather than redundant AcceptedAnswer rows. Publication revalidates sequential storage order, side sizes, labels and exactly one occurrence of each key on each side. Broken/foreign keys cannot be submitted for review. Existing independent reviewer/scope/owner/MFA rules apply.

Revision copying generates new option IDs and private pair keys while preserving correct relationships. Old versions are retained. Draft edits replace options/keys transactionally. No SQL entity field, dependency or migration was added; existing MatchKey/jsonb snapshots suffice.

## Learner interaction and server assessment

A session snapshots shuffled public option IDs/text/side and a separate private pair mapping. Shuffle uses the platform cryptographic random generator for answer-sensitive option order. Public/learner DTOs contain neither MatchKey nor storage OrderNumber nor private expected pairs. The shuffled order persists on resume.

The learner connects each left item to one right item using labelled native selects and may clear/change connections. A right choice already used elsewhere is disabled. The board shows neutral connection counts; it does not mark pairs correct before server feedback. It requires no drag gesture and stacks rows at small widths. Existing character reactions come from the returned server result.

Answer requests add optional `matchedPairs` arrays of `{ leftOptionId, rightOptionId }`, up to 10. The server rejects null entries, incomplete sets, duplicate left/right use, foreign or reversed-side IDs, and mixed text/token/choice payloads. Invalid structure saves no attempt and costs no heart. A structurally valid wrong complete mapping saves an incorrect answer and costs one lesson heart for the exercise; free practice does not lose hearts. No partial XP or browser-provided score is accepted.

Pairs are canonicalized by left/right IDs before storing the idempotency payload: retries with a different row ordering return the original feedback. A changed mapping cannot reuse that key. Earlier text/token clients keep their old stored payload shapes when matching arrays are omitted/empty. Older snapshots deserialize with absent matching fields. All mutation ownership, active-session checks, locks, answer order and unique completion/reward constraints remain in force.

## Verification and open work

Final full backend suite: 149 passed, 0 failed/skipped (matching-final.trx), including the final cryptographic-shuffle refinement. PostgreSQL integration cases cover independent review/publication, key exclusion, concurrent canonical replay/completion, one reward, malformed/incomplete/duplicate/reversed/foreign/null mappings, no invalid-attempt heart cost, valid wrong mapping cost, changed-key conflicts, private key regeneration and old-session preservation. Additional tests cover Unicode label ambiguity and decoding legacy evaluation snapshots. No known test failure was suppressed.

Frontend: 70 tests across 20 files passed; production build/format check passed. Matching tests cover author count validation and private edit restoration, unique right-side selection/release/clear, disabled pending controls, complete-set submission and absence of client correctness decisions. Existing state isolation and network retry tests remain green.

Full authenticated browser/mobile/keyboard learner verification remains pending reviewed real curriculum; component/CSS review is not real-device evidence. The actual application still has no published lessons, and synthetic content remains inside isolated databases. No test content was silently published, no personal account was promoted, no external email sent and no paid/public service activated. The original live phone preview and retro/character design were left in place.

## Audio association update — October 28

Per-item reviewed audio now extends the text matching flow. Captions stay public; pair keys remain private. Saved SessionOptionAudio references, owner/current-or-answered playback, source-independent deletion protection and readonly result review are verified. See AUDIO_ASSOCIATIONS.md; earlier text-only restrictions are historical checkpoints.
