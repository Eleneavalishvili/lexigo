# Sessions and password recovery — 2026-10-27

This is an internal implementation review of the first-party account flow. It is not an independent security audit, an OAuth/OIDC server implementation or approval for public deployment.

## Decisions and boundaries

Signed-in learning requests have a per-account rate limit. Learners sharing
a school or home network have separate budgets. Unauthenticated learning
requests and account entry remain limited by source IP. Session refresh,
audio uploads and speech checks retain their existing limits.

- Identity verifies passwords and handles password hashing, lockout and email/reset tokens. New passwords require 12–128 characters, uppercase A–Z and a punctuation/symbol; spaces do not count. Five failed attempts lock sign-in for 15 minutes. Account entry endpoints share a 10-request/10-minute per-IP limit; authenticated password changes also use that limit. Proxy trust and distributed rate limiting remain deployment decisions.
- Sign-in requires an active account and confirmed email. Unknown accounts and incorrect passwords receive the same public error; unknown accounts still incur hashing work. This mitigates enumeration without promising identical response timing.
- RS256 access JWTs last five minutes. Validation requires the configured issuer/audience, valid signature, allowed algorithm, expiration and zero clock skew. Every protected request also checks the active device family, account status, security stamp and current roles in SQL. Logout, password changes and blocking therefore invalidate access before JWT expiration.
- Development generates a persistent ignored RSA 3072-bit signing key. Production refuses startup without an explicitly configured key; deployment must protect and rotate it and persist/protect data-protection keys. Multi-key rollover is still a deployment/recovery requirement.
- Refresh secrets contain 48 cryptographically random bytes. SQL stores SHA-256 hashes only. The raw value travels in an HttpOnly, SameSite=Strict cookie. Production uses Secure and a __Host- cookie; loopback Development uses an explicit HTTP exception. The response JSON never contains a refresh token.
- Refresh replaces the token once and keeps an absolute seven-day family expiry. Reusing a previously replaced token revokes its active successor. Concurrent reuse fails closed; a lost successful refresh response may require a fresh sign-in. Ten active device families are allowed per account; new sign-in revokes the oldest excess family.
- Session changes lock the user row before token rows. Reset/change/logout/revocation and refresh use consistent lock ordering, preventing a refresh from resurrecting a concurrently revoked family. Unique token hash and unique live-family indexes reinforce this rule.
- A device ID in the API is the stable family ID, not a rotating token record ID. Session list/details and revocation require ownership. The list is bounded by the sign-in cap and shows a friendly browser/platform label, never a raw token. Browser labels are informational and can be spoofed.
- Login, refresh and cookie-based logout require ASP.NET Core antiforgery validation. An API-only filter calls IAntiforgery rather than depending on MVC view services. The anonymous CSRF setup request and header avoid authenticated/anonymous token binding mismatches. Bearer-only account changes do not authenticate via ambient refresh cookies.
- The frontend keeps access tokens in memory. A refresh request restores the session after reload. Requests share one refresh promise, and Web Locks serialize cookie rotation across compatible browser tabs. BroadcastChannel shares only invalidation events. No access/refresh values are broadcast or saved in localStorage. Unsupported browser coordination can require reauthentication after a refresh race.
- No cross-origin browser API access is enabled. The development Angular proxy and intended production same-origin reverse proxy avoid permissive CORS. A separate-origin deployment needs an explicit trusted-origin review; never add wildcard credentials.

## Recovery

- Forgot-password returns the same generic 202 for known/unknown accounts and does not change the password or sign out devices. A per-user/per-purpose five-minute email cooldown is separate from confirmation resend. Delivery uses the transactional outbox and local Mailpit during development.
- Reset links are Identity data-protection tokens with a 30-minute lifespan. The explicit reset POST validates user, token and new password. A row lock and security-stamp change make successful reset single-use, including simultaneous submissions.
- A reset does not confirm an unverified email. Successful reset/password change revokes every session and queues a password-change notification. Authenticated password change additionally requires the existing password. Neither flow emails a password or signs the browser in automatically.
- Reset pages use no-referrer and remove query tokens after completion. Access logs, reverse-proxy logs and monitoring must redact reset/confirmation query values in production. Mailpit and development outbox access are local developer access, not a public inbox service.

## Privileged accounts

Any role beyond Learner requires two-factor authentication before sign-in. Enabled accounts must supply a valid Identity authenticator code, and privileged JWT/session validation requires MFA evidence. Authenticator enrollment and one-use recovery codes are implemented, with user-bound encryption of authenticator keys/recovery codes/replay state. Authenticator replay is blocked for three minutes. See TWO_FACTOR_SECURITY.md. Operator-controlled initial provisioning and session-bound authenticator replacement are implemented; administrator-assisted recovery after losing every factor/code and actual privileged onboarding remain pending, so SEC-003 stays partial. See PRIVILEGED_SETUP.md.

## Verification

- 51 PostgreSQL-backed account tests cover registration/confirmation, wrong credentials, lockout, JWT validation, cookie/hash handling, rotation/replay/concurrent refresh, logout/all-device revocation, cross-user session denial, account blocking and privileged setup denial.
- Recovery tests cover generic requests, email cooldown, unchanged password/sessions on request, weak/malformed/wrong-user/expired tokens, single-use/concurrent reset, all-device revocation, no confirmation bypass and current-password change.
- Fifteen Angular tests cover account state, password complexity, eye-button accessibility, MFA setup/recovery presentation and recovery-code request handling.
- Browser checks use a synthetic example.test learner: sign-in, reload restoration, session list/removal, logout, private-route denial and recovery request. Password mutation is verified through isolated API tests; no personal credential was changed.
- API migration applied locally; schema.sql exported. Formatter, production frontend build and dependency scans are recorded in PROJECT_MEMORY.md.

## Remaining release gates

Remaining avatar/profile/media and preference consumers; administrator-assisted recovery and operational privileged onboarding; all other role/language/class/entitlement policies; retention and cleanup of expired sessions/outbox; production TLS/proxy/CSP and access-log redaction; protected persistent keys, key rollover and secret scanning; least-privilege database roles; tested backups/incident recovery; CI source/dependency scans; full requirements reconciliation. No public release or external audit is claimed.

Framework references: [ASP.NET Core antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0), [Identity confirmation and recovery](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm), [RFC 9700 refresh token protections](https://www.rfc-editor.org/info/rfc9700/).

Current additions: saved profile/study preferences and confirmed email changes are documented in PROFILE_SETTINGS.md and EMAIL_CHANGE_SECURITY.md. Latest complete suite: 81 backend tests and 35 frontend tests passed (2026-10-27). Email changes preserve MFA, require current-password/MFA proof at request and revoke sessions at confirmation.
