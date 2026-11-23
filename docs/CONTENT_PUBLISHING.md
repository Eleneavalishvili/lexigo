# Content publishing: first working slice

The original implementation notes below describe the earlier rollout.
Current format coverage includes choice, translation, fill-in-the-blank,
sentence tiles, matching, reading, dialogue choice, alphabet, listening,
speaking self-assessment, and automatic spoken-word checks. The workshop
uses the same submit, independent review, publish, and revision workflow
for these formats. Publishing keeps previously started sessions on their
saved version, including private audio references. The current checklist
records remaining course-creation and physical microphone verification.

## Behavior and access

Creating or duplicating a lesson draft returns `201 Created` with a
`Location` pointing to the workshop detail route. Following it uses
the existing role, language, and draft ownership rules.

Creating a course unit returns `201 Created` with the workshop URL for
that unit. The detail uses the same fields as the unit list. Access
requires a staff session and the course's language scope, or an
administrator role.

The public `/courses` page reads only published course/unit/lesson metadata and versioned English explanations. Anonymous responses never include exercises, accepted answers, matching keys or grading rules. Unpublished and archived lessons return 404. Actual learning sessions, scoring and guest trials remain separate required work.

The `/manage/lessons` workshop supports staff course queues, admin unit creation, owned lesson drafts, metadata and text exercise editing, submission, independent review with comments, admin publication and separate revision creation. Current ready editors and publication validators are MultipleChoice and Translation. The eight other documented types remain mandatory and cannot pass publication until their validators and delivery paths exist. Review notes remain visible when changes are requested. Full history navigation, prerequisite picker and archive controls still need UI work; their current server operations are tested.

ContentAuthor needs the target language scope and owns their drafts. Reviewer needs the language scope and cannot approve a lesson they own or a version they created, even with additional roles. Admin/SuperAdmin may manage drafts and publish an independently approved version; administrator status alone does not grant independent review. All staff need active, confirmed, MFA-enabled accounts and matching active MFA sessions. Account/role changes invalidate access through existing session checks. No personal account was promoted for development.

## Persistence and publication

Migration `20261028062355_AddLessonPublishing` adds Unit, Lesson, LessonVersion, ContentReview, LessonPrerequisite, Exercise, ExerciseOption, AcceptedAnswer and MediaAsset mappings. Lesson.OwnerId, version Title/Objective snapshots and MediaAsset.IsVerified extend the baseline fields. All nine register entries remain partial for their wider dependent behavior.

Mutations lock the acting account before the root course, reload access state, and commit content and audit together. All future course-content writers must preserve this order. Positive unique ordering, one working revision, unique version numbers, same-lesson current-version foreign keys and prerequisite self-edge constraints are enforced in PostgreSQL. Course locking serializes concurrent prerequisite graph changes; graph size is bounded. Cross-course prerequisites/cycles and archiving a prerequisite of a published dependent are rejected.

Submitted/approved/published content cannot be edited as a draft. Changes-requested returns the working version to Draft. Publication requires an independent approved review and revalidates required fields, accepted answers and any referenced audio's verified/source/license metadata. Published content remains available while a separate revision is authored. Publishing replaces the current pointer and archives the previous version without deleting its content. Archive retains historical content and updates course publication visibility. Started learning sessions have not been implemented; snapshot preservation for those sessions is therefore not claimed.

Only bounded typed requests are accepted; unknown fields are rejected. Body limit is 64 KiB. Separate content read/write rate limits avoid sharing the login quota. Owner/account changes clear private frontend state and late responses are discarded. Explanations are rendered as text, without raw HTML. Media upload/verification and S3 delivery are not yet exposed.

## Verification and limits — 2026-10-28

Full backend suite: 113 passed, no failures/skips. After final review-note and archive consistency refinements, all 8 PostgreSQL PublishingTests passed again. Tests cover scope/ownership/self-review denial, state immutability, independent publication, public DTO privacy, retained snapshots, review notes, exercise cleanup, concurrent approval/prerequisite cycles and actual SQL constraints.

Frontend: 50 tests passed across 13 files, no unhandled errors; production build and formatting passed. Original retro catalog layout checked in Opera on desktop and at 390 x 844; mobile scroll width equaled viewport width. Screenshots: outputs/lexigo-course-catalog.png and outputs/lexigo-catalog-mobile.png. Full privileged-workshop browser validation remains pending safe real staff provisioning; component tests do not establish that visual check.

The actual app database has four unpublished prepared courses and zero lessons. No unreviewed sample curriculum or fictional review was published, no external email was sent and no paid service was activated. Empty catalog is intentional until authored material is independently reviewed and published.

Remaining scope includes all other exercise formats, complete curriculum and skills, media pipeline/licensing, full content CRUD/reorder/import/history UI, interactive learning and server scoring, enrollment/progress/unlocking, unit assessments and all other unchecked baseline/additional requirements.

## Learning integration update — October28

MultipleChoice/Translation now have a separate interactive engine, prerequisite map and persisted public/private session snapshots; see LEARNING_ENGINE.md. The earlier statement that no learning sessions exist is superseded for those two formats only. Authors can persist per-exercise case/final-punctuation rules, which publication validates and sessions snapshot. Remaining eight formats and complete content tooling remain mandatory.

Four-format expansion: FillBlank and SentenceBuilder authoring/review/publication/revision support is now verified. Sentence revisions remap private token IDs; blank prompts require one placeholder. See SENTENCE_EXERCISES.md. Six remaining types and other content tools remain mandatory.

## Text matching update — October 28

Text Matching authoring, independent review/publication and new-ID/new-pair-key revision copying are verified. Audio matching remains explicitly publication-blocked pending media delivery. See MATCHING_EXERCISES.md.

## Context and visual-letter update — October 28

AddExerciseContext stores public passages/situations separately from private evaluation. Reading/DialogueChoice require valid context/choice keys; Russian visual Alphabet validates course/letters. ExerciseFormats now keeps publishing/revision/assessment capabilities consistent. See CONTEXT_EXERCISES.md.

## Reviewed lesson audio — October 28

Listening selection/transcription is now supported. The owned draft editor selects reviewed audio; server attachment checks file ownership, matching language and Ready/review evidence under a media lock. Publication/start require valid file evidence; revisions retain the media reference. Session playback follows immutable saved references. Speaking and dedicated audio Matching/Alphabet association remain required. See LISTENING_LESSONS.md; this supersedes earlier unsupported-Listening notes only.

## Guided speaking update — October 28

Speaking now accepts reviewed matching-language reference audio up to30seconds, with no choices or accepted answer keys. Its normal draft/review/publication/revision flow is supported. The editor filters short approved files; publication/start independently enforce the rule. See SPEAKING_LESSONS.md. Dedicated audio Matching/Alphabet association and real curriculum remain required.

## Private draft erasure — November 6

### Shared private lesson handover — November 18

Administrators can use **Hand over this unpublished lesson** in the workbench
to reassign a never-published lesson. The receiving account must be active,
email-confirmed and MFA-enabled, with author access to the course language
or an administrator role. The form requires the receiving email, a reason
and explicit confirmation. Published versions and lessons with learner
history cannot use this operation.

The server checks the expected current owner inside a transaction. A stale
owner conflicts; an identical retry does not create another audit event.
Version authorship and other contributors' drafts remain unchanged. This
lets a departing owner resolve shared private work without erasing somebody
else's contribution. Original authorship remains in the history.

PostgreSQL tests verify role/language denial, stale ownership, published
history rejection, retry safety and subsequent departing-owner deletion.
Desktop and phone browser scenarios verify the actual handover form.

The workbench offers paged version history and confirmed erasure of private
lesson text. Only the version creator with author scope or an admin can erase.
The server refuses published versions and any version referenced by learning
sessions, session exercises or reports. Erasure removes exercises and answer
keys, replaces private review comments and records a timestamp on an archived
version. Version identity and the audit event remain.

After erasing a private revision of a published lesson, the next revision copies
the last published material with the next available version number. A lesson
whose only drafts were erased requires a new lesson. Older history is
viewable, with revision creation available on the latest version.

## Browser walkthrough — November 11

Playwright verified a scoped author creating and submitting a Spanish lesson,
an independent reviewer approving it and an administrator publishing it.
The author also held the Reviewer role and had no approval control for their
own submission. The anonymous lesson endpoint returned 404 before approval
and after approval, then returned the published title after publication.
Each account registered, confirmed by local email, enabled MFA with a
time-based code and signed back in with a recovery code. Only the disposable
database was used to assign fixture roles. All 36 targeted backend publishing
tests passed separately. The disposable API uses a higher per-IP account
request limit so the parallel learner and staff setup can share one loopback
address.
