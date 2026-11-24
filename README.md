# LexiGo

An English-language learning platform for Spanish, Italian, German and Russian.

## Live presentation

Open [LexiGo](https://206.189.25.5/) from any device. Existing accounts and
learning progress were migrated; sign in with the account's existing credentials.
The temporary server is scheduled for deletion on **December 4, 2026 at
00:15 Tbilisi time**. The public link stops working when the server is deleted.
Payments inside LexiGo use simulated cards and balances.

See [deployment verification](docs/DEPLOYMENT.md) for the current release,
private-service checks, certificate renewal and shutdown procedure. Public
hosting does not mean every outstanding requirement is complete.

## Repository structure

| Path | Responsibility |
| --- | --- |
| `backend/LanguageLearning.Api` | HTTP endpoints, authentication, middleware and application startup |
| `backend/LanguageLearning.Core` | Domain entities, contracts and business rules |
| `backend/LanguageLearning.Infrastructure` | EF Core, PostgreSQL migrations, storage, email and service implementations |
| `backend/LanguageLearning.Tests` | Unit tests and PostgreSQL integration tests |
| `frontend` and `frontend/e2e` | Angular application and Playwright scenarios |
| `database` | Idempotent schema and role seed SQL |
| `docs` | Architecture, setup, requirement evidence and technical guides |
| `docker-compose.yml` | Local development services |

See the [database relationship diagrams](docs/DATABASE_RELATIONSHIPS.md) for the learning, classroom, and billing tables.
The [OpenAPI snapshot](docs/openapi.json) describes the local API, and the
[Georgian test report](docs/TEST_REPORT_KA.md) records the verified checks.

## Current milestone

For the local presentation, open PowerShell 7 in this folder and run:

```powershell
./scripts/Start-Demo.ps1 -OpenBrowser
```

Docker Desktop must be running, with the existing local `.env` and frontend
dependencies installed. This command starts or reuses the local services and
opens no paid resources. Visit http://localhost:4200 after it reports ready.
In VS Code, open this repository's root folder and press **Ctrl+Shift+B**
to run **Start LexiGo**. The same command waits for the services and opens
the browser. Opening the editor alone does not start the website.
Confirmation and recovery messages stay in the local inbox at
http://localhost:8025; they are not sent to Gmail. API documentation is at
http://localhost:5187/swagger. See the
[Georgian presentation walkthrough](docs/GRADING_AUDIT_KA.md).
For the step-by-step demo order and setup limits, use the
[presentation runbook](docs/PRESENTATION_RUNBOOK.md).

Learners do not need an authenticator. Staff demonstrations require an
appropriately authorized account with MFA already enrolled. There is no
public default administrator password or automatic role promotion.

The local browser smoke tests cover a guest lesson and private history access
at desktop and phone widths. With the local preview and API running, open a
terminal in `frontend`, run `npx playwright install chromium` once, then
`npm run test:e2e`. On Windows, set `$env:LEXIGO_E2E_BROWSER = 'msedge'`
to use an installed Microsoft Edge browser.

For the full learner and staff publishing walkthroughs, build the API in
Release and run `./scripts/Verify-LearnerBrowser.ps1` with PowerShell 7 from
the repository root with Docker running and frontend dependencies installed.
It starts a disposable database, local inbox and separate preview on ports
4280 and 5280, installs the starter lessons and cleans up after the run.
Use `-BrowserChannel msedge` to run in an installed Microsoft Edge browser.
The regular preview smoke tests skip registration and publishing unless
the isolated services are configured. The isolated walkthrough runs at desktop
and phone widths.

GitHub Actions installs Chromium and runs the same isolated walkthroughs
after backend and frontend checks. Run `npm run lint` from `frontend` to
run the TypeScript lint check locally.

The [deployment rehearsal](docs/DEPLOYMENT.md) runs the built Angular site
behind Nginx over local HTTPS and checks routing and log redaction.

[Saved mistake practice](docs/MISTAKE_PRACTICE.md) now retries unresolved text questions from your own completed learning steps, with original snapshots and no hearts, XP or original-lesson completion. It appears in enrolled course journeys.

[Course placement](docs/PLACEMENT.md) is available from **Find my starting point**. It currently samples 40 questions across eight teaching units per course, grades on the server and applies the recommended starting unit after confirmation. The independent Russian alphabet introduction is excluded from that sample. Skipped lessons earn no XP. Existing progress remains saved.

Four published courses contain **129 teaching lessons and 32 unit checks (2,148 questions including review)**. Each language has 32 teaching lessons and eight checks; Russian also has a 12-question Cyrillic introduction, with teach-first cards, explicit corrections, saved progress and cross-unit prerequisites. The new [calendar/work/study unit](docs/BASELINE_TOPICS.md) adds dates, school vocabulary and grammar explained through examples. [Unit checks](docs/UNIT_CHECKS.md) unlock after their unit lessons and preserve prior access. See [curriculum status](docs/CURRICULUM_STATUS.md) and [local pronunciation](docs/PRONUNCIATION.md). Current content covers part of A1–B1; independent language/audio review remains separate.

[Subscription renewals and inbox](docs/SUBSCRIPTION_RENEWALS.md) now run on the local test simulator after explicit opt-in. Old saved preferences need fresh confirmation; failed/missed renewals stop for manual purchase. [Financial history](docs/FINANCIAL_HISTORY.md) includes declined attempts and owned subscription timelines. [Simulated refunds and billing administration](docs/REFUND_ADMINISTRATION.md) now include explicit delegated permissions, MFA/fresh-password controls and atomic original-card refunds. Dated verification results are recorded in docs/TEST_REPORT_KA.md; the remaining learning and release-security requirements stay tracked separately.

[Free/Premium subscription purchases](docs/SUBSCRIPTION_PURCHASES.md) now have `/pricing`, two-step simulated checkout, owned test receipts and server-enforced UnlimitedHearts. Seed prices match the PDF:9.99GEL/month or79.99GEL/year. Apply AddSubscriptionPurchases. Renewal starts off. Cancellation, durable renewal jobs, original-card refunds and restricted billing administration are implemented and verified locally. Plan catalogue administration and other Premium learning features remain required.

[Simulated heart checkout](docs/HEART_CHECKOUT.md) at `/billing/hearts` restores all five hearts for the user-approved5simulated GEL. It has explicit confirmation, atomic test-card debit/refill, saved declines, safe status lookup/retry and history. Apply AddHeartRefillPurchases and RequireHeartPurchaseFailureReason. Free one-hour recovery remains available; no real money or Premium activation is involved.

The [test-card wallet](docs/TEST_CARD_WALLET.md) at `/billing/cards` now creates/renames/blocks test cards, selects defaults, adds bounded simulated balances and shows owned paged top-up/heart-debit history. Apply AddTestCardWallet. No real money, PAN/CVV or premium/heart grant is involved. Subscription purchases, explicit renewal consent/jobs, original-card refunds and UnlimitedHearts entitlements are now implemented and verified locally; the linked financial documents describe their exact limits. One-time heart checkout is described above.

[Shared hearts and recovery](docs/HEART_RECOVERY.md) now prevent lesson-switch resets and restore all five hearts one hour after exhaustion, with a server-confirmed live countdown. Apply AddSharedHeartRecovery. One-time simulated heart purchases are available; Premium entitlements and device checks remain required. No real charges are enabled.

The [free local speech experiment](docs/LOCAL_SPEECH.md) now passes 12 checks using licensed human recordings: correct words, different words and noise across all four languages. Browser PCM conversion is implemented and tested. [In-lesson spoken word checks](docs/SPOKEN_WORD_CHECKS.md) now add explicit recording submission, server-trusted recognized-word matches, one free miss then heart loss, replay-safe persistence and character activity. Run `./scripts/Initialize-LocalSpeech.ps1 -EnableForDevelopment`, apply AddSpeechCheckAttempts and restart the API. Real microphone/device/curriculum validation and production native isolation remain required; this is not a precise pronunciation-quality score.

The [personal word library](docs/VOCABULARY_LIBRARY.md) at `/vocabulary` contains **88 starter entries, 22 per language**, with examples, topics, word/example reference playback, bilingual search, bounded pages, private collections and server-graded spaced reviews. Staff-reviewed recordings use their separate protected playback. For a fresh local database, run the vocabulary installation command in that guide after migrations; it preserves existing words and saved schedules. Wider curriculum and independent recording review remain separate requirements.

The retro homepage, registration, confirmation email, single-use verification, sign-in/out, rotating sessions, password recovery, authenticator setup, one-use recovery codes and account security screens work locally. Profile/study settings persist in PostgreSQL, with optional dark mode and reduced motion; original retro colors remain the guest palette. Confirmed email changes require password/MFA proof and revoke sessions. Contact Us includes the supplied support email/company and a clearly marked demo address. User-approved original built-in lessons are published without a claimed independent human review; staff-created lessons still require their normal review workflow. Avatar media, scheduled reminders and other unchecked scope remain pending.

Privileged provisioning/authenticator replacement, email/profile editing, lessons, classes, advanced AI and billing remain required. Privileged sign-in requires verified two-factor authentication; initial enrollment currently happens before a privileged role is assigned. This is a development milestone, not a production release.

## Start locally

Keep Docker Desktop running. From the repository root:

```powershell
docker compose up -d database mail
dotnet restore backend/LexiGo.slnx
dotnet tool restore
./scripts/Initialize-Development.ps1 -InstallStarterContent
dotnet run --project backend/LanguageLearning.Api --launch-profile http
```

On a fresh checkout, copy `.env.example` to `.env` and set a random alphanumeric `POSTGRES_PASSWORD` before starting Docker Compose. The initialization script saves the connection in .NET User Secrets, applies migrations, and creates the application roles. The optional flag installs the built-in lessons and branching conversations. Staff accounts are provisioned separately.

In a second terminal:

```powershell
cd frontend
npm ci
npm start
```

- Website: http://localhost:4200
- Registration: http://localhost:4200/register
- Sign in: http://localhost:4200/login
- Account and devices: http://localhost:4200/account
- Authenticator settings: http://localhost:4200/account/security
- Lost/changed authenticator: http://localhost:4200/account/security/replace
- Profile/study settings: http://localhost:4200/account/profile
- Confirmed email change: http://localhost:4200/account/email
- Contact Us: http://localhost:4200/contact
- Local test inbox: http://localhost:8025 (emails stay on this computer)
- API documentation: http://localhost:5187/swagger (Development only)
- Database readiness: http://localhost:5187/health/ready

To debug in Visual Studio, stop any terminal-owned API process first and open `backend/LexiGo.slnx`. Never run two API copies on port 5187.

## Verification

Run `./scripts/Verify-DatabaseRestore.ps1` to rehearse PostgreSQL recovery in
a disposable container. See [database recovery](docs/DATABASE_RECOVERY.md)
for the backup and restore procedure.

```powershell
dotnet build backend/LexiGo.slnx --no-restore
dotnet test backend/LexiGo.slnx --no-restore
npm test --prefix frontend -- --watch=false
npm run build --prefix frontend
npm run format:check --prefix frontend
```

Docker must be running for PostgreSQL integration tests. Testcontainers creates and removes a separate temporary database; tests do not target the app database. C# packages are pinned and locked; `NuGet.Config` keeps the local package cache under ignored `work/`.

See [setup](docs/SETUP.md), [full scope](docs/REQUIREMENTS.md), [project memory](docs/PROJECT_MEMORY.md), [engineering standards](docs/ENGINEERING_STANDARDS.md) and [registration security notes](docs/REGISTRATION_SECURITY.md) and [session/recovery security notes](docs/ACCOUNT_SECURITY.md).

New passwords require 12–128 characters, an uppercase A–Z letter and a punctuation/symbol character. Whitespace does not satisfy the symbol rule. Existing accounts can still sign in; the stronger policy applies when creating or changing a password. See [two-factor setup and boundaries](docs/TWO_FACTOR_SECURITY.md).

Real-inbox email setup: [Brevo setup](docs/BREVO_SETUP.md). The SMTP worker supports STARTTLS, validates enabled transport settings and keeps credentials outside source. The approved Brevo Free test reached the user's inbox, and SQL confirms the account's email is verified. External delivery is disabled again after that single test; production domain authentication remains pending.

Initial administrator setup and authenticator recovery: [operator/security guide](docs/PRIVILEGED_SETUP.md). The first admin requires existing enrollment and private proof; none is created automatically. Administrator-assisted recovery after losing all factors remains pending.

Account administration: /admin/users, available to Admin/SuperAdmin only. See [access rules and evidence](docs/ADMINISTRATION_SECURITY.md). Role/status changes require recent MFA and current-password proof; no personal account was promoted for development. Other role-specific modules remain pending.

Content authoring and catalog: `/courses` lists published content and `/manage/lessons` opens the MFA-protected staff workshop. Authors draft lessons, independent language reviewers approve them, and administrators publish them. Administrators can edit course details and reorder courses, units and lessons. The local browser walkthrough installs 148 built-in starter lessons in its disposable database. See [publishing scope, security and checks](docs/CONTENT_PUBLISHING.md).

Interactive learning: `/learn` holds enrolled language journeys, `/learn/:courseId` shows the prerequisite map, and `/learn/session/:sessionId` resumes saved text exercises. First-pass progress/XP and local-day goals are server-owned; free practice has no XP/unlocks. See [learning rules, security and remaining scope](docs/LEARNING_ENGINE.md). Real lessons require independent review/publication before they appear.

More exercise formats: FillBlank and SentenceBuilder now support authoring through server assessment; see [word-tile/blank rules and verification](docs/SENTENCE_EXERCISES.md). Meet the original animated companions at `/companions`; [character design and accessibility boundaries](docs/CHARACTER_DESIGN.md). The ten documented exercise formats have implementations; reviewed curriculum/device verification and Premium AI conversations remain required.

Text matching is supported from author pair definition through server assessment, with private pair keys and safe retries. Reviewed per-item audio matching is also implemented; real curriculum/device checks remain required. See [matching rules, security and evidence](docs/MATCHING_EXERCISES.md).

Reading passages, single dialogue-response questions and visual Russian-letter recognition now use the reviewed lesson flow. Apply AddExerciseContext for the nullable passage column. See [context, alphabet rules and evidence](docs/CONTEXT_EXERCISES.md). Branching dialogues and all audio/recording variants remain required.

Local speaking practice: `/speaking` records a short clip only after microphone permission, plays it back and releases it after listening or leaving. Clips are never silently uploaded. See [recording limits and privacy](docs/VOICE_RECORDING.md).

Guided [Speaking lessons](docs/SPEAKING_LESSONS.md) now combine reviewed reference audio, local record/compare controls and explicit ungraded completion. Speaking does not affect accuracy or hearts; speaking-only practice earns no XP. Mixed lessons use graded questions for scoring. Apply AddUngradedPractice before running the updated API. Real microphone/device and curriculum checks remain required.

[Audio matching and Russian letter–sound questions](docs/AUDIO_ASSOCIATIONS.md) now use reviewed per-item audio and protected saved references. Apply AddMatchingAudio. Matching review can replay original clips; sound-mode letter choices retain normal server grading. Actual licensed curriculum and browser/device checks remain required.

Private lesson-audio storage: [local Garage setup and security boundaries](docs/PRIVATE_AUDIO_STORAGE.md). Run `./scripts/Initialize-Storage.ps1`, then `docker compose --profile storage up -d --wait storage`. Credentials stay in ignored local configuration and .NET User Secrets. The staff [audio library](docs/MEDIA_WORKFLOW.md) at `/manage/media` validates PCM WAV uploads, saves permission metadata and supports independent review/private preview/durable deletion. Apply AddMediaLifecycle and AddSessionAudio before starting the updated API. [Listening lessons](docs/LISTENING_LESSONS.md) now support reviewed draft attachment, typed/choice assessment and private Free learner playback with saved audio references. Real licensed curriculum, browser/device verification and production infrastructure remain required.

Repeatable free local [security checks](docs/SECURITY_CHECKS.md) cover dependency advisories and a pinned offline source-secret scan. Both pass at this checkpoint; broader source analysis and production release gates remain open.

[Weekly learning summaries](docs/WEEKLY_SUMMARY.md) are at `/weekly-summary`. [Unit completion cards](docs/COMPLETION_CARDS.md) are at `/completion-cards` after applying AddCompletionCards. New unit completions issue private cards automatically; older completed units can be claimed explicitly. Public sharing requires consent and can be revoked. The default setup script applies current migrations; no real account or public sharing setting is seeded.

Beginner setup and illustrated lessons: [flow, original art and verification](docs/PICTURE_FIRST_LESSONS.md). Home language cards open daily-goal/starting-point setup. New first-word units show native labels with pictures, then word-bank/typed recall. Existing sessions retain their saved content; choose the new first unit to see the new flow. Current curriculum remains partial; no claim of full A1–C2 or Duolingo parity. New human character concept is saved for visual review and is not yet animated or installed.


