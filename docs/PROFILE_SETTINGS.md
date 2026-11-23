# Profile and study settings

2026-11-16: The current profile supports private avatar upload and removal.
Desktop and phone-width Edge verified real storage playback and removal
after reload. The browser test also verified saved name, course, time zone,
daily goal and study days. The earlier rollout notes below record the
initial profile implementation.

Implemented 2026-10-27 against PDF pages 4, 11 and 14. This is a partial profile milestone; the remaining profile, media, notifications and learning requirements are still required.

## Current behavior

- `/account/profile` edits a display name and saved study preferences. The account page links to it. The original cream, blue and red design remains the guest palette; dark mode is an optional account setting. After the dark-mode test, the user asked about the colors and the preview was restored to Daylight paper.
- `PATCH /api/v1/me` currently edits only DisplayName, trimming whitespace and rejecting control characters or names outside 2–80 characters. Unknown members are rejected globally. Avatar changes await private media/upload implementation; the entire profile PATCH scope is not complete.
- `GET/PUT /api/v1/me/preferences` reads/replaces only the authenticated account's preferences. No caller-controlled user ID. Stored fields include every baseline UserPreference field plus ActiveCourseId, StudyDays, ReminderTime and EmailRemindersEnabled, required by page 4's prose.
- Daily XP accepts 1–1,000; study days use a seven-bit mask, Monday bit 0 through Sunday bit 6, with at least one day. Reminder time is local wall time with minute precision; the server validates the time zone. The future scheduler must handle daylight-saving gaps/overlaps and deduplicate notifications. Saving does not send email.
- Only published courses can be selected. The four seeded courses are currently unpublished; the UI shows Choose later until content is published. Additional endpoint `GET /api/v1/me/preferences/courses` supplies authenticated choices and does not replace the required public catalog.
- Light/dark/system appearance and reduced motion apply through a root preference store. Settings load after authentication and reset on logout. A generation counter prevents old account responses from applying after account changes or newer saves. Failed saves do not show success.
- Sound, leaderboard and email reminder choices persist; sound effects, leaderboard and reminder scheduler remain pending. Avatar upload, confirmed email change, export/deletion and privileged recovery also remain pending.

## Data and security

`AddStudyPreferences` adds a nullable course foreign key with SET NULL deletion, study-day/theme check constraints and safe weekday/18:00 defaults for existing rows. The idempotent schema script was regenerated. Preference writes use a transaction and the same user-first row-lock order as session/security mutations; parallel updates remain whole. Name writes are scoped to the active authenticated user. Framework template escaping remains in place.

Threats reviewed: cross-account access, role/email/ID overposting, hidden-course selection, invalid time zones/masks, partial concurrent updates, stale account responses and false saved-state messages. Negative/concurrency cases are in ProfileTests and frontend preference/profile tests. Real email sending remains disabled after the single approved Brevo test. No external message, paid service or personal password change was introduced.

## Verification

- 73 backend tests passed against isolated PostgreSQL, including 11 new profile cases; 29 frontend tests passed, including 7 new settings cases. Production Angular build and formatting passed.
- Initial frontend sandbox execution failed before tests because its temporary files could not be renamed. The elevated run then caught a real missing reactive-form directive on the name form; fixed it and reran successfully.
- Applied migration locally with existing accounts preserved. Browser checked a synthetic learner's sign-in, name save, goal/time-zone save, optional dark mode, restoration of light palette and values after a fresh page load. Its test-only queued confirmation was removed after local verification without external sending. The personal account/password were not changed.
- CSS includes a single-column layout below 650px. Full mobile-device, keyboard and contrast audits remain release gates; a desktop screenshot does not establish those checks.

This is an internal implementation review, not an independent external audit. Preference persistence does not establish scheduled delivery or downstream learning behavior.

## Profile API update — 2026-11-12

The signed-in account summary and display-name update now declare their typed
responses in Swagger. A successful display-name change returns HTTP 200 with
the saved display name. The avatar and preference editors use their dedicated
endpoints.
