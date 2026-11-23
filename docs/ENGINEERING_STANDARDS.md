# LexiGo engineering and release standards

Standing user requirement, saved 2026-10-27. Applies in addition to the complete PDF and acceptance checklist. Best effort is mandatory; perfection and detector outcomes are not promised.

## Full scope and traceability

- Read AGENTS.md, PROJECT_MEMORY.md and the relevant specification before changes.
- Every documented feature, role, course, exercise, model field, API behavior, bonus and deliverable remains required.
- Phasing changes the implementation order, not the final scope.
- Maintain feature/model/API evidence as work progresses. Partial implementation stays unchecked.
- Resolve ambiguities explicitly; record approved amendments. Do not pad scope with unnecessary technologies or remove documented behavior without agreement.

## Handwritten source quality

- Consistent formatting enforced by editor settings, formatters and lint/analyzer rules.
- Readable multiline code; one focused component/service/entity/configuration per file when appropriate.
- No duplicate function declarations, obsolete overwritten implementations, abandoned copies or patch-on-patch workarounds.
- Split files by responsibility. Do not mix routing, data access, business rules, DOM manipulation and templates in a single large script.
- Descriptive names, explicit DTOs and typed contracts. Comments explain non-obvious intent or constraints rather than narrating every line.
- Avoid unnecessary generic repositories, layers, wrappers and speculative abstractions. Follow one consistent architecture appropriate to the app.
- Do not write claims of authorship or create fake commits/evidence. Source quality and user understanding are the objective, not concealment.
- Generated migrations, lockfiles and build artifacts may follow tool conventions; do not confuse bundled/minified assets with handwritten source.

## Angular frontend

- Feature-oriented Angular structure, reusable shared UI, standalone components, typed Reactive Forms and API services.
- Presentational components focus on UI; stores/services coordinate state and requests. Server business decisions are never trusted to frontend flags.
- Signals/RxJS have clear ownership, cleanup and derived state. Avoid leaks, duplicate subscriptions and stale cross-account data.
- Consistent routes, guards, error/loading/empty states, retries and accessible interaction patterns.
- Responsive layouts and keyboard/focus/contrast checks on realistic mobile and desktop sizes.
- Do not bypass framework escaping or inject untrusted HTML. No secrets in browser code. Never expose answer keys in downloaded payloads.
- Microphone permission and unsupported-browser behavior handled; recordings released after use and not uploaded silently.
- Speaking self-assessment is ungraded under the user-approved policy. Preserve `IsGraded` in results/reports and exclude ungraded rows from accuracy, mistakes and mastery; never infer pronunciation quality from a practice-completion Boolean. Speaking-only completion earns no XP. First graded lesson rewards use the durable reward ledger.

## Backend and database

- Consistent thin controllers and application services, DTO validation, domain rules and EF configurations. No accidental bypass via direct ad hoc controller queries.
- Async operations, cancellation where relevant, bounded pagination, projections and deliberate query patterns; avoid N+1 queries and unbounded payloads.
- PostgreSQL constraints enforce uniqueness and relationships; migrations are reviewed. No production database auto-reset, destructive seed or public default admin password.
- Explicit transactions/concurrency protection for XP, publication, invitations, billing and other multi-record business operations.
- Initial administrator provisioning uses PostgreSQL advisory transaction lock 730027001 before user-row locks. Reuse this guard/order in future administrator role/status mutations, including last-Super-Admin protection; do not add a competing lock order.
- Stable error contracts, no internal exception detail to clients, redacted structured logs and auditable privileged actions.

## Security work throughout development

- Maintain a threat model for anonymous, learner, privileged, class-scoped and billing operations.
- Deny by default. Server checks role, language scope, resource ownership, membership and entitlements on every protected operation.
- Identity password hashing; expiring one-use email/reset tokens. Never email a password or change it just because someone requested a reset.
- Short-lived access tokens, rotating hashed refresh tokens, revocation/reuse handling and protected cookies. Do not persist sensitive access tokens casually in localStorage.
- HTTPS, precise CORS, CSRF protection for cookie-authenticated actions, safe redirects and appropriate response headers/CSP.
- Defend against injection, XSS, broken object authorization, overposting, account enumeration, brute force and resource exhaustion.
- Rate-limit authentication, uploads, imports, costly queries, exports and payment operations with sensible bounds.
- Validate uploaded content beyond its filename. Restrict types/size/access; use safe storage keys, private buckets and expiring signed links where required.
- Protect imports/exports against malformed data and spreadsheet formula injection. Restrict external URL fetching to avoid SSRF if introduced.
- Secret scanning and dependency/security advisory review. Pin dependencies and track fixes without claiming scans catch everything.
- Least-privilege service/database credentials, restricted network exposure, patching, log access and secret rotation procedures.
- Document retention/deletion, backup/restore and recovery. Test restoration, not just backup creation.
- Keep a security review record with severity, mitigation, verification and unresolved issues. Never claim secure solely because tests pass.

## Billing-specific safeguards

- Preserve required simulated cards and payments. No real PAN/CVV is accepted or stored by the simulator.
- Server prices, decimal amounts, immutable ledger, scoped idempotency keys, transaction/concurrency tests, no negative balances or duplicate Premium grants.
- Permission checks and audit on refunds; refunds capped at paid amounts. Period boundaries and cancellation tested with controlled time.
- Real provider integration, if approved later, uses hosted/tokenized checkout and authenticated provider confirmation; redirect success alone is not payment evidence.
- Do not activate live charges, buy services or register external accounts without authorization and required credentials.

## Verification and release gates

- Meaningful unit tests for scoring, reviews, prerequisites, XP, streaks, renewals and cancellation.
- PostgreSQL integration tests for constraints, permission boundaries, concurrent requests and rollback.
- Negative tests across every role, language scope, class membership and ownership boundary, including attempts to tamper with IDs, amounts and entitlements.
- Frontend tests plus E2E learning, publishing, classes and billing flows; exercise failure, interruption and resume paths.
- Clean build, format/lint/analyzers, dependency review and appropriate security checks in CI. Do not suppress failures merely to pass.
- Review changes for regression, duplicate/dead code, secret leaks and maintainability before marking a slice complete.
- Verify responsive/a11y behavior, database migration/seed, production configuration, HTTPS, health checks, email/storage, backups and rollback before release.
- Reconcile the final app against every PDF page, model field, expanded endpoint, grading criterion and bonus item. Produce reproducible setup and accurate README/presentation.
- Explain key design decisions and show the user working features in manageable stages. No unsupported claims that the whole project is finished.

## Additional security requirements approved 2026-10-27

- Maintain a feature-by-feature threat review before implementation.
- Verify cross-user and cross-role denial paths for every protected resource.
- Require two-factor authentication for privileged accounts, including secure enrollment, recovery and enforcement before privileged access.
- Automate dependency vulnerability, source-security and secret scanning in CI; review findings and do not claim scans prove safety.
- Perform a separate review pass over authentication, uploads and billing. Do not call self-review an independent external audit; record reviewer and evidence. External review requires arranging an actual reviewer.
- Verify minimal exposed services, least-privilege database credentials and a software update procedure.
- Test backup restoration and document incident response, session revocation and credential rotation.
- Block public release while critical or high-severity findings remain unresolved. Record severity, remediation and retest evidence.
- Continue security maintenance after launch. No promise of immunity to all attacks.
