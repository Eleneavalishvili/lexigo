# Teacher profiles and private study

## Cancellation and fresh requests — November 25

Cancelled or declined requests no longer disable the teacher card. A fresh
request has a new ID; the previous request remains in history and its private
space and messages remain closed. Concurrent clicks return the same currently
Pending/Accepted request rather than notifying the teacher twice. PostgreSQL's
partial unique index enforces one active request per learner and offer.

The new migration preserves all rows. Its downgrade refuses to collapse
multiple historical requests into the old all-status unique index. Retain a
compatible application when rolling back after new request history exists.

Verification:24 class/request/schedule/message PostgreSQL cases passed;
the teacher-card component case and desktop/phone cancellation/reload/re-request
journeys passed. Published with an encrypted pre-update SQL backup, original
asset retention, index-hash verification and the existing server expiry timer.

Approved additions, 2026-11-25. These extend the original Georgian specification; they do not replace its classroom, assignment, identity or privacy requirements.

The presentation directory contains eight fictional tutor profiles: Ava and Leo (Spanish), Sofia and Luca (Italian), Nina and Noah (German), Maya and Alex (Russian). Portraits are original SVG illustrations. Cards show the language, teaching levels, biography and GEL rate per lesson. A study request does not itself deduct that displayed rate; the existing subscription and wallet remain the only implemented payment checkout.

An active Premium learner can browse the directory, filter by language and request a teacher. The teacher receives an in-app notification and can accept or decline. An accepted request opens a private space where the teacher shares published course lessons. Both participants see their shared appointment times in their browser's time zone. The teacher can schedule, change or cancel an appointment. The server rejects overlapping teacher or learner appointments and accepts durations of 15–120 minutes, five minutes to 180 days ahead. These are appointment records, not video meetings or external calendar bookings.

The shared lesson list shows the learner's status, best score and completion timestamp for those shared lessons only. Class assignments and class-wide reporting remain under Classes. A teacher generates an invitation code from an owned classroom; a learner pastes it under Classes → Join class. Codes are not emailed automatically.

Private messages are available to the two participants of an accepted study request while the learner has Premium. The server validates ownership and active access for every read and send. Message bodies are plain text, limited to 2,000 characters, displayed with Angular escaping. Messages use stable request IDs for safe retries, bounded pagination and a conversation quota. Sending produces a recipient notification without copying private message text into that notification. Refresh messages reads new replies; this implementation is not a WebSocket chat.

Teacher sign-in at `/teacher-login` uses a private six-digit access code plus a current authenticator code. The server stores a keyed HMAC lookup rather than the raw access code. The lookup key belongs in private server configuration, never frontend code or GitHub. Identity lockout, sign-in rate limiting, MFA replay protection, session rotation and teacher-role checks still apply. Learner and administrator sign-in remain unchanged. Teachers are provisioned by the operator; public registration never grants a teacher role.

Presentation access is stored encrypted for the operator's Windows account. `scripts/Show-PresentationTeacherAccess.ps1` displays the eight access codes and current authenticator codes locally. It contains no embedded credentials. Actual teachers should manage their own authenticators; the local helper is for the approved fictional presentation accounts.

Implementation map: `ExtraStudyDirectory`, `ExtraStudyRequests`, `ExtraStudySpace`, `ExtraStudyScheduling`, `ExtraStudyMessages`, `TeacherCodeLogin` and `TeacherAccessHasher` in Infrastructure; dedicated API controllers; Angular `extra-study`, `extra-study-space`, `study-messages` and `teacher-login` components. Message export and account-deletion erasure use the existing account services.

Verification evidence is tracked in REQUIREMENTS.md and the additional API/model registers. Physical microphone pronunciation accuracy and AI conversation calls are separate open work, not established by messaging or appointment tests.
