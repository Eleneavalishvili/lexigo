# Guest trial and help

Visitors can choose a course in /courses and open /try/{courseId} without an
account. The trial uses the earliest published lesson with a written word guide
and multiple-choice exercises. It introduces the written words and their English
meanings before offering up to three questions. Option selection and correction
playback use the existing local pronunciation catalog when a clip is available.
The original LexiGo logo, characters, typography and colors are retained.

GET /api/v1/courses/{courseId}/trial returns the guide, prompts and shuffled
options. It omits grading keys. POST to the corresponding /trial/answers route
requires the returned published version, question and one of that question's
options. It returns correctness, the correction and explanation. Hidden courses,
foreign options and changed versions are rejected. Both routes are rate-limited,
bounded and private/no-store; errors do not disclose unpublished content.

Trial requests use read transactions only. They create no account, enrollment,
learning session, heart state, daily activity or XP. The browser keeps temporary
question progress in memory. Leaving/restarting resets it. This is deliberately
separate from authenticated lessons, which retain their persistent learning
engine and reward rules. A changed publication requires restarting the trial.

The public /help page uses native keyboard-accessible disclosure controls and
links to trial entry, learning, confirmation, password recovery, privacy, plans
and support. Its payment guidance describes the simulated checkout; it does not
advertise real charges or unfinished AI conversation features.

## Verification — November 6

Two PostgreSQL integration cases exercised all four languages, correct and wrong
answers, omitted grading fields, foreign-option/version/hidden-course rejection
and zero account/progress/reward writes. Twenty-eight frontend tests passed
across the trial, introduction, catalog and existing lesson player. New cases
verify teaching before questions, pronunciation on selection, explicit server
feedback, completion, failure retry and discarding a stale language response.
The frontend production build passed. Browser/mobile/audio-device verification
remains a separate open gate. No curriculum breadth or linguistic-review claim
is made by the short trial.

Final checks also passed after excluding audio-only candidate questions and
showing playback error messages. The offline source-secret scan reported no
leaks. The final local API serves all four trials; Development Swagger at
/swagger/v1/swagger.json includes both operations. This is local verification,
not public deployment.

On November 11, all four trials returned a written word guide and three
questions through the local website proxy. Submitting each option for the
first question returned exactly one correct result and a correction for
every submission. This check exercised the running frontend proxy, API and
PostgreSQL. Visual and audio playback checks remain open.

## Changed source files

- Backend: Learning/GuestTrialService.cs, Learning/GuestTrialController.cs,
  DependencyInjection.cs and Learning/GuestTrialTests.cs.
- Frontend: core/learning/guest-trial-api.ts; learning/guest-trial.ts, .html,
  .scss and .spec.ts; learning/choice-answers.ts; help/help.ts, .html and .scss;
  home/home.html; catalog/catalog.html and .scss; app.routes.ts.
- Evidence: PROJECT_MEMORY.md, REQUIREMENTS.md, THREAT_MODEL.md, this file and
  the model/API registers. Full curriculum and browser/device review remain open.
