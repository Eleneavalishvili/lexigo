# Initial administrator and authenticator recovery

Implemented 2026-10-27 for PDF page 3's protected initial Super Admin and the SEC-003 security amendment. No personal account has been promoted or had its authenticator changed by this work.

## Initial Super Admin

The first administrator is an existing active, email-confirmed account with an enrolled authenticator. There is no default password, public bootstrap endpoint or role field in registration.

When the account owner is ready:

1. Register/confirm the account and sign in. Open Account → Manage two-factor authentication.
2. Enter the current password, add the displayed private key in a time-based authenticator app, and verify its code. Save the ten recovery codes before leaving. The account owner performs credential entry/setup privately.
3. Stop the running API. From the repository root in PowerShell 7, build the current backend, then run `./scripts/Initialize-SuperAdmin.ps1`.
4. Enter the confirmed email, current password and a fresh authenticator code in the local prompts. Password/code prompts are hidden. A code just used for enrollment/sign-in cannot be replayed; wait for a new code if necessary. Never paste credentials into chat or source.
5. The command refuses unless the account is eligible and no Super Admin assignment already exists, including assignments on inactive accounts. Success grants SuperAdmin, changes the security stamp, revokes sessions and writes an AuditLog/security notice in one transaction.
6. Restart one API copy and sign in using password plus a fresh authenticator or unused recovery code. Admin console/role-management workflows are separate required scope and are not established by initial provisioning alone.

The wrapper supplies JSON through stdin; credentials are not command arguments, settings or files. BSTR buffers are cleared afterward; managed strings still follow normal runtime garbage collection. This is not a production secret vault. The command does not bind an HTTP listener or start the delivery worker. Production keys/configuration and migrations must be prepared separately.

`SuperAdminBootstrapService` acquires PostgreSQL advisory transaction lock **730027001 before user-row locks**. Future role/status administrator mutations must use the same order/guard. Concurrent bootstrap attempts can create only one initial administrator. This is not a maintenance backdoor: it does not replace an existing admin or bypass MFA.

## Replacing a lost or changed authenticator

1. Sign in with password plus a working authenticator code or unused recovery code.
2. Within five minutes of that sign-in, open Account → Manage two-factor authentication → Replace a lost or changed authenticator.
3. Enter the current password and stage the new manual key. A retry from the same session reuses its unexpired pending setup. The old authenticator remains active.
4. Add the key to the new app and verify its six-digit code within ten minutes. The setup belongs only to the authenticated account and sign-in session.
5. On success, save the new recovery codes shown once. The old authenticator key and all previous recovery codes are replaced, MFA stays enabled, and every session is revoked. A security notice and audit entry record the change without keys/codes/passwords.

The recent sign-in already verified both factors, so the last recovery code can be used for login without needing a second old code at replacement. Refresh-token rotation does not renew that five-minute age: the service uses the original family's earliest creation time. Pending state is user/name-bound encrypted Identity token storage. Password/MFA/status/session expiry and pending deadline checks fail closed.

Incorrect new codes restore the old key before committing the failed-attempt counter. Confirmation uses a user-row transaction; concurrent attempts cannot finalize the same setup twice. Losing a password, disabling MFA or revoking the staged session invalidates access to its pending replacement.

## Evidence and remaining gates

- Full backend suite: 94 passing tests, 0 failed/skipped; eight authenticator-replacement cases and five initial-admin cases added. Replacement assertions were refined afterward and all eight cases passed again. Shared TestAuthenticator implements the independent RFC 6238 test client used by both MFA test suites.
- Frontend: 40 passing tests, including setup/verification separation, no premature recovery-code display, wrong-code retry, stale-sign-in rejection and enrollment guidance. Production Angular build/format checks pass. PowerShell script parsed successfully; an empty-proof invocation of the real process command was refused with exit code 1 and no role grant.
- Applied AddAdministrativeAudit locally; SQL has four migrations and zero Super Admin assignments. No successful personal-account CLI provisioning was run. Browser checked the prerequisite/enrollment guidance with a synthetic learner; secret-bearing setup and actual replacement were tested in isolated integration/component tests.
- No new dependencies, paid services or outside emails. SMTP remains disabled following the separate approved Brevo test; pending outbox was empty after these checks.
- If both the authenticator and all recovery codes are lost, these flows do not bypass authentication. Safe administrator-assisted recovery with an approved identity-verification process remains required; SEC-003 therefore stays partial. Actual privileged-account onboarding, production key protection/restore and the full release/security gates also remain pending.

This is an internal implementation review, not an independent security audit or a claim of production readiness.
