# Account administration milestone — 2026-10-28

Source PDF pages 3, 9, 12 and 14. This implements user search, role/status changes, language-scope persistence and administrative history. Content, billing permissions, settings, classes and other role-specific modules remain required and unfinished.

## Access and behavior

- Admin/SuperAdmin can search accounts by name/email, filter role/status and page through results. Responses expose explicit account summaries, not Identity entities, password hashes, security stamps or tokens. Pages contain at most 20 accounts; related role/scope queries are batched.
- Admin may block/restore learner and ordinary staff accounts, but cannot change roles/languages or block Admin/SuperAdmin targets. SuperAdmin may change roles/languages and privileged account status. Other roles and anonymous requests are denied by the API, independently of frontend controls.
- Role sets retain Learner, support all seven documented roles, reject unknown/duplicate values and require active, confirmed, authenticator-enrolled recipients before assigning privileged roles. Language IDs must identify Spanish, Italian, German or Russian. Selecting a language alone grants no content permission; future content endpoints must check role AND scope/ownership.
- Changes require a current password, reason, active server-side MFA session and original two-factor sign-in age at most five minutes. Refresh rotation does not renew this window. Locked-out/blocked actors fail closed. The server reloads actor/target records and checks current roles/session after locking, rather than trusting stale JWT claims alone.
- Role/status writes acquire PostgreSQL advisory transaction lock 730027001, then user rows in deterministic order. Initial bootstrap uses the same guard. The last usable active SuperAdmin cannot be blocked or demoted, including simultaneous self-demotions. Future account deletion and every administrator-role/status writer must preserve this invariant; the full deletion requirement is still pending.
- Changed roles, scopes or status revoke all target sessions and update the security stamp. Audit records include actor, entity, reason, before/after access details, time and the HTTP request trace. No-op saves return success without writing another audit record or revoking sessions.
- Audit reads are paged and optionally account-filtered. SuperAdmin sees the existing audit log; Admin sees account-status history only. Other roles see neither. Audit retention, remaining event writers and broader administrative modules remain pending.

## Data and interface

UserLanguageScope has the original composite key and foreign keys. AddUserLanguageScopes migration was applied locally; the idempotent schema script was regenerated. PostgreSQL rejects duplicate mappings and unknown language references. The application additionally restricts assignments to the four documented target languages.

`/admin/users` uses the approved retro identity, typed reactive forms, account filters, selection, roles/languages, password eye, status controls and paged history. Routing and account links are role-aware; server authorization remains authoritative. Proof/selection/history clear on account changes, and late requests cannot apply across accounts or overwrite newer list/history requests. Failures and last-admin conflicts remain visible instead of claiming success.

The operator has not provisioned the first real SuperAdmin yet. No personal permissions were changed for a browser demo. This milestone's privileged UI behavior was checked with component tests; full visual/mobile/keyboard review with a provisioned test admin remains pending. No screenshot or personal-role grant is claimed.

## Verification and review

AdministrationTests cover every non-admin role denial, authenticated/anonymous access, Admin/SuperAdmin distinctions, role/scope persistence, MFA enrollment requirements, malformed proof/roles/languages/reasons, target session revocation, audit visibility, search/filter/pagination, original sign-in age and concurrent last-admin protection. Database constraint tests exercise actual PostgreSQL uniqueness/foreign keys. Frontend tests cover explicit proof/submission, hidden role controls for Admin, server conflicts and account-switch clearing.

Build/format checks and 44 frontend tests pass. Final backend results are recorded in PROJECT_MEMORY.md. Initial integration execution could not connect to Docker after restart; this was an environment failure, not passing evidence. The stale-socket runtime problem was recovered while preserving Docker data, and tests were rerun against PostgreSQL.

Threats reviewed: privilege escalation/overposting, stale roles/sessions, cross-role roster/audit exposure, MFA bypass, scope tampering, password guessing, last-admin races, partial updates and stale cross-account UI state. Mutations have rate limits and a 16 KiB body cap. This is an internal review, not an independent audit. Production hardening, assisted recovery, account deletion, all remaining role/module policies and the release gates remain required. External SMTP remains paused; no paid service, public deployment or real-account role change was performed.
