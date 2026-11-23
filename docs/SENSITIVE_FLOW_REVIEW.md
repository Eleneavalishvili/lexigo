# Sensitive-flow review — 2026-11-17

Reviewer is Codex acting as the project implementer. This is recorded
as a separate self-review pass. Independent review and production release
approval require their own evidence.

| Area | Controls examined | Findings and disposition |
| --- | --- | --- |
| Authentication and session ownership | Identity sign-in and recovery, antiforgery for cookie-authenticated operations, one-use recovery and factor replacement, current session-family checks, trace-only request diagnostics | A session can be refused after account or family revocation. Request logs omit raw credentials and queries in the diagnostic test. Keep privileged recovery after total factor loss, production proxy trust, and secret rotation open before release. |
| Reviewed audio uploads | Multipart limits, normalized PCM header and frame checks, uploader/language scope, independent review, private playback URLs, and reference-aware deletion | Audio validation is based on content and bounds. A signed URL carries access for its short lifespan. The review process must check rights and pronunciation, and production storage policy and media restore remain separate gates. |
| Simulated checkout and refunds | Live owner lock, quoted price captured server-side, replay keys, atomic test-card balance and entitlement changes, refund delegation and rollback | Replays reuse the original order and payment. Changed details conflict where required by the operation. The completion contract includes only simulated funds. Production billing privileges and the complete operation-level access matrix remain open. |

Regression evidence for the local review is listed in
[the Georgian testing report](TEST_REPORT_KA.md). These findings feed the
[threat model](THREAT_MODEL.md) and the release criteria in
[the engineering standards](ENGINEERING_STANDARDS.md).

The focused PostgreSQL and storage run passed 105 tests with no failures
or skips. The report is `work/test-results-20261117/sensitive-flow-review.trx`.
