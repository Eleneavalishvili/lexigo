# Documented screens and presentation entry points

This maps the baseline screen list on PDF page 10 to the implemented Angular
views. It describes navigation, not completion of the advanced curriculum,
AI calls or public deployment. Use http://localhost:4200 for the local demo.

| Documented view | Entry point | Verification |
| --- | --- | --- |
| Home and catalog | `/`, `/courses` | Guest lesson and accessibility browser journeys |
| Registration and sign in | `/register`, `/login` | Registration browser journey; account access checks |
| Confirmation and recovery | Email link opens `/verify-email` or `/reset-password`; `/resend-confirmation`, `/forgot-password` request a link | Registration/Mailpit journey; password recovery and one-use token integration tests |
| Initial language, goal and starting point | Choose a course, then its setup link under `/learn` | Registration, placement and first-lesson accessibility journeys |
| Learning map | `/learn`, then the selected course | Registration, prerequisite, restore and language-switch browser checks |
| Lesson and results | Start an unlocked node; results appear in the saved session view | Real lesson completion in registration, Russian alphabet and classroom journeys |
| Russian alphabet reference | Russian course → Meet all 33 Russian letters; /alphabet/russian | Letter-name/example playback and keyboard navigation; desktop and phone checks passed 2026-11-23. Ungraded reference, no XP or hearts. |
| Mistake and review practice | Practice actions on the course map and lesson results | Learning continuation component tests; private session and practice API tests |
| Vocabulary, collections and review | `/vocabulary` | Vocabulary browser journey and PostgreSQL ownership/scheduling tests |
| Branching dialogue | Open Dialogues from the course map | Four-language conversation browser journeys, including rewards and replays |
| Progress panel | `/activity`, `/weekly-summary` | Dashboard tests and learner accessibility journey |
| Achievements and leaderboard | `/achievements`, `/leaderboard` | Learner accessibility checks; reward and ranking integration tests |
| Classes, assignments and feedback | `/classes`, then a class | Classroom assignment, invitation and handover browser journeys |
| Notifications | `/notifications` | Learner accessibility journey; inbox and reminder tests |
| Profile and study preferences | `/account/profile` | Profile save/reload/export browser journey |
| Security and sessions | `/account/security`, `/account` | Session, password and MFA component/integration tests |
| Free/Premium plans | `/pricing`, `/billing/subscribe`, `/billing/subscription` | Simulated Premium purchase and cancellation browser journeys |
| Test cards, payments and history | `/billing/cards`, `/billing/history`; checkout starts from a plan or heart refill | Billing and heart-refill browser journeys; no real funds |
| Content and review administration | `/manage/lessons`, `/manage/media`, `/manage/vocabulary/revisions`, `/manage/imports`, `/manage/skills`, `/manage/rewards` | Publishing, skill and reward browser journeys and staff accessibility checks |
| User, report and system administration | `/admin/users`, `/manage/reports`, `/admin/analytics`, `/admin/settings`, `/admin/audit` | Staff accessibility journey and server permission/audit tests |

The route definitions are in `frontend/src/app/app.routes.ts`. Result,
practice and feedback views reuse their saved course/session/class context
instead of requiring a separate top-level URL for every screen.

The full desktop/phone browser run recorded on 2026-11-18 passed 36 scenarios.
The 2026-11-20 accessibility runs additionally inspected the listed public,
learner and management views at four viewport widths. Their exact coverage
and remaining manual/device work are recorded in ACCESSIBILITY.md; a screen
being present does not establish every edge case or accessibility behavior.

Teacher and administrator screens require the appropriate role and current
MFA proof. A normal learner account cannot open them. The local demo keeps
email in Mailpit at http://localhost:8025; it does not silently activate an
external mail provider or create privileged personal accounts.
