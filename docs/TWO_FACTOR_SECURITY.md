# Authenticator setup and recovery — 2026-10-27

## Working local flow

1. Register, confirm email and sign in as a Learner.
2. Open Account → Manage two-factor authentication. Enter the current password and choose setup.
3. In a time-based authenticator app, use its manual setup-key option. Enter the displayed key under an account named LexiGo; choose time-based codes. The key is delivered only after password verification and never sent to a third-party QR generator.
4. Submit a valid six-digit app code and the current password. Save the ten recovery codes shown once. All existing devices are signed out.
5. Sign in again with password plus a fresh app code, or choose the recovery-code option. The code used during setup cannot be replayed; wait for the app's next code if necessary.

Replacing recovery codes requires the password and a fresh app code, invalidates previous recovery codes and signs out all devices. A Learner may disable MFA using password plus an app code or an unused recovery code. Disabling rotates the authenticator key, clears recovery codes and signs out all devices. Any privileged role makes MFA mandatory and disables the removal operation on the server.

## Security decisions

- Identity generates authenticator keys, validates time-based codes and creates/redeems recovery codes. No custom production TOTP algorithm is introduced. The test client implements RFC 6238 independently to exercise Identity's verification.
- ProtectedUserStore extends the EF Identity store at its token read/write boundary. Authenticator keys and recovery-code collections are encrypted using ASP.NET Core Data Protection with user-specific and token-specific purposes. The bounded replay history is encrypted too; it contains hashes/timestamps rather than plaintext app codes. SQL-only read access therefore does not reveal these secrets without the protected key ring.
- New setup reuses an existing disabled key and cannot reset an enabled authenticator. Pending setup does not enable MFA or invalidate the session needed to complete setup. Enabling requires password plus a valid app code and then changes the security stamp.
- Login/settings transactions take the user row lock. A recovery code succeeds once, including concurrent redemption. App codes are remembered for three minutes across all successful uses in that window; using a different code does not make an earlier code valid again. Histories are bounded and fail closed if malformed.
- Wrong app codes and wrong settings passwords count toward Identity lockout. Settings require an authenticated bearer session and current password; mutating operations share the account-entry rate limit. Unknown JSON fields cannot override user IDs. The account ID comes from the validated token.
- Enabling, recovery-code replacement and disabling invalidate all sessions, change the security stamp and queue a security-notification email. Codes/keys stay out of logs, URLs, localStorage and cross-tab broadcasts. The frontend keeps the one-time display only in component memory.
- Privileged login and JWT/refresh validation require MFA evidence. A password reset never removes MFA. Recovery login still requires the account password.
- Development uses the existing ignored persistent data-protection key ring. Production must explicitly protect, persist, back up and rotate that key ring. Losing it prevents decryption; access to both the SQL data and key ring compromises authenticator/recovery secrets.

## Tests and review

TwoFactorTests verifies encrypted storage/round-trip, enrollment revocation, login challenge, invalid codes, MFA claims, replay after another code was used, concurrent single-use recovery, privileged sign-in/disable denial, replacement-code revocation, learner disable, missing authorization/current password and role/user-ID overposting rejection. Frontend tests verify setup and activation are separate, recovery codes appear only after server success, privileged removal is hidden and recovery login sends the intended proof.

During the expanded tests, separate hosts using the same development RSA key exposed a shared signature-provider cache holding a disposed RSA object. SigningKeyProvider now disables shared signature-provider caching for its owned key; the account suite verifies the fix. This is an internal implementation review, not an independent audit.

## Remaining required work

- Initial privileged provisioning now requires an existing confirmed, active, authenticator-enrolled account and password/fresh-code proof through an operator-controlled stdin command. It is audited, serialized and refuses when any Super Admin assignment exists. No personal account was promoted; see PRIVILEGED_SETUP.md.
- Dedicated authenticator replacement now supports Learner and privileged accounts after a recent two-factor sign-in, including use of the last recovery code. The old factor stays active until verification; success replaces recovery codes and revokes all sessions. Safe administrator-assisted recovery when all factors/codes are lost remains required, so SEC-003 stays partial. See PRIVILEGED_SETUP.md for timing, evidence and remaining operational gates.
- Manual setup is implemented; a locally generated QR can be added for convenience. Never send a shared key to an external QR service.
- Production key protection/restore, rate-limit/proxy review, incident procedures, independent review when arranged, CI scans and complete authorization remain release gates. Local tests do not establish production readiness.

Framework reference: [Identity UserStoreBase authenticator/recovery methods](https://source.dot.net/Microsoft.Extensions.Identity.Stores/UserStoreBase.cs.html), [Identity UserManager key and recovery operations](https://source.dot.net/Microsoft.Extensions.Identity.Core/UserManager.cs.html).
