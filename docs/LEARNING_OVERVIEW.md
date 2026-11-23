# Real learning overview — 2026-11-03

The course page now has a desktop navigation/sidebar and a compact mobile overview, preserving the existing logo, typography and colours. It shows the selected language, current streak, total XP, hearts, daily XP goal and course XP. Country flags are native SVGs because Windows can render flag emoji as letters.

## Source of truth

Original documented `GET /api/v1/me/dashboard?courseId=` reads the authenticated owner only. The response contains no email, private answer keys, other learners' histories, payment details or secrets. The optional course must belong to an enrollment; unknown/unenrolled courses receive the same 404. Invalid IDs receive 400 and anonymous requests 401. Supplying another user ID does not change the authenticated subject.

Total/course XP are sums of existing XpTransactions, not cached browser rewards. Completed lessons come from persisted LessonProgress. Today's XP and goal-reached evidence come from DailyActivity for the server-calculated local date using the saved time zone. Hearts use the existing recovery and subscription entitlement service. No currency/gems or achievements are fabricated.

The read uses one PostgreSQL repeatable-read snapshot for coherent aggregates. Streak is calculated in SQL without downloading the complete activity history. Consecutive historical `GoalReached` dates count; the streak can end today or yesterday, giving the learner the rest of today to continue it. A missed calendar day breaks continuity. Future dates are excluded. Changing today's goal does not retroactively erase a historically earned goal flag; the UI says **a daily goal reached today**, while the progress meter reflects the current goal. Historical date labels remain saved when a time-zone preference changes.

This is a read-only endpoint: it does not finish lessons, create XP, alter the heart wallet, enroll learners or change preferences. The response is private/no-store and uses the existing learning rate limit. No new entity, migration or supporting endpoint was added. Original counts remain 70 models/122 APIs plus five supporting models/44 APIs.

## Frontend lifecycle

LearningOverview clears data on owner/course change and rejects stale responses. Request generations and DestroyRef cleanup prevent late updates. It refreshes every minute while visible and asks the server again when an existing refill countdown expires. The countdown uses the response's server time plus elapsed client time, so a client clock offset does not change the refill entitlement. Failed refreshes keep the original timestamp rather than extending the countdown. Background/destroyed views do not keep polling the API.

LearningShell provides a sticky desktop sidebar and a stacked mobile layout. Navigation targets existing Learn, Vocabulary, Profile settings and Pricing pages. The Profile link does not claim that social profiles/friends are finished. Practice/placement tools and extra saved sessions are now accessible disclosures so they do not crowd out the next lesson and course path.

## Evidence

- Backend build: zero warnings/errors; repository formatter completed.
- `dashboard.trx`: five isolated PostgreSQL cases pass. Covers fresh zero state, ownership/privacy/access, per-course/global ledgers, historical goals, streak gaps/future dates, saved-timezone rollover and unchanged historical streak under a raised goal.
- Frontend: 251 tests/67 files pass, including new real-value rendering, server-offset countdown, stale-owner and stale-course response tests. Final build/format logs are `work/dashboard-ui-build.log` and `work/dashboard-format-check.log`.
- Existing pinned offline Gitleaks source scan passed (`work/dashboard-secret-scan.log`); this is not a comprehensive security audit.
- Actual authenticated browser read showed 20 total XP, 1-day streak, 5/5 hearts, 0/40 today and 20 Spanish XP, matching the stored history. No account/session was fabricated for the walkthrough and no lesson answer was submitted.
- Desktop 1280px and mobile 390px were checked for overflow. Captures are `outputs/learning-overview-desktop.png` and `outputs/learning-overview-mobile.png`. The user's active profile page was left untouched while checks ran in a background tab.

## Files

- Backend: `DashboardQueries.cs`, `DashboardView.cs`, `DashboardController.cs`, `DashboardTests.cs` and scoped DI registration.
- Frontend: `learning-overview.{ts,html,scss,spec.ts}`, `learning-shell.ts`, `course-flag.ts`, `learning-api.ts`, `journey.{ts,html,scss,spec.ts}` and shared disclosure styles.
- Traceability: PROJECT_MEMORY, REQUIREMENTS and original API/model registers.

Full social profile/friends, achievements/challenges, weekly leagues, achievement/level-up toasts, optional sound effects and wider analytics/activity endpoints remain required. Dedicated Premium AI calls, full curriculum scope and all outstanding security/release gates remain open. This dashboard does not establish project completion or complete security.

## Daily activity API — 2026-11-12

`GET /api/v1/me/activity?from=2026-11-01&to=2026-11-30` reads the signed-in
learner's saved daily activity. Dates are inclusive and represent the local
study date recorded by the server. Each row contains earned XP, completed
lessons and whether the daily goal was reached. The API returns only recorded
days, ordered by date. It accepts ranges of up to 366 days and supports `page`
and `pageSize` with a maximum of 100 rows per page.

The signed-in learning menu links to **Learning activity** at `/activity`.
The screen opens on the last 30 days and supports date filters and pagination.
The desktop and phone-width registration tests verify that a finished lesson
appears with 20 XP and that a date range without records shows an empty state.
