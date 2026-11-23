# Reviewed audio in lessons

## Current behavior

Listening now supports either a typed transcription or a single selection from 2–8 choices. Zero options means transcription; a nonempty choice list must have 2–8 distinct options with valid private accepted answers. Text answers use the same server-owned Unicode/case/punctuation rules as Translation. Invalid answer shapes do not create attempts or consume hearts; valid wrong answers use the normal lesson heart policy. Playback itself never awards XP or changes an answer.

The author selects an independently reviewed recording for the lesson's target language. The picker shows the author's visible Ready/verified files with pagination and requires explicit selection. Source, license and duration remain available in the staff library. A missing audio reference may remain in an incomplete API draft, but Listening cannot be submitted, published or started without reviewed audio. The editor requires selection before saving.

Core lesson listening is available to Free learners. Dedicated Premium listening/speaking practice and AI conversation calls remain separate required work. Speaking, audio Matching, branching dialogues and complete Russian sound-association behavior are not declared complete by this change.

## Attachment and reference safety

`ExerciseCreateRequest`/`ExerciseInput` accept an optional `audioMediaId`. The server checks current draft ownership, actor/language access and the media row under lock. Authors can attach their own reviewed audio; Admin/SuperAdmin may use other owned uploads in the matching language. Foreign, unreviewed, wrong-language, Pending or Deleting files fail. Text Matching still refuses audio until its dedicated format is implemented.

Transactions acquire user, course and then media locks. The same media row is locked by deletion. An attachment and an administrator's concurrent deletion cannot both succeed. Revisions copy the reviewed reference into the new draft; published data is not overwritten. Publication and start validation recheck the shared ReviewedAudio predicate rather than trusting a browser flag or legacy IsVerified field.

On session creation, referenced media rows are locked in sorted UUID order. `SessionExercise.AudioMediaId` stores the reference separately from the source Exercise. Its restrictive PostgreSQL FK protects audio even if a future source lifecycle removes the original reference. Public JSON snapshots preserve source/license/duration, without storage keys, media hashes, signed URLs or private answer lists. Old snapshots without audio still decode as before.

Media deletion and cleanup now check both source Exercise and SessionExercise references. Future vocabulary/dialogue/avatar references must extend those rules when implemented. Expiring URLs are generated on demand and are never persisted in snapshots.

## Private learner playback

`GET /api/v1/me/learning-sessions/{id}/exercises/{exerciseId}/audio` returns the explicit `AudioPlaybackLink` DTO: `url` and `expiresInSeconds` (300). Swagger documents its response. Requests require the active owner/session family and an existing saved audio reference. No subscription upgrade is required.

A learner can request the current unanswered exercise or an already answered exercise, including completed-result review. Future unanswered exercises, exhausted unfinished current exercises, foreign sessions/exercises and revoked accounts are denied. Playback checks that media remains Ready and independently reviewed in the course language. It follows the saved reference, not a newer source exercise/current lesson version. A completed/started session is not redirected to a replacement audio file.

The response uses `Cache-Control: private, no-store`; S3 links also request no-store caching. Issued URLs are temporary bearer credentials and may remain usable until expiry after revocation; deleting bytes ends access. Do not log/share URLs. The original staff preview remains two minutes; learner links allow five minutes so a recording up to two minutes can be played at a slower pace. Default learning rate limits apply. Disabled storage returns a sanitized 503; no raw SDK error or credential is exposed.

## Frontend and privacy

The shared LessonAudio component appears in the current exercise and completed answer review. It fetches a link only on an explicit click and uses native controls with no autoplay/preload. Playback speed is bounded to 0.75×, 1× and 1.25×. Source/permission text is framework-escaped. Opening links, browser playback failures and expired links have visible retry guidance.

Closing, changing exercise/account, expiry and destruction pause the actual element, remove its source and unload it; late responses cannot restore an old recording. A normal token refresh for the same account keeps playback. The staff preview was also updated to pause/unload its native element explicitly. No microphone recording is uploaded, and no play event is treated as proof of pronunciation or listening accuracy.

The existing cream/ink-blue/gold palette, typography and Pip/Sol reactions remain. This code does not establish actual Safari/iPhone/audio-device compatibility. Full authenticated browser/device checks, licensed recordings and real independent curriculum review remain pending safe staff onboarding. Automated fixtures are not real reviewed Spanish/German/etc. curriculum.

## Verification

- Full backend:230 passed,0 failed/skipped (`work/test-results/listening-final.trx`). Six new real PostgreSQL/Garage cases cover author→reviewer→admin publication, both answer modes, normal scoring/XP, ownership/current-versus-future access, byte-range playback, withdrawal/revocation denial, saved source changes, source-independent FK protection, bad attachment cases, missing audio/invalid choices and attachment-versus-admin-delete races.
- Final focused six cases also cover the real revision endpoint retaining audio and the explicit playback JSON contract (`listening-final-focused.trx`). Synthetic audio/review metadata stays in isolated fixtures; the earlier MediaWorkflowTests cover actual media review APIs independently.
- Frontend:103 tests across27files passed, including picker filters/late responses, explicit playback/speed/cleanup/error/same-account refresh, Listening payloads and author restoration. Production build and Prettier passed. C# whitespace verification passed. No dependency added.
- Additive migration20261028122800_AddSessionAudio adds nullable FK/index only. Existing sessions keep their old JSON and null audio references. Normal local migration/schema verification is recorded in PROJECT_MEMORY.md.

All original requirements remain tracked in REQUIREMENTS.md. Real four-course content, Speaking/self-assessment integration, audio matching/letter association and complete release/security/deployment gates remain required.

## Audio association update — October 28

Audio Matching and Russian sound association now use the reviewed media pipeline. Supporting per-option playback has the same active owner/current-or-answered and expiry protections; no match key enters public snapshots. Speaking is separately ungraded. See AUDIO_ASSOCIATIONS.md and SPEAKING_LESSONS.md for current evidence and remaining real curriculum/device scope.
