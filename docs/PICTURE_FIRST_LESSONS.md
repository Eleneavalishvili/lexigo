# Picture-first beginner experience — 2026-11-02

The user requested illustrated recognition, written target words, word-bank translations, persistent feedback and goal/starting-point setup. This slice implements those mechanics in LexiGo's own cream/terracotta style and original artwork. It does not establish full Duolingo curriculum parity.

## Sequence and content

Each course starts with **Your first words**: drinks, people and food, then a checkpoint. Each teaching lesson introduces four words through picture recognition, English word-bank translation and typed target-language recall: 12 questions. Each checkpoint has 12 review questions and requires all three teaching lessons.

Twelve teaching lessons and four checks added. Current source totals: **120 teaching lessons + 28 checks = 148 steps, 1,992 questions, 28 units**. Review is not counted as extra vocabulary breadth. This remains partial A1–C2 coverage. The installer reports 16 added steps and preserves existing lessons/history.

Twelve original SVGs in `frontend/public/lesson-art` show coffee, milk, tea, water, boy, girl, man, woman, bread, apple, sugar and rice. A fixed label whitelist maps assets. Picture cards show target-language labels; selection plays pronunciation. Translation bubbles show the written target expression. Underlined expressions expose public teaching meanings and introductory article hints; placement and unit checks exclude teaching hints.

Word banks can contain unused distractors. Server grading accepts nonempty, known, unique selected IDs and compares their order against private accepted keys. Empty/repeated/foreign selections are rejected; valid wrong selections are graded incorrect. Draft validation, publication and revision copying preserve those rules. No private keys added to public DTOs.

The submitted question remains displayed alongside feedback until Continue. Reload can return the owned last answered public exercise, rather than displaying the next question under the previous correction.

## Starting point

Home language cards open `/learn/start/:courseId`: choose a daily goal, then **Start from scratch** or **Find my level**. Displayed 5/10/15/20-minute estimates map to 20/40/60/80 daily XP across languages. These are estimates, not minute tracking. Save occurs on Begin.

New supporting `PUT /api/v1/me/study-goal` updates only the active owner's DailyGoalXp, validates 1..1000 and preserves other preferences. Scratch enrolls A1 and opens the first accessible lesson; placement uses the existing diagnostic. Existing progress is not reset. Explicit scratch enrollment suppresses older placement recommendations on the next-step card while preserving earlier access grants.

Fresh installs link old greetings after the new teaching sequence. Existing installed greetings retain prior prerequisites to preserve access. Old open sessions keep their immutable question versions. Placement now samples 35 questions for seven units; its existing 12-unit maximum still needs expansion later.

## Evidence and remaining checks

- `picture-first.trx`: 117 focused backend cases pass, zero failed/skipped. Learning suite, profile and selected sentence-publishing regressions; **not the full backend suite**. Includes all current source answers, prerequisites, saved versions, ownership/private keys, distractors/revision copying and goal preservation/validation.
- Frontend: 246 tests / 66 files pass. Picture selection, submitted-question feedback, target hints, wizard and stale-account checks included.
- Production build passes. Final mobile CSS build output: `work/picture-first-dist`. Restricted output writes initially hit Windows EPERM; the authorized build outside that restriction succeeded. Final log has no style-budget warning.
- 2,757 manifest files checked for nonempty RIFF/WAVE structure. This is file validation, not native pronunciation review. Normal playback 1x; Slowly 0.85x.
- Browser walkthrough reached the Spanish goal wizard, starting-point choices and actual **Your first drinks** session with written picture labels. The user continued to a word bank showing café and English distractors while the agent observed. The agent submitted no lesson answers. Opening the preview saved a 40-XP goal and A1 preference, without resetting progress or spending hearts. Narrow-screen inspection exposed excess spacing; mobile spacing and card sizing were tightened and rebuilt. Final mobile geometry still needs another visual check.

Original registers: 70 models/122 operations. Supporting: five models/44 operations (one goal endpoint added). No schema migration, paid service, outgoing mail or public deployment. Full curriculum depth, other documented modules, native review, device checks and security/release gates remain open.

## New character concept

The user rejected the fox/otter appearance and requested an expressive original cartoon cast. A concept only is saved at `outputs/character-concepts/lexigo-human-cast-v1.png`. Three human companions in the retro palette; not installed, rigged or lip-synced. Do not call it finished animation. Confirm/revise the visual direction before producing a large animation set.

Built-in image-generation tool used. Prompt brief: original human cast, midcentury travel-poster/editorial cartoon; cream, ink blue, terracotta, ochre and teal; curly-haired woman with glasses/ochre jacket, wavy-haired man with knit vest/notebook, woman with two hair buns/teal boiler suit; distinct silhouettes, expressive faces, no copied Duolingo costumes/characters, logos or text. Full prompt retained in the conversation tool call.
