# Sentence and blank exercises — October 28, 2026

Four formats now support the complete author/review/publish/session/answer path: MultipleChoice, Translation, FillBlank and SentenceBuilder. The six other baseline formats remain mandatory and publication-blocked pending their validators/renderers. This does not complete the curriculum or whole project.

## Authoring and validation

FillBlank prompts contain exactly one `____` placeholder. The learner types only the missing word or phrase. Private alternatives and per-exercise case/final-punctuation rules use the existing text evaluator; no options or token keys are accepted. Text accepted answers must fit the learner's 1000-character input limit. Zero/multiple blanks fail publication.

SentenceBuilder uses 2–20 word/phrase tiles, each at most80characters. The staff editor numbers these from1 and accepts up to20 alternative complete orders, one per line (for example `2 1 3`). Repeated words are separate tile IDs. Every accepted order must use every tile once; missing, duplicate, zero, out-of-range or null entries are rejected. Client-provided plain answers are not used for this format. The server generates AcceptedAnswer.AnswerText and the private OrderedTokenIds JSON from validated numeric positions and newly generated option IDs.

Publication independently validates every private key against that exercise's actual tile IDs. Unsupported forms remain blocked. Draft edits replace options/answers atomically and regenerate keys. Revisions copy all four ready formats and map old tile IDs to newly generated IDs; prior versions and started sessions are unchanged. Invalid old token keys prevent copying rather than creating an invalid new draft.

## Session delivery and assessment

Public session snapshots expose prompt, type and shuffled tile IDs/text only. Accepted token orders stay in the private evaluation snapshot. The order is shuffled once at session creation and persists on refresh. Authors' numbered positions are never returned to learners.

Answers add optional `orderedTokenIds` (max20) to the existing answer DTO. Omitted/null is treated as empty for legacy text/choice clients. Non-sentence formats reject supplied token orders; sentence submissions reject text/choice values and require every displayed tile exactly once. Invalid structural submissions save no attempt and cost no heart. A valid wrong order saves an incorrect attempt and costs one lesson heart; practice remains free.

Assessment uses only snapshotted server token text/keys. Identical printed tiles are interchangeable, so swapping two identical “very” tiles does not create a false error; different word order remains wrong. Private accepted alternatives are supported. The unsorted submitted UUID sequence participates in idempotency comparison, so a changed order cannot reuse an earlier key. For empty token arrays, stored answer payloads retain the previous shape to preserve existing replay compatibility.

The learner word bank supports add/remove, earlier/later controls and clear; it requires no drag gesture. Buttons have text/accessible labels and use standard keyboard interaction. Account/session/exercise changes clear stale selections, and pending retries freeze the payload and UI. Full real-device/keyboard browser learning validation remains a release gate.

## Evidence

Backend full suite141passed,0failed/skipped, PostgreSQL Testcontainers, sentence-final.trx. Content/learning targeted run36passed before final refinements. Tests cover independent publication, invalid/foreign/null token orders, private key exclusion, alternate blank answers, missing placeholder, revision remapping, old-session preservation, concurrent replay, changed-order conflict, valid wrong order heart loss and duplicate-word equivalence. Earlier existing permission/concurrency suites remain green. An initial new test compile missed the RoleNames namespace; fixed before tests ran.

Frontend final count and build are recorded in PROJECT_MEMORY. UI tests cover author numbered-order validation/restoration, blank marker validation, learner answer DTOs, repeated tiles, reorder/remove/clear, disabled state and existing network/account guards. No dependency, SQL entity field or migration changed; EF reports no pending model changes. Actual reviewed curriculum is still empty; test lessons stay isolated.
