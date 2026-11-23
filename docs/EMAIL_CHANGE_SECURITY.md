# Confirmed email changes

PDF pages 4 and 11 require changing email with confirmation. EmailChangeService implements a request and explicit POST confirmation; opening a GET link never changes an account.

## Behavior and threats

- Authenticated PUT /api/v1/me/email requires the current password and, when MFA is enabled, a fresh authenticator or unused recovery code. Wrong proof counts toward Identity lockout. Account-entry rate limiting and a five-minute per-user cooldown bound abuse.
- Requesting leaves the old email, password, confirmation and sessions unchanged. Occupied addresses and cooldowns return the same generic accepted response as eligible requests.
- Identity UserTokens stores PendingEmailChange encrypted through user/name-bound Data Protection, containing target address, random request ID and creation time. No new model table/schema is required.
- Identity issues a target-bound change-email token with the configured 30-minute lifespan. The link includes user ID, request ID and encoded token, without the target email. A new request ID supersedes older links even for the same target. Pending state also expires after 30 minutes.
- Anonymous POST /api/v1/auth/confirm-email-change locks/reloads the user, checks pending ID/deadline, target uniqueness and Identity token, updates email and username, removes pending state, revokes all sessions and queues notices to old/new addresses in one transaction. PostgreSQL uniqueness protects address-claim races. Invalid, expired, used, occupied and wrong-user attempts share a generic failure.
- Password and MFA settings survive changes. Confirmation never signs in the link holder. Existing JWT/session validation rejects revoked sessions. The frontend clears local authentication only for the affected account, preserving an unrelated account viewing the link.
- Routes /account/email and /confirm-email-change clear sensitive inputs after acceptance and URL tokens after final results. Network/5xx/429 failures preserve the link for retry. Missing-token pages provide neutral guidance.

Threats reviewed: stolen sessions, password guessing, MFA bypass/replay, link theft/replay, scanners, enumeration, superseded requests, uniqueness races, partial writes and cross-account browser state. Production HTTPS, token-query log redaction, protected persistent keys and the full audit remain release gates. Encrypted UserTokens depend on retaining the protected key ring.

## Evidence and transport

EmailChangeTests cover request preservation, encrypted pending state, proof/overposting/anonymous denial, unavailable targets/cooldowns, malformed/wrong-user/expired tokens, simultaneous one-use confirmation, superseded links, later address claims, all-session revocation, both notices, new-address sign-in and MFA preservation/recovery-code consumption. Frontend tests cover validation, request errors, explicit confirmation, missing tokens, transient retry and unrelated-account preservation. Final run results and browser-check limits are in PROJECT_MEMORY.md.

External sending remains disabled after the separately approved single Brevo test. Implementation/testing does not authorize more personal emails or changing the user's actual address. This is an internal implementation review, not an independent external audit.
