# Weekly learning summary

Learning navigation links to /weekly-summary. A learner can view the current
Monday–Sunday week and twelve previous weeks in the saved profile time zone.
The page shows XP, first lesson completions, different words reviewed, activity
days, graded answer accuracy and a per-course breakdown.

GET /api/v1/me/weekly-summary?weeksAgo=0 takes identity from the validated
session. The response is private/no-store. A repeatable-read database snapshot
keeps counts consistent while learning continues. Reading creates no records
or rewards. No new persistent model or migration was needed.

## Definitions

- XP comes from owned ledger entries earned within the selected local week.
- Completions use first successful LessonProgress.CompletedAt. Repeats do not
  become additional first completions.
- Words are distinct saved vocabulary items actually reviewed in the week.
  The page does not claim newly learned words without an acquisition timestamp.
- Activity days include recorded non-placement answers, word reviews, first
  completions or earned XP. Zero-XP ungraded practice can count as activity.
- Accuracy uses graded lesson/practice answers. Ungraded speaking and placement
  are excluded. No graded answers displays “Not yet measured”.
- Future events are excluded. PostgreSQL converts local week boundaries and
  activity dates using normalized IANA zones, including DST. Calendar-only UI
  dates are formatted without shifting to the browser's time zone.

Course cards show words due now and missed graded answers from the selected
week. Suggestions refer to today: saved-word review, current free mistake
practice or the course path. Historical misses are not presented as still
unresolved; practice can report that no eligible mistakes remain. Archived
courses retain their totals. Original scope has four courses; cards are bounded
at twenty.

## Verification — November 6

Four PostgreSQL tests passed for anonymous/revoked denial, week bounds, no-store,
empty data, account/course isolation, word deduplication, future exclusion,
ungraded accuracy, zero-XP practice and placement exclusion. The Berlin DST test
verified both sides of a 169-hour week. The first SQL version failed grouping
because repeated interpolations became different parameters; grouping by the
selected date alias fixed it before deployment.

Ten summary/navigation frontend cases passed for calendar labels, unmeasured
accuracy, week changes, retry, explicit practice, resolved mistakes and account
changes. A deferred-promise test needed an explicit wait for its rendered loading
state. Frontend/API builds and formatting passed. Signed-in browser, mobile
layout and broader release review remain pending.

The unit completion card, unique code and sharing choice remain separate
unfinished requirements under REQ-064.

## Source changes

Backend: WeeklySummaryView.cs, WeeklySummaryQueries.cs, WeeklyStudyDays.cs,
WeeklySummaryController.cs, DependencyInjection.cs and WeeklySummaryTests.cs.
Frontend: weekly-summary-api.ts; weekly-summary.ts, .html, .scss and .spec.ts;
learning-overview.html and app.routes.ts. Requirement/model/API evidence and
project memory track this slice.
