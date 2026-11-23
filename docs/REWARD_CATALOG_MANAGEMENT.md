# Staff reward catalogs — 2026-11-03

## Paging and creation follow-up — 2026-11-20

The staff screen now searches badges by title/code and challenges by title,
offers 10/20/50/100-row pages and displays the exact filtered record count.
Both list APIs return items, page, pageSize, totalCount and the existing
hasMore field. Requests above 100 rows or 100 search characters are rejected;
stable ordering and empty pages retain the filtered total. Default page size
remains 20. This supersedes the fixed-page description below.

Creating either resource now returns 201 with its readable private Location;
updates retain 200 and the existing revision, audit and protected-history rules.
The 16-case catalog regression passed, and 20 frontend checks passed across
the reward catalog, skills and exercise composer. The final desktop/phone
browser journey passed both projects (20.6 seconds), creating a badge and a
challenge, checking 201, searching and changing page size:
work/learner-browser-76fe9c001fee4bba918aa5779da0c281.

The final targeted validation rerun and local API restart are recorded in
PROJECT_MEMORY.md when finished. This follow-up does not establish complete
pagination or creation-contract coverage for unrelated APIs.

Adds the documented manage/{resource} operations for achievements and challenges: list, single-record read, create, update and DELETE-as-archive. These ten expanded operations are part of the original resource scope; they do not replace the remaining unfinished resource APIs.

## Access and preservation
Global reward definitions are managed only by Admin/SuperAdmin. The controller role check and ContentAccess both apply: active confirmed account, enabled two-factor authentication, current unrevoked MFA-verified session family/security stamp and current database roles. Learner, Teacher, ContentAuthor, Reviewer and Moderator cannot read or write this global catalog. Language-scoped author/reviewer permissions do not grant global reward management.

Writes require a 10–500 character reason and a current revision. The account row and target catalog row are locked, stale edits return409, and successful changes write an AuditLog in the same transaction. Reads/writes are private/no-store, reads are20-record pages, request size8KB and existing content read/write rate limits apply. Unknown fields are rejected; DTOs omit learner identities/award records and secrets. Create operations use server IDs, permanent unique badge codes and unique active challenge rule/window combinations, so uncertain retries cannot silently duplicate the same definition.

Earned badge definitions cannot be edited. Badge codes remain permanent. Archiving retires a badge from new awards while preserving every earned copy and its original title/rule. Archived unused badges disappear from learner eligibility. Award evaluation locks definitions FOR SHARE; staff edits/archiving use FOR UPDATE. Both follow account-before-catalog ordering, so an award/edit race cannot change the definition after it is earned.

Challenges can be created/edited only for a future window starting at least five minutes ahead, maximum366days; started or progressed challenges cannot be edited/cancelled. Future cancellation is archival, retaining audit evidence. Target1..10000/reward1..1000 and supported server rule types are validated; a perfect-lesson badge uses target1. Existing weekly defaults remain unchanged. A deliberately scheduled future challenge with the same rule/exact UTC window takes precedence over the corresponding automatic default; the worker's conflict-safe insertion does not overwrite staff settings.

Migration20261103135602_ManageRewardCatalogs adds IsArchived/Revision metadata to the two original entities and a filtered unique active challenge index. Up is additive, with four existing badge metadata defaults initialized; no learner progress/reward deletion. The scaffold's generic data-loss warning refers to the generated Down dropping these added columns, not a destructive Up. Local migration28 is applied; catalog remains4badges/2challenges and learner history remains1XPtransaction/8sessions/27answers. Role grants, real learner awards and external services were not used for setup.

## UI
/manage/rewards is guarded by administrationGuard and linked from administrator accounts/the lesson workshop. The original logo/fonts/colors remain. Parent catalog handles paging, selected state, loading/errors, mutation feedback and stale-account/generation guards. AchievementEditor/ChallengeEditor provide focused typed reactive forms and share reward-editor.scss. Server CanEdit flags disable protected records; archiving an earned badge remains available with a reason. UTC time inputs serialize explicitly asUTC and preserve precise stored timestamps when those inputs are unchanged. Mutation success is not shown for unknown/failed results; the user is directed to refresh before retrying.

## Evidence and remaining work
First focused20 PostgreSQL cases passed (reward-management and prior achievement/challenge tests). Final full445backendtests passed including the additional concurrent edit-vs-real-collection consistency case (reward-catalog-full.trx). Earlier test failures were a duplicate-role fixture setup and projecting DTOs before filtering/ordering in EF; both were corrected before passing.

Frontend274tests/73files passed, including role/account response guards, explicit mutation/reason/revision behavior, conflict handling, UTC serialization/date order and stored timestamp precision. Final production build/Prettier, scoped .NET formatting, noEFdrift, local readiness/OpenAPI and offline source-secret gate passed. These local checks do not replace independent review or the production release gates. Admin live walkthrough requires an actual authorized staff session. No local account was promoted to fabricate that evidence. Test staff identities/data exist only in isolated PostgreSQL containers.

Source files changed: Core Learning Achievement/Challenge; EF configurations, migration/designer/snapshot; learner AchievementAwards/Queries, ChallengeProgression/Queries, WeeklyChallengeCatalog; Infrastructure Content RewardCatalogAccess/Queries, AchievementManagement/ChallengeManagement and DI; API ManagedAchievementsController/ManagedChallengesController; Content RewardCatalogTests. Frontend reward-catalog-api, reward-catalog page/spec, achievement-editor/challenge-editor, reward-editors.spec, shared reward-editor.scss, routes, workbench/account navigation. Requirement/model/API registers, threat review and project memory carry traceability.

Remaining original/approved scope includes unfinished manage resources, other notification/reminder flows, social profile/avatar/friends/leagues, general achievement/level-up toasts, remaining curriculum and all production/security release gates. Staff catalog delivery is not whole-project completion.

