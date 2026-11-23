# Registration milestone: security decisions and remaining gates

## Implemented

- ASP.NET Core Identity handles password hashing, email normalization, token generation and validation. Minimum password length 12; maximum request length 128 to bound hashing input. New passwords also require an ASCII uppercase letter and a punctuation/symbol character; whitespace does not count as a symbol. The API enforces this policy for registration, reset and change. Passwords are never emailed.
- Registration DTO accepts only email/name/password. Unknown JSON members are rejected; callers cannot assign themselves roles. Every registered account receives Learner.
- User, preference, role association and confirmation outbox record commit together. Unique normalized email protects races.
- New/duplicate valid registrations return the same generic HTTP 202 body. Duplicate requests do password-hashing work without changing the account. This mitigates enumeration; it is not a promise of indistinguishable timing.
- Confirmation is a POST after an explicit action, so ordinary email-link scanners do not verify accounts by GET. Identity tokens have a 30-minute lifetime. Row locking and confirmed-status checks prevent successful replay/concurrent reuse.
- A still-valid token for an already-confirmed account returns HTTP 400 with code email_already_confirmed, allowing helpful sign-in guidance without repeating confirmation. The server validates that token before revealing the status; invalid/expired tokens receive invalid_confirmation. A missing frontend token gives neutral navigation; network, server and rate-limit failures retain the link for retry.
- Resend returns a generic response for unknown/confirmed/eligible accounts; per-account five-minute cooldown and IP-based entry limits bound abuse.
- Email outbox delivery uses a row lock and retry schedule. SMTP is at-least-once: a process failure after SMTP acceptance can lead to a duplicate email. Tokens remain single-use; exactly-once SMTP is not claimed. Message bodies are cleared after successful delivery.
- Plain SMTP is allowed only for loopback development. Other environments require STARTTLS. Development Mailpit never forwards to outside recipients.
- Tokens and password values are not logged. Frontend referrer policy is no-referrer, and verification removes its token from the displayed URL after use. Production access logs must also redact token query values.
- User Secrets stores the local DB connection; no secrets in tracked settings. Dev data-protection keys persist under ignored .local. Exception output is generic ProblemDetails with trace ID.

## Evidence

PostgreSQL integration tests cover hashed storage, default role, duplicate/concurrent registration, invalid password rejection, malicious role overposting, replay, tampering, wrong-user tokens, concurrent verification, configured token expiration, resend cooldown/requeue, role/course seeds, real schema readiness and entry rate limiting. The browser flow was manually checked from form submission through Mailpit delivery to successful confirmation, using a synthetic example.test account.

## Still required before public release

- Privileged provisioning and authenticator replacement/re-enrollment after losing the device. Authenticator enrollment, login challenges and one-use recovery codes now have implementation and tests; see TWO_FACTOR_SECURITY.md. Email change still remains pending.
- Authenticated authorization policies, class/language scopes and every remaining ownership boundary.
- Trusted proxy configuration for client-IP rate limits, distributed/multi-instance limits as appropriate, trusted HTTPS/CORS/CSRF configuration.
- Secure production SMTP/storage, least-privilege database roles, protected persistent data-protection keys, verified backup/restore and retention/cleanup.
- Production CSP/headers and log redaction, CI security checks and the complete release audit. Local defaults are not production credentials or deployment approval.
- Full contract/a11y/E2E coverage beyond this registration slice. Independent external security review is not claimed.

## References

Microsoft Identity account confirmation/recovery documentation and the official Identity token provider implementation guide the use of framework tokens: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm

HTTP 202 for registration represents accepting an asynchronous confirmation workflow; other resource-creation endpoints retain their documented creation contracts.
