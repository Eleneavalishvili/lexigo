# Curriculum status — updated 2026-11-24

The current completion target is the original Georgian PDF's page 5 topics and language-specific basics in Spanish, Italian, German and Russian. On 2026-11-23 the user prioritized only that PDF scope. Earlier A1–C2/Duolingo-parity ambitions are historical additions, not the basis of the PDF completion count. Topic labels alone do not establish teaching or exercise coverage.

Current verified source inventory (2026-11-25): 173 steps (136 teaching lessons and 37 unit checks), 2,365 questions and 37 units. Spanish, Italian and German each have 42 steps; Russian has 47. The dictionary has 136 entries (34 per course). The release installed 12 new lessons and 48 new words on the public database while preserving existing versions and progress. All four beginner/placement routes passed desktop and phone browser checks. Audio has 4,020 indexed clips; this count does not establish native pronunciation accuracy. See LANGUAGE_FOUNDATIONS.md.

Earlier inventory: 161 steps (129 teaching lessons and 32 unit checks), 2,148 questions and 33 units. The four public language-specific reference guides now supplement the graded courses; see CONTENT_REVIEW.md. Audio integrity review found 3,614 valid reference WAVs and the live manifest matches source exactly. This is not pronunciation-quality approval. Spanish, Italian and German each have 40 steps; Russian has 41 including Cyrillic basics. Earlier checkpoints below record how that inventory developed. Independent language/audio review and remaining baseline coverage stay open.

The 2026-11-23 calendar/work/study slice adds two teaching lessons and one check per language. Nine complete-course/session/installation tests passed, including grading all questions in all four packs; 12 placement regressions passed. Five final installation/English-alternative cases passed after adding natural translations. Eight desktop/phone checks passed in an isolated environment and eight passed against the actual normal website. Source and live catalog counts agree. See BASELINE_TOPICS.md and TEST_REPORT_KA.md. That slice had 3,339 indexed clips; the current 2026-11-24 inventory is 3,614. Availability is not independent pronunciation-quality approval.

On 2026-11-20 a read-only reconciliation compared the running public catalog
and unit endpoints against all four embedded source packs. Spanish, Italian
and German each expose 37 steps/498 questions/seven units; Russian exposes
38 steps/510 questions/eight units. All published step counts match the packs.
Evidence: work/curriculum-inventory-20261120.json. This corrects the stale
60-lesson figure in the checklist; it adds no new lessons or completion marks.

## Installed and verified — initial checkpoint

Each course now has 27 lessons and 376 questions across six units:

- First conversations: seven original introductory lessons, eight questions each.
- Everyday essentials: directions, shopping, home and health; four lessons, 16 questions each.
- Connecting ideas: plans, past events, reasons and polite requests; four lessons, 16 questions each.

- Describing everyday life: colours, time, clothes and hobbies; four lessons, 16 questions each.
- Choices and possibilities: comparisons, obligations, future and invitations; four lessons, 16 questions each.

- Connected stories and opinions: narrating a disrupted trip, explaining a problem, advice and balanced opinions; four lessons, 16 questions each. These add four connected-passage comprehension questions per lesson.

Total: 108 lessons, 1,504 questions, 24 units. A1/A2/B1 tags describe practice topics, not certification or complete coverage. Source packs are private embedded JSON under Infrastructure/Content/Starter. The original source is authored for LexiGo, not copied from another learning product. Automated correctness tests are not independent linguistic review.

New lessons teach four expressions first, then practise recognition, English meaning, finding a sentence in context and production. Language notes cover relevant tense, register, gender and sentence patterns. Male-speaker past forms and corresponding female forms are explicitly explained where needed. Original seven-lesson content and existing progress remain untouched.

The installer uses stable IDs, an advisory lock and a transaction. It skips existing lessons; it must not be used as a silent updater for already-published snapshots. Future corrections require explicit versioning. The immediate previous lesson remains a prerequisite across unit boundaries. Ordinary server grading, ownership, hearts, completion and XP rules apply.

## Evidence

- `work/curriculum-install.log`: 32 new lessons installed, existing progress preserved.
- Read-only local SQL: 60 Published lessons, 736 exercises, 12 units.
- `curriculum.trx`: six passing integration cases, including all 60 lessons/736 answers through authenticated APIs, per-answer reload, completion, XP, prerequisite unlocking and idempotent reinstall. Includes correction privacy and self-reported starting-level checks.
- `work/curriculum-ui-tests.log`: 215 tests in 60 files pass.
- `work/curriculum-ui-build.log`: production build passes.

## Earlier expansion plan (historical, outside the current PDF completion count)

The following paragraph records the earlier ambition, not the current Georgian-baseline acceptance target. Current unfinished baseline work is in REQUIREMENTS.md and CONTENT_REVIEW.md.

Complete A1 and A2 breadth, then substantial B1/B2/C1/C2 material in each language. Advanced topics need original extended reading/dialogue, grammar and productive tasks, not beginner phrases relabelled. Proper placement with a starting-unit recommendation and no skipped XP, unit assessments, cumulative review, curriculum listening/speaking media and independent linguistic validation remain open. The current starting-level selection saves self-report only; it does not assess or unlock advanced units.

Suggested sequence: finish A1 everyday interactions and core grammar; A2 past/future/comparisons/practical tasks; B1 connected narrative/opinions/problem-solving; B2 extended arguments/work/study; C1 register/implicit meaning/complex discourse; C2 nuance/synthesis/style. Each must be populated and verified before it is shown as available.

## Reproduce locally

With the development database running, build the backend and run:

```powershell
dotnet run --project backend/LanguageLearning.Api --no-build --launch-profile http -- --install-starter-lessons
```

This explicitly permitted built-in route runs only in Development. Normal startup does not republish or overwrite content. No new schema, privileged login, real charge or outgoing email is involved.

## Version2 teaching sequence

All60currentlessons now use version2, grouped by expression with writing early. A single expression is taught immediately before its related exercise, rather than a forced all-words carousel. Previously opened sessions retain their saved version and sequence. Current active content remains60lessons/736questions, not120lessons. Revision regression learning-pace.trx7pass; frontend219/buildpass. Installer may now publish an explicitly incremented contentVersion; it never overwrites old versions.

User acceptance clarification: original courses should have comparable breadth and lesson volume to Duolingo, not simply a short starter pack. Exact numeric parity remains unverified and requires a language-specific inventory; the official public B2 coverage statement is not a lesson-count benchmark. Full A1–C2 and all original requirements remain open.

Latest evidence: connected-lessons.trx94learningtests pass, covering all108lessons/1504answers plus grading/ownership/progress regressions.2587referenceWAVs validated; productionbuild and source-secret-scanpass. UI suite remains last219tests because nofrontend source changed thisslice. Fulladvancedcurriculum and placement remain open.

## Unit assessments

24mixedtextunitchecks added: six perlanguage,12–14questions each. Current totals108teachinglessons+24checks,1504teachingquestions+296reviewquestions=1800. Reviewquestions intentionally reuse studied material and are not counted as additional curriculum breadth. Explicit all-unit prerequisites are server enforced; later-unit access is preserved. UNIT_CHECKS.md describes scoring, XP, optional onward progression and remaining assessment scope. unit-checks.trx7pass covers all132steps/1800answers; frontend219/buildpass.

## Placement now available

PLACEMENT.md documents the implemented30question check for each currentcourse, serverfirst-gap recommendation, explicitapply and zero skippedlessonXP. placement-learning.trx98learningcases and224frontendtests/buildpass. Sampling is text-only, not CEFR certification; futurelargerbank/linguisticvalidation and curriculumlistening/speaking remain required. This diagnostic reuses publishedquestions and is not counted as newlessonvolume.

## Picture-first beginner unit — 2026-11-02

Current totals supersede earlier checkpoints: 120 teaching lessons + 28 checks = 148 steps / 1992 questions / 28 units. Added three original 12-question first-word lessons and one check per language. PICTURE_FIRST_LESSONS.md records recognition-to-bank-to-recall sequence, prerequisite compatibility, tests and actual browser inspection. Full A1–C2 and Duolingo breadth parity remain unverified/incomplete. Placement now samples 35 questions for seven units; text-only and 12-unit cap remain.

## Cyrillic basics — 2026-11-16

The Russian course now includes an independent A1 unit with **Meet the
Cyrillic letters**, a 12-question lesson built around М, Т, К, Н, Е and Ё.
It covers letter names, case variants, written sound examples and the
distinction between Е and Ё. Its explicit empty prerequisite list lets
new learners open it without completing another lesson. The starter packs
now contain 149 steps and 2,004 questions across 29 units.

The signed-in lesson uses normal server grading and existing progress
rules. Reviewed recordings used as the prompt for a graded audio-only
question are a separate authoring path. They are still required for this
starter unit.

