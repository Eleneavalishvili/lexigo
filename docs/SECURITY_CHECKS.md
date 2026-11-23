# Reproducible local security checks

Current evidence, 2026-10-30: npm and NuGet report zero known dependency vulnerabilities. Both completed successfully; NuGet returned four audited projects and no incomplete-data problems. These are current known-advisory checks, not proof that dependencies are vulnerability-free.

Run `./scripts/Verify-Dependencies.ps1` with PowerShell7 from the project workspace. It uses locked installed dependencies without restoring/changing packages, saves ignored reports in `work/security`, fails on unavailable/incomplete data and blocks high/critical findings. Low/moderate findings remain reported for review. The script itself was executed successfully with zero findings.

Run `./scripts/Verify-Secrets.ps1` with Docker Desktop running. The official [Gitleaks CLI](https://github.com/gitleaks/gitleaks) is free and needs no external account. Its locally verified8.30.1 image is pinned by digest in `config/security-scanners.json`. It runs without network, with read-only source, no capabilities/new privileges and fully redacted reports. Only the report directory is writable. Pulling the pinned public image is the only network step; source is never uploaded.

The source scan includes untracked deliverable source and `.env.example`. `.gitleaks.toml` excludes ignored local runtime/secrets and generated dependencies; it is not a scan of the whole computer or private .NET User Secrets. Three initial generic-key findings were natural-language documentation about credential hashes and local process/port details. Wording was clarified without suppressing rules or adding finding-specific exclusions. The unchanged rules then reported no leaks. Reports remain ignored under `work/security` and must not be committed.

A separate local source review also found no use of Angular trust-bypass/innerHTML/eval, unsafe interpolated raw EF SQL or certificate-validation bypass patterns in handwritten C#/TypeScript/templates. This targeted review is not a general SAST or independent audit.

Still required: validated GitHub Actions execution, broader automated source-security analysis, independent sensitive-flow review, production least-privilege/network/HTTPS/security configuration, verified backup restoration and incident/session/credential rotation procedures, dependency maintenance and real-device/accessibility checks. No hosted jobs, external account, public deployment, paid service or credential rotation was activated by these local checks. Public release remains blocked until the full applicable gates pass.

## 2026-11-06 — Deletion flow checks

The offline Gitleaks source scan reported no leaks. NuGet reported zero known
dependency findings. The first npm audit reported 16 moderate findings through
the Angular CLI packaging dependency tree. Exact overrides select glob 14.0.2
and minimatch 11.0.1, and the subsequent npm install audit reported zero known
vulnerabilities. Compatibility checks cover the Angular suite, production build
and local package creation with scripts disabled. The upstream fixed versions
are documented in the package advisories. [Glob advisory](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4), [minimatch advisory](https://github.com/advisories/GHSA-g3hv-75wg-m249).

## API request diagnostics

### Local dependency follow-up — 2026-11-20

The npm review reported two moderate development-server dependency advisories.
Compatible patch updates to hono and @hono/node-server removed both findings;
the final audit reports zero known findings at every severity. Evidence:
work/security/npm-audit-20261120.json and
work/security/npm-audit-20261120-patched.json. The lockfile records the selected
versions. This is a dependency snapshot, not proof of general application security.
The new pinned @axe-core/playwright development dependency runs accessibility
checks locally under MPL-2.0 without an external service or account.

Unexpected API failures return a generic HTTP 500 ProblemDetails response
with a trace reference. The exception entry records its type and trace
reference. The request summary records the configured route template, status
code, duration and the same reference. Framework request-start logs that
include the raw URL are filtered at normal verbosity.

The integration test sends synthetic values in a path, query, authorization
header, body and exception message, then checks both the response and captured
logs. Administrative audit events remain in their own PostgreSQL table.
Review production proxy and log collector settings separately before release.

## 2026-11-18 — Presentation preparation

The pinned offline Gitleaks scan completed with no source leaks reported.
The source was mounted read-only and the scan used no network. Private
runtime data remains excluded; this is not a scan of local secret storage
or a complete security audit. Frontend lint and the production build passed.
No public deployment, paid service or personal staff provisioning occurred.

