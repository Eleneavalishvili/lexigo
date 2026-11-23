# Personal word library

Updated 2026-11-23. Dated evidence below retains historical checkpoints. The Georgian PDF remains the scope authority.

## Lesson-linked expansion — 2026-11-23

The normal local database now contains **88 entries: 22 per language**, up
from 24. Added drinks, people/family, calendar, school/work, travel and routine
words have an English meaning, target-language example, part of speech and
one of the existing six topic labels. This expands the beginner dictionary;
it does not claim to include every expression in the lesson bank.

After the local database is migrated and the Release API built, run from the
repository root:

```powershell
dotnet run --project backend/LanguageLearning.Api --configuration Release --no-build --launch-profile http -- --install-starter-vocabulary
```

This explicit Development-only command adds missing entries. It uses an
installation advisory lock and course locks; existing spellings or stable
built-in IDs are left untouched, including staff-edited text, recording links,
personal saves and due dates. The resource is
`Vocabulary/Starter/words.json`; `tools/curriculum/vocabulary_entries.py`
maintains its readable source data. New insertions have system audit entries;
they are not labeled as independently human-reviewed revisions. The normal
staff author/reviewer/publication workflow is unchanged.

Word cards offer locally generated reference playback only when a matching
clip exists in the same language. They also offer **Hear the example** when
that recording exists. Staff-reviewed word recordings retain their protected
player and are not replaced or falsely marked available by reference clips.
Missing/custom reference text has no unusable playback button. Availability
checks share one cached manifest and do not autoplay or upload learner data.
Playback errors appear beside the affected word/example.

Noun-meaning reviews accept an optional English a/an/the prefix. This does not
change sentence-exercise grammar grading, strip other words or apply to
adjectives/numerals. Review scheduling, replay protection and zero-XP policy
are retained.

Verification: final vocabulary backend run passed 21 tests; the complete
frontend run passed 387 tests. Ten desktop/phone word-library journeys passed
in `work/learner-browser-eadfa271e470458396db6d2fe266f3c1/browser-results.log`,
covering four languages, paging 20+2 entries, word/example playback, saving,
article-tolerant review, persisted due dates and existing collections. The
installer was applied to the normal database and aggregate SQL confirmed 22
entries per course. All 176 word/example references for the 88 entries exist
in the production build. There are 3,532 indexed pronunciation clips, 193
more than the preceding release. Native-speaker quality review remains separate.

## Available behavior

Creating a collection returns 201 and its private API URL. A repeat with
the same name and course returns the existing collection with 200. Only
its owner can open the detail URL.

A newly saved word returns 201 with a private URL for its review
schedule. Repeated saves return 200 and keep the same saved word ID
and schedule.

The authenticated `/vocabulary` screen offers discovery and a personal library. Search matches the target word or English translation, case-insensitively. SQL wildcard characters are literal search text. Course and server-based due/scheduled filters, stable pages of at most 20 words, escaped text, loading/empty/error/retry and save/remove states use the existing retro design. Open it from Your account.

- GET /api/v1/vocabulary lists only published-course words, joining personal save IDs only for the requesting account.
- GET /api/v1/me/saved-words lists owned records, including words saved before a course is withdrawn. Parameters: courseId, search, page; saved list also accepts status=all|due|scheduled.
- POST /api/v1/me/saved-words accepts vocabularyItemId and returns its durable save. New saves require a published course; existing-save retries preserve the original schedule. Initial due time is now, interval/streak zero. No XP or daily activity is awarded.
- DELETE /api/v1/me/saved-words/{id} removes only an owned personal record, preserving the shared item. Missing/foreign IDs return 404.

All operations require an active, confirmed, non-revoked session and use the existing learning rate limit. Mutations lock the account before checking its current session and records. Concurrent saves share one database-enforced unique (UserId,VocabularyItemId) record. The first response reloads PostgreSQL timestamp precision for exact retries. Capacity is 10,000 saved words per account. Lists are private/no-store. Search length/NUL, page, status and unknown JSON fields are validated.

## Persistence and verification

VocabularyItem and SavedWord contain all PDF-listed fields. AddVocabularyLibrary adds tables, restrictive course/user/media FKs, unique saves, schedule index and text/schedule checks. Staff media deletion and background cleanup now check vocabulary references; SQL independently restricts deletion. Nullable audio linkage alone does not complete word audio playback.

Six VocabularyTests cases use isolated PostgreSQL: bilingual/literal search, validation, concurrent duplicates/no XP, account isolation/anonymous denial, withdrawal behavior, bounded paging/due filtering and actual unique/check/FK rejection. Full backend suite: 250 passed, zero failed/skipped (work/test-results/vocabulary-final.trx). Six new UI/store cases cover escaping, save identity, last-item paging, stale searches, account changes and failure/retry; all 125 frontend tests passed. Angular production build and Prettier passed. Migration applied locally; generated schema.sql updated; EF reported no pending model changes.

The initial tests caught query-projection SQL translation and timestamp-precision errors; both were fixed before successful focused/full runs. Formatter checks exposed generated migration encoding and existing whitespace issues; normalized without semantic changes.

## Current vocabulary workflow

My words supports topic filters, private collections and server-graded spaced reviews. Correct answers schedule the next review at 1, 3, 7, 14 or 30 days. A mistake schedules a retry in 10 minutes.

The four courses now have 88 built-in entries with example sentences across six topic families. The earlier migration filled blank examples only on unchanged original starter words without a staff-published revision. The expansion installer never overwrites those records. Word cards show the example and topic label.

In Vocabulary drafts, an author saves and submits a draft, a separate reviewer for that language approves or requests changes, and an admin publishes it. Published revisions preserve existing learner saves. Admins can attach reviewed recordings in Vocabulary recordings. Playback uses short-lived signed links from private storage.

Authors and admins can withdraw an unpublished vocabulary revision after
confirmation. The published word remains usable, a new draft can be created,
and reviewers do not gain access to withdrawn private text. Published revisions
cannot be withdrawn through this action.

Profile & Settings → Privacy downloads a vocabulary-only JSON file with saved words, review history, collections and collection memberships. The server writes the sections sequentially from one PostgreSQL snapshot and scopes all records to the authenticated account.

## Remaining mandatory work

Attach independently reviewed recordings to starter words and expand reviewed curriculum coverage. Verify retention across the vocabulary tables and complete signed-in staff, keyboard and device walkthroughs.
