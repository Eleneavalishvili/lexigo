# Staff audio workflow

## Current behavior

The staff audio library is `/manage/media`, linked from the lesson workshop. It preserves the existing LexiGo palette and typography. It supports owned/scoped upload, private preview, independent review with comments, pagination and explicit deletion confirmation. Reviewed audio can now be attached to lesson drafts and delivered through owned immutable sessions; see LISTENING_LESSONS.md for the Listening editor, learner playback and remaining scope.

The first inspected upload profile is PCM WAV: one or two channels, 8- or 16-bit integer samples, 8–48 kHz, 0.1–120 seconds and at most 10 MiB. MP3, Ogg, WebM, MP4, float/extensible WAV and other codecs are not accepted by this upload workflow. The underlying S3 adapter's MIME list is not evidence of decoder support. Broader format support remains separate work; do not weaken validation to accept an extension or MIME label.

`WaveAudioInspector` reads actual bytes with a hard limit, including non-seekable streams and partial reads. It checks RIFF/WAVE signatures, exact outer size, bounded chunk count and lengths, padding, one format before one nonempty data chunk, PCM encoding, channel/bit/rate/alignment consistency and complete frames. Duration comes from frames and sample rate. It rebuilds canonical format/data chunks, preserves samples and drops optional metadata. Supplied filenames are not used as paths or storage keys. A normalized SHA-256 and duration are saved server-side. This is a narrow structural parser, not a claim that every browser or decoder is immune to vulnerabilities.

The source and license/permission statement are mandatory, plain text, immutable upload metadata. The software cannot decide whether a permission statement is truthful. The independent reviewer checks pronunciation, quality, language, source and usage permission. Required public attribution and the actual licensed curriculum still need to be addressed when learner playback is implemented. Synthetic test audio is confined to temporary test containers.

## Permissions and API contracts

All routes require an active confirmed account, matching live JWT/session roles and privileged MFA. `ContentAccess` rechecks current account status, factor enrollment, session family and language scopes. The server enforces these rules; frontend visibility is only convenience.

| Operation | Behavior and access |
| --- | --- |
| `POST /api/v1/manage/media` | Multipart `file`, `languageId`, `source`, `license`. Author in assigned language, or Admin/SuperAdmin. A valid WAV reserves a new server-generated media UUID; success returns its ID. Always starts unverified. |
| `GET /api/v1/manage/media?page=1` | 25 rows per page; owner, Admin/SuperAdmin, or Reviewer in the upload's language. DTO excludes storage key, signed URL and private hash. Includes latest review comment. |
| `GET /api/v1/manage/media/{id}/preview` | Same visibility, Ready files only. Explicit request returns a private two-minute GET link and `Cache-Control: private, no-store`. |
| `POST /api/v1/manage/media/{id}/reviews` | `approve`/`reject` and comment. Requires independent Reviewer role and matching language scope, even for an administrator. Uploader cannot approve their own audio, including with combined roles. Only Ready, unverified files with validation evidence can be reviewed. Approval records reviewer/time and audit evidence; rejection leaves the file unverified and shows its comment to the owner. |
| `DELETE /api/v1/manage/media/{id}` | Owner with current author scope or Admin/SuperAdmin. Rejects any exercise/version reference. Success acknowledges a durable deletion request; the worker subsequently removes object bytes and the metadata row. Repeating the request does not duplicate the audit. |

Upload requests are limited to 10 per IP per ten minutes, with no queue, alongside a bounded multipart request and field/header limits. A serialized 200 MiB per-owner quota counts Ready, Pending and Deleting rows until cleanup succeeds. These initial operational limits do not establish production capacity planning. Validation errors return 409, binding errors 400, access failures 401/403 or non-enumerating 404, and unavailable/disabled storage returns a sanitized 503. No raw SDK exception, filename or signed request is sent to the user.

## Storage and SQL consistency

1. Check staff access and source/permission input; inspect and normalize the bounded audio.
2. In a user-locked SQL transaction, recheck access and quota, then commit a Pending MediaAsset with its generated `media/{uuid}` key, hash, duration and metadata **before** PUT.
3. Recheck the user and lock the media row. PUT normalized bytes with a 20-second cancellation deadline. On success set Ready and record the upload audit in SQL. A crash/failure leaves a durable Pending row, even if object bytes reached S3.
4. Cleanup removes Pending rows older than one hour and explicit Deleting rows. It locks one candidate with `FOR UPDATE SKIP LOCKED`, rejects references, performs idempotent object deletion, then removes the SQL row. Failed cleanup rolls back, leaving a retryable row. A restart cannot silently forget a recorded deletion.

Ready objects are never overwritten through the API. A replacement is a new upload and new review. User mutations lock user then media; cleanup only locks media and never takes a user lock. Publication requires Ready state, matching language, independent reviewer/time, validation hash/duration and nonempty source/license. Legacy `IsVerified=true` alone is insufficient. Existing legacy rows are preserved by the additive migration; re-upload is required before new approval when validation evidence is absent.

The cleanup worker runs every 30 seconds when storage is enabled. Test hosts disable it and invoke cleanup explicitly against isolated storage. Ordinary account tests force storage disabled so they cannot use local development credentials or touch the real bucket. A failed worker logs only exception type and retries. Queue age, alerting, failure isolation/capacity, production backups/restore and operational monitoring remain release gates.

Draft attachment writers now lock the media row, require Ready plus reviewed matching-language metadata and respect user/course/media ordering. Both Exercise and SessionExercise have restrictive PostgreSQL FKs and deletion/cleanup checks. Future vocabulary/dialogue/avatar references must join deletion protection before those modules are released. See LISTENING_LESSONS.md for current concurrency and saved-version evidence.

## UI privacy and remaining verification

No recording, upload or playback starts automatically. Upload requires file selection and submission. Microphone self-practice remains separate and local. The library keeps one playback URL in component memory, removes it after expiry/end/error/close/account change/destruction and never persists it. Review notes are entered for one selected recording. Text is framework-escaped; a separate confirmation is required for deletion. Private rows and late responses are cleared/ignored on account change.

Actual privileged onboarding still needs the user's private proof; no real account was promoted for this work. Full authenticated browser/mobile/Safari/audio-device checks and real independent review remain pending. Component/API tests do not establish those results. Core listening stays free under the original scope; this staff library does not implement Premium practice or AI calls.

## Explicit cleanup — November 6

The library now offers Finish deletion for an owned/admin Deleting recording.
POST `/api/v1/manage/media/{id}/finish-deletion` rechecks live MFA, ownership and
language scope, locks the row, rejects learning/vocabulary references and removes
the storage object before the SQL row. The action shares cleanup logic with the
worker. Disabled/unavailable storage returns a sanitized 503 and leaves the
durable request for retry. A repeated request after completion succeeds only for
an administrator or the staff member recorded in the request/completion audit.
Worker completions use a null actor to preserve media-only lock ordering.

Twelve focused PostgreSQL/storage/account-deletion tests passed, including
concurrent completion, foreign denial, missing physical object, storage-disabled
retry and clearing a synthetic author's deletion blocker. Eight audio-library UI
tests and the frontend production build passed. No schema migration was needed.
Authenticated browser/device walkthrough and production storage gates remain open.

## References

RIFF/WAVE container handling follows [Microsoft's RIFF documentation](https://learn.microsoft.com/en-us/windows/win32/xaudio2/resource-interchange-file-format--riff-). See PRIVATE_AUDIO_STORAGE.md for provider setup, credential handling, signed-link limitations and production storage gates.


## Verified checkpoint — 2026-10-28

Full backend224passed,0failed/skipped (`work/test-results/media-final.trx`), including32 WAV cases and8 authenticated media-workflow cases. Final workflow tests also verify reviewer-comment visibility and parallel cleanup. Frontend93tests across25files passed cleanly, including5 library component cases and1 multipart transport case; production build and format check passed. C# whitespace verification passed. No dependency added this phase.

Migration20261028120749_AddMediaLifecycle applied locally; idempotent database/schema.sql regenerated and EF reported no pending model changes. Initial design-time update lacked the migration-specific environment connection and stopped without changes; rerun used existing .env credentials privately through LEXIGO_MIGRATION_CONNECTION. Actual DB9migrations/4users/0lessons/0media. API live/ready, media SPA and original phone preview returned200; all five media operations appear in Development OpenAPI. No real privilege grant, curriculum publication, email delivery, paid service or public deployment.

## Audio association update — October 28

Media lifecycle checks now include ExerciseOption and SessionOptionAudio as well as whole-exercise/session references. Validation preloads media once; draft/session writers batch-lock in PostgreSQL UUID order. Saved item playback survives source-reference removal. See AUDIO_ASSOCIATIONS.md; future vocabulary/dialogue/avatar references still need to join the lifecycle rules.
