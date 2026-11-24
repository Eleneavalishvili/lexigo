# Accessibility and responsive layout checks

The original LexiGo logo, palette and typefaces remain in use. Accessibility
fixes should make the existing interface easier to use without changing its
identity.

## Repeatable local check

Run from the project root with Docker Desktop available:

```powershell
./scripts/Verify-LearnerBrowser.ps1 -Configuration Debug -BrowserChannel msedge -Spec accessibility.spec.ts -Workers 1
```

The runner creates disposable PostgreSQL and email services and uses synthetic
accounts. Its staff fixture enrolls MFA for those accounts only. It does not
promote a personal account or send an external email. The normal website at
port 4200 and its persistent database are separate from this test preview.

`frontend/e2e/accessibility.spec.ts` uses the pinned, local axe-core Playwright
library. It runs WCAG 2 A/AA and WCAG 2.1 A/AA automated rules without excluding
any rule. JSON findings include rule names, affected selectors and explanations;
the report omits form values and credentials.

## Covered views

| Journey | Screens and interactions |
| --- | --- |
| Public | Home, courses, pricing, contact, sign in, registration and password recovery; keyboard use of the password visibility button |
| Learner | Onboarding into a real Spanish first lesson; account, profile, classes, vocabulary, notifications, test cards, billing history, leaderboard, achievements, challenges and activity |
| Staff | Lesson workshop, CSV import, vocabulary, media, reports, users, analytics, settings and audit; an MFA-authenticated synthetic account has the required roles |

Each journey is inspected at 320×568, 390×844, 768×1024 and 1440×900 across
the phone and desktop projects. A 64-character learner name checks realistic
text wrapping. The checks also detect page-wide horizontal overflow and enable
reduced motion. Existing component and workflow tests separately cover focus
trapping, validation, permissions and account changes.

## Findings and limits

On 2026-11-20 the initial scan found insufficient contrast in the small red
homepage step numbers. They now use 24px text with the same color and display
font. A follow-up scan passed public and learner journeys at all four widths,
but reported faint native dropdown text on the tablet-width CSV import page.
The import selects now explicitly retain the theme's text fill and opacity.
The focused final staff run passed both desktop and phone projects (1.7 minutes),
including the tablet width. Evidence: work/learner-browser-33855f430c0b453282ed7678181d7fb5.
The preceding run passed all public and learner projects; it remains a failed
overall run because of that staff contrast finding. Together these final runs
verify the listed views after the fixes; they are not a single clean full-suite run.

Automated checks cover the listed views and states, not every possible user
journey or all accessibility requirements. Physical phone browsers, screen-reader
operation, device microphones and curriculum-wide pronunciation review remain
separate checks. Passing axe is not a claim of full WCAG certification.

## Release recheck — 2026-11-24

All six public, learner and staff journeys passed again at the four documented widths. Evidence: work/deployment/accessibility-release-20261124.log. Four new language-guide browser checks also passed in isolation and on public HTTPS, including fragment refresh and invalid routes; see CONTENT_REVIEW.md. Physical phones and screen-reader review remain separate.

## WebKit and iPhone-size follow-up

`frontend/playwright.webkit.config.ts` reuses the existing desktop/phone projects
with WebKit, Desktop Safari and iPhone 13 device settings. It does not simulate
an App Store installation or establish physical Safari/device compatibility.
Install the free test engine with `npx playwright install webkit` in `frontend`.
For public-only checks, set `LEXIGO_E2E_URL` to the live URL and run
`npm run test:e2e:webkit -- accessibility.spec.ts --grep "public screens" --workers=1`.
Do not run account-creation journeys against production without their own scope.

All four live guide checks passed under WebKit. The initial broader public run
passed phone but found the rotated Contact postcard extending beyond 768px.
The Contact stacking breakpoint was increased from 760px to 860px, preserving
the visual identity. The local desktop/phone public rerun passed both cases;
the initial failed run is not a pass. Published rerun evidence is recorded in
the release report when complete. Physical audio/microphone and screen-reader
checks remain separate.
