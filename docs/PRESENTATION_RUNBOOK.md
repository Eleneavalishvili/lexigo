# LexiGo presentation walkthrough

The public presentation website is <https://206.189.25.5/>. Use your existing
confirmed account and rehearse the exact course before presenting. The live
server does not depend on Docker Desktop or this laptop staying open. Its
scheduled deletion is December 4, 2026 at 00:15:26 Tbilisi time; verify actual
deletion in DigitalOcean. This guide does not certify full-scope completion.

The startup and local-inbox instructions below apply only to the local copy.
Live account email uses the separately configured Brevo connection; local
Mailpit messages do not arrive in Gmail. Staff MFA must be prepared separately.

## Start the application

1. Open Docker Desktop and wait for its engine to start.
2. Open the repository root in VS Code. Press **Ctrl+Shift+B**, then choose
   **Start LexiGo** if prompted. Alternatively, run
   `./scripts/Start-Demo.ps1 -OpenBrowser` in PowerShell 7 at the repository root.
3. Wait for **LexiGo is ready**. The browser opens at
   <http://localhost:4200/>. Docker Desktop shows database, mail and storage;
   the frontend and backend run separately on the computer.
4. Keep the computer awake and these services running throughout the demo.

## Learner demonstration

1. Open the home page and select Spanish, Italian, German or Russian. Open a
   course; use the guest trial if showing the experience before registration.
2. Register an account you control. For this local setup, open
   <http://localhost:8025/> and find the message addressed to that account.
   Open the confirmation link and press **Yes, confirm my email**.
   These messages stay in the local inbox; they do not arrive in Gmail.
3. Sign in. Return home: the hero should offer **Continue learning** and
   **My account**, with no **I already have an account** action.
4. Choose a course, daily goal and starting point. Choose **Start from scratch**
   to demonstrate beginner teaching rather than a placement assessment.
5. Complete the first lesson. Show picture choices, word tiles, typed answers,
   pronunciation controls, immediate feedback and a deliberate incorrect answer.
   The Spanish drinks lesson introduces café, leche, té and agua.
6. Finish the lesson and show XP, accuracy and the answer explanations. Reload
   the results and learning path. Sign out and back in to demonstrate that
   completed progress belongs to the account and is stored on the server.
7. Open **My account → Profile, settings & privacy**. Show saved profile
   settings and privacy controls. Do not delete the demonstration account.
8. Show the word library, progress analytics, achievements and weekly ranking.
   Ranking participation is optional; do not imply private profiles are public.

## Premium demonstration

Open **Free & Premium** and the test wallet. Use only a simulated card and
simulated balance to buy Premium, show the subscription and receipt, and open
the available Premium practice. Payments are simulated; no real bank card is
needed. AI-character conversation calls remain pending and must not be
presented as implemented.

## Staff demonstration

Staff pages require an authorized account with the appropriate role and MFA.
A learner account cannot grant itself staff access. Follow
[the privileged-account setup](PRIVILEGED_SETUP.md) privately before presenting;
never put passwords, authenticator keys or recovery codes into slides.

With prepared accounts, show teacher invitations and assignments, then the
author → independent reviewer → administrator publication workflow. The
workshop also links to private media, CSV imports, course skills and rewards.
Do not modify published teaching content merely to demonstrate a control.

## Before presenting

- Rehearse the exact learner account and language you will use.
- Test the audio on the presentation computer; browser voices and microphone
  permission differ between devices. Do not promise perfect pronunciation.
- Prepare staff access in advance or explicitly leave that demonstration out
  of the planned route; its underlying requirements remain mandatory.
- Keep the local inbox private: it contains confirmation and recovery links.
- Public access needs a separate deployment. Never expose PostgreSQL, the
  local email inbox, storage administration or development diagnostics.
- Review [the full checklist](REQUIREMENTS.md) and distinguish verified
  functionality from remaining scope when answering the lecturer.

The formal Georgian grading walkthrough is [GRADING_AUDIT_KA.md](GRADING_AUDIT_KA.md).

## Changes in the 2026-11-22 presentation pass

Verification: 379 frontend tests passed; 46 broad browser cases passed and
the four corrected account cases passed in a dedicated final run. Four guest
and private-history cases also passed against the normal site on port 4200.
The production frontend/API image passed the local HTTPS rehearsal, including
real authentication and persisted lesson progress. Lint, formatting, the
source-secret scan and current NuGet/npm dependency audits passed. These are
separate runs, not one uninterrupted green full-suite invocation. See
[the Georgian test report](TEST_REPORT_KA.md) for scope and evidence.

| Files | Change |
| --- | --- |
| `.vscode/tasks.json`, `scripts/Start-Demo.ps1`, `README.md` | Start the local services and open the website from VS Code; document the correct startup and local email inbox. |
| `frontend/src/app/features/home/home-hero.ts`, `home-hero.html`, `home-hero.spec.ts` | Replace guest actions with learning/account links when signed in; verify live session changes. Existing branding is preserved. |
| `frontend/e2e/registration.spec.ts` | Verify home actions and persisted lesson completion, XP and results after sign-out/sign-in. |
| `frontend/e2e/account-recovery.spec.ts` | Verify the real reset-email journey, old-password behavior, successful reset and single-use link enforcement. |
| `frontend/e2e/import-restoration.spec.ts` | Use accessible dropdown roles in saved-import browser checks. |
| `scripts/Verify-LearnerBrowser.ps1` | Increase only the isolated test environment's shared account-request budget and preserve browser output in its run directory. |
| `.github/workflows/lexigo-ci.yml` | Allow 60 minutes for the existing full build/database/browser verification sequence. Hosted execution remains unverified. |
| `frontend/src/app/features/content/lesson-preview.spec.ts`, `frontend/src/app/features/learning/spoken-check.spec.ts`, `frontend/e2e/extra-study.spec.ts`, `frontend/e2e/plan-administration.spec.ts` | Formatting corrections only. |
| `docs/openapi.json`, `docs/additional-api-register.json` | Refresh the API snapshot and record verified private resource detail operations from the existing implementation. |
| `docs/PROJECT_MEMORY.md`, `docs/TEST_REPORT_KA.md`, `docs/REQUIREMENTS.md`, this runbook | Record actual presentation checks and outstanding setup without inflating the checklist. |
