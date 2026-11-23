# Local speaking self-assessment — October 28, 2026

`/speaking` is a free local recording tool, linked from the homepage footer and account. It contains no unreviewed lesson curriculum or artificial grading: learners bring a phrase from a lesson, record it and listen back. Licensed reference audio and integration with reviewed Speaking sessions remain required. This is not the Premium AI conversation feature.

The microphone is requested only after pressing Record. Only audio is requested, never camera access. Feature detection requires a supported MediaRecorder/getUserMedia browser in a secure context (HTTPS/localhost). MIME selection uses browser support checks for Opus WebM/Ogg or MP4, with a browser-default fallback. Denied/unavailable microphones, recorder/playback failures and unsupported environments show clear errors.

Clips are bounded to30seconds/5MB, stay in memory and use transient object URLs for native audio controls. No HTTP/upload/storage/API request carries recordings. Capture tracks stop on completion/error/cancel. Blob URLs/chunks are discarded after playback, discard/replacement, page hide/exit, component destruction or account change. Late permission grants after cancel/destruction release their streams immediately. Timers/listeners are removed. This is lifecycle cleanup, not a forensic memory-wipe guarantee.

The ignored local phone iframe delegates microphone capability only to localhost:4200; this does not grant actual microphone permission. The user must choose Record and approve their own browser prompt. No agent requested or accepted real device permission. Real hardware/Safari/mobile behavior is not established by mocks.

Automated evidence:87frontend tests across23files passed, including9 recorder lifecycle cases and1 page integration case. Checks cover explicit audio-only request, denied/unsupported state, cancellation/late destruction, duration/size caps, hidden/exit/destroy cleanup, playback deletion and account switching. The production Angular build passed. Backend unchanged; prior164test run remains the last backend evidence. Real microphone capture, reference audio, full Speaking lesson delivery and device accessibility verification remain release work.

## Guided speaking update — October 28

Guided Speaking now reuses this local recorder with reviewed reference audio and explicit ungraded completion. The native element is also explicitly paused/unloaded when the standalone clip is cleared. Current full checks:238backend and111frontend tests, with no actual microphone access by the agent. Device/Safari/browser checks and licensed curriculum remain required. See SPEAKING_LESSONS.md; earlier backend/reference-pending notes are historical checkpoints.
