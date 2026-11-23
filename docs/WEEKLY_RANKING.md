# Opt-in weekly course ranking — 2026-11-03

Implements the original leaderboard read using the existing XP ledger and participation preference. No sample competitors, new currency or promotion/demotion league outcomes are created.

## Rules

`GET /api/v1/leaderboards?courseId=&week=&page=` requires authentication and an enrollment in the course. Joined archived courses remain readable. Weeks start Monday at 00:00 UTC and end at next Monday, exclusive; UI labels UTC. Current/past Mondays are accepted; future/non-Monday dates rejected. Daily goals/streaks continue to use the saved personal time zone.

Only currently opted-in, active, confirmed accounts with positive XP in that course/week appear. Scores sum server-created XpTransactions. Equal totals sort by when the total was attained (last positive award), then a stable internal UUID. Twenty-entry pages are bounded; own rank is returned even off-page. Own weekly XP remains private/available while opted out.

Public entries return display name, rank, XP and an own-entry marker. Emails, IDs, award timestamps, answer keys, billing and individual lesson history are absent. Initials are default avatars; uploaded-avatar integration remains pending.

Supporting `PUT /api/v1/me/leaderboard-participation` accepts a required Boolean only, verifies active confirmed account/family under the existing account lock and changes only LeaderboardOptIn. Missing/unknown fields are rejected. Other preferences/accounts are preserved. The UI never joins automatically. Its notice explains all-course/historical scope; withdrawing hides the person from future historical reads without deleting private XP.

Reads use a repeatable-read snapshot, private/no-store responses and existing rate limits. Index-only migration `20261103093949_IndexWeeklyXp` adds `(CourseId, EarnedAt, UserId)`. No new business entity/table or data reset. Original registers remain 70 models/122 operations; supporting operations increase from 44 to 45.

## UI and security review

Protected `/leaderboard` supports course selection, week browsing, explicit join/leave, own score/rank, paging, podium highlighting and honest empty/error states. Course/account changes invalidate late responses. Shrinking pages re-clamp to the last available page. Names use Angular escaping, including markup-like names. Actual browser testing caught a native-select mismatch for a requested course that was not first; explicit option selection and reversed-order regression coverage fix it.

Sensitive-flow review found a shared interceptor edge case: a signed-in request waiting for token acquisition or refresh could be retried under a newly signed-in account. The server still enforced the bearer subject, but could receive the old UI intent under that different subject. Signed-in requests are now bound to their starting account through token acquisition, retry and response delivery. A changed identity produces local409 before dispatch/retry; late private responses are discarded. Same-owner refresh remains valid. Anonymous/public and auth-endpoint flows remain supported. Server authorization is still mandatory; no absolute security or independent release approval is claimed.

## Evidence

- Backend build: zero warnings/errors. `ranking-progress.trx`:20 focused ranking/dashboard/profile cases pass, not the full backend suite. Three ranking cases cover week/course boundaries, attained-time ties, privacy/default/withdrawal, anonymous/invalid/unenrolled denial, overposting, unrelated preferences, archived history and26-participant paging/own rank outside page. Synthetic actors exist only in disposable PostgreSQL tests.
- Frontend: 259 tests/69 files pass, including four ranking cases and four account-binding cases. Production build and formatting pass. Logs: `work/leaderboard-*`.
- Migration applied:25 local migrations, no EF model drift, index exists; before/after learning counts remained at 1 XP transaction, 8 sessions and 27 answers. Both routes appear in actual local OpenAPI; API readiness returned 200. Pinned offline secret scan passed; it is not a comprehensive security audit.
- Live browser verification used the existing signed-in learner. Spanish showed 20 private weekly XP while opted out; the previous week showed 0; Russian showed 0; returning to Spanish restored 20. The agent did not press Join or Leave, submit learner answers or change rewards. Populated ordering was verified in isolated API/UI tests rather than through fake live competitors.
- Desktop/mobile screenshots: `outputs/weekly-ranking-desktop.png` and `outputs/weekly-ranking-mobile.png`.

## Files and remaining scope

Backend: LeaderboardQueries/View/ParticipationService, LeaderboardController, LeaderboardTests, scoped DI, XpTransactionConfiguration and migration/snapshot. Frontend: leaderboard API/page/template/styles/tests, PreferenceStore participation setter, learning navigation, profile help, app route, auth-interceptor and security regression tests. Traceability: REQUIREMENTS, PROJECT_MEMORY, THREAT_MODEL and API/model registers.

Uploaded avatars, actual leagues/promotions/demotions, achievements/challenges, related notifications, friends and all other original/approved scope remain unfinished. The aggregate ranking requirement stays partial where dependent avatar details are unverified. No paid service, real payment, external email, public deployment or fake runtime learner data.

## Signed-in ranking and refresh recovery — 2026-11-17

Desktop and phone-width Edge verified that 20 XP earned in Spanish stays
private by default, appears in the ranking after joining, and stays in the
learner's private total after withdrawal and reload. Previous-week and
Italian rankings showed no Spanish XP for the learner. The browser run
used a disposable PostgreSQL database and local mail inbox.

If a ranking reload fails, the page clears the old results and displays
the refresh action. This prevents sending another participation change
based on an outdated Join or Leave button. The focused Angular suite
passed all five tests, including recovery after a successful save
followed by a failed read. Uploaded avatar integration remains open.

## Private photo on the owner's ranking row — 2026-11-17

A ranked learner can see their existing profile photo on their own row.
The page fetches it through the existing owner-only endpoint and keeps the
ranking response free of photo links. Other participants show as initials.
Initials also cover missing photos, failed requests and broken images. The
ranking notice describes the photo's visibility before joining. Six focused
Angular tests passed, including owner-only rendering and image fallback.
Sharing uploaded photos with other members still requires explicit consent
and a separate access policy.

## Photo consent storage — 2026-11-17

Joining alone leaves the uploaded photo private. The participation write
can store explicit consent for the current photo, and clears it when the
learner leaves. Replacing or removing a photo clears its consent. The
migration introduces a nullable media reference and requires ranking
participation for a stored reference. Seventeen PostgreSQL ranking and
profile tests passed. Shared image delivery and its learner-facing control
are still open.

## Shared profile photos — 2026-11-17

The leaderboard has separate Share profile photo and Stop sharing profile
photo buttons. Sharing applies only to the approved upload. An enrolled
viewer receives a short-lived image link only for an opted-in participant
whose current, ready profile photo still matches their consent. Each page
uses one bounded lookup. The ranking response carries no private account
IDs. Initials cover unshared and unavailable images.

Signed links expire after 30 seconds and use private, no-store responses.
Withdrawal prevents new links. A link already issued may continue working
until it expires, and a viewer can retain image bytes already received.
League promotion and demotion are a separate scope.

Six focused PostgreSQL and private-object-storage tests passed, including
real PNG retrieval, anonymous and nonmember denial, default privacy and
withdrawal. Seven focused Angular tests passed.

### Unavailable ranking photos

If private storage cannot sign a participant photo, the ranking still
returns the score and the UI displays initials. The private-storage
regression verifies a row with an invalid legacy key.
