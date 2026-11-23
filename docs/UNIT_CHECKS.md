# Unit checks

Each of the 24 bundled units now ends with a mixed text assessment: seven introductory lessons produce a 14-question check; each other four-lesson unit produces a 12-question check. There are 108 teaching lessons plus 24 checks, with 1504 teaching questions plus 296 review questions. Review reuses studied material intentionally; it is not additional curriculum breadth.

The unit check is a published lesson using the existing server learning engine. Its explicit prerequisites are every teaching lesson in that unit. Applied placement can satisfy captured earlier-unit prerequisites without marking them completed; see PLACEMENT.md. The API enforces these prerequisites; disabled buttons are not the access boundary. It has no bilingual teaching-card lines, so the player does not reveal vocabulary cards before its questions. Ordinary choices and required reading passages remain visible. Corrections appear after submission through the existing owned-answer flow.

All questions must be answered and at least80% correct to pass. Existing shared hearts, saved answers, immutable session snapshots and retry rules apply. As a graded learning step, the first successful completion uses the normal20XP reward ledger; replays/free practice cannot farm XP. Checks do not award skipped-lesson XP or establish a CEFR certificate.

## Progress compatibility

Checks must follow their own unit lessons. They are optional for access to subsequent units: installing them does not relock lessons learners already had available. Fresh installations use the same ordinary lesson prerequisite chain, excluding check steps; each check independently requires all its unit lessons. The journey offers the next unfinished unlocked step in unit/order sequence. Course totals say learning steps, avoiding the claim that review checks are new lessons.

Published teaching versions, old sessions and existing progress are preserved. The explicit Development installer creates only missing checks. It validates that prerequisites refer to distinct earlier entries in the same course pack, preventing accidental forward references/cycles. Reinstalling is idempotent.

## Verification and limits

`unit-checks.trx`: seven passing integration cases complete all132steps/1800questions, reject early check access, inspect every missing unit prerequisite, verify no teaching hints in check explanations, persist every answer, award first-completion XP, preserve existing version snapshots and verify reinstall. Frontend219tests/61files and productionbuild pass. Read-only actual local SQL confirms132publishedsteps,24checks and1800currentexercises; APIreadiness200.

This is text production/recognition/reading assessment. Listening/speaking unit assessment, broader diagnostic validation, dedicated unit analytics/mastery, broader staff assessment authoring and full A1–C2 curriculum remain required. This implementation does not complete the whole assessment requirement or prove independent linguistic quality. No new schema/model or REST operation was needed; normal lesson permissions/ownership and private answer snapshots are reused.
