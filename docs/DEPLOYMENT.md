# Deployment and HTTPS verification

## Public submission references — 2026-11-25

At the user's explicit request, public static Swagger UI documentation is now
available at https://206.189.25.5/api-docs/index.html. Its same-origin OpenAPI
snapshot documents213paths. It uses the installed Swashbuckle Swagger UI assets
and their Apache license; external validation and persistent authorization are
disabled. Production API authentication, CSRF, rate limits, private storage,
database ports and the existing /swagger404 restriction are unchanged. No
Development environment or new paid service was enabled. Swagger documentation
does not grant access to protected API operations.

Public presentation downloads are under /presentation/: the original45slide
PDF, Georgian speaker script DOCX, reviewed11page C4 architecture PDF and
combined56page presentation/C4 PDF. Copies of the final C4 and combined PDF
are in docs/presentation for GitHub submission. The architecture PDF includes
the four C4 levels, actual class/dependency diagrams, deployment, authorization,
Signals/audio/recording/globe explanations, SQL/migrations and a code-showing map.
All11pages were rendered and inspected; overlap and unsupported arrow glyphs
were corrected before publication. These references do not mark remaining
baseline requirements complete.

These server-hosted links share the existing scheduled server-deletion date
below. GitHub copies remain independently available. No hosting purchase or
extension of the expiry schedule was made.

## Live presentation deployment — 2026-11-24

Public URL: **https://206.189.25.5/**. This is the existing Angular/.NET
application, hosted on the approved DigitalOcean Ubuntu 24.04 server with
1 GiB RAM and one CPU at the $6/month rate. No paid backup, managed database,
volume, domain or other paid add-on was enabled. DigitalOcean meters usage
until deletion; switching the server off does not stop billing, and accrued
usage can be invoiced afterward.

The server's scheduled deletion deadline is 2026-12-03 20:15:26 UTC
(December 4, 00:15:26 Asia/Tbilisi), ten days after creation. The root-owned
expiry service passed an authenticated nondeleting identity check and its
enabled minute timer has completed successful early runs. This is a
best-effort deletion schedule, not a provider-enforced spending cap; verify
actual deletion in DigitalOcean. Earlier manual deletion is possible.

The release passed these checks:

- Current container scans found no known high/critical findings in the API,
  patched PostgreSQL/Nginx/certificate-tool images and private Garage image.
  Cached vulnerable images were blocked and replaced. Scanner availability
  and database freshness remain release requirements; scans are not a full audit.
- The corrected Nginx image passed the real production-mode HTTPS sign-in,
  refresh/restart and saved-lesson rehearsal before public startup.
- Existing SQL data and private-storage metadata were restored. The server
  contains six accounts, 161 lessons and 88 vocabulary entries. Old refresh
  sessions were revoked; obsolete queued local email bodies were suppressed
  without claiming delivery. The original local database was not reset.
- PostgreSQL requires TLS on its private network. The application uses a
  separate non-superuser role; database and Garage ports are not published.
- Public HTTPS returned 200 for the homepage, catalog, login, learn deep
  link and Russian reference; the catalog shows 40/40/40/41 lessons.
  Public health/Swagger returned 404; unsigned private storage returned 403.
- Signed private upload/download/delete and unsigned denial passed through
  the actual HTTPS gateway. Brevo STARTTLS authentication and NOOP passed on
  port 2525; no test email was sent. Port 587 is blocked by DigitalOcean.
  This verifies connection/authentication, not a new inbox-delivery test.
- Let's Encrypt issued a trusted IP-address certificate. A real staging
  renewal rehearsal succeeded; the twice-daily renewal timer is enabled.
- Source secret scanning found no leaks. Private runtime files, screenshots,
  working notes and local key/backup files are excluded from GitHub.

Evidence is retained privately under `work/deployment/`, particularly
`final-images-security.log`, `patched-nginx-rehearsal.log`,
`private-services-final.log`, `certificate-renewal-check.log` and
`expiry-activation-verified.log`. Remaining baseline requirements and staff
account/MFA setup are tracked separately; this is not a full-scope completion claim.

## Local HTTPS rehearsal

Build Angular with `npm run build` in `frontend`. From the project root,
build the patched edge image with
`docker build --file config/deployment/Nginx.Dockerfile --tag lexigo-nginx:patched .`,
then run `./scripts/Verify-NginxEdge.ps1` with PowerShell 7, curl and Docker.
The script serves the production frontend output through Nginx with a
temporary, explicitly trusted certificate for `lexigo.example.invalid`.
It binds the edge to ephemeral loopback ports and uses a disposable upstream.
The temporary containers, network and private key are removed afterward.

The check covers HTTPS, HTTP redirection, browser route reloads, API routing,
forwarded header replacement, HSTS, oversized requests, hidden files, missing
assets and redacted access logs. Nginx logs time, status, duration and byte
count. The rehearsal includes a synthetic token to catch accidental logging
of query strings. The hosted CI job is configured to run the same script.

For the real API rehearsal, build the non-root image from the project root
and pass it to the same runner.

```powershell
docker build --file backend/LanguageLearning.Api/Dockerfile --tag lexigo-api:local-rehearsal .
./scripts/Verify-NginxEdge.ps1 -ApiImage lexigo-api:local-rehearsal
```

The runner uses a disposable database and starts the API in Production mode
with generated credentials and a private key volume. It verifies registration,
confirmation from the disposable outbox, secure refresh-cookie attributes,
session refresh after an API restart, course enrollment, answer grading and
persisted progress. External SMTP is disabled. The earlier proxy-only check
also verifies replacement of client-supplied forwarding headers.

## Production preparation

The Nginx template uses the official image's template processing. Set
`LEXIGO_HOST` to the approved hostname and set `NGINX_ENVSUBST_FILTER` to
`^LEXIGO_HOST$`. Mount the Angular production output and TLS files read-only.
The certificate chain and key are expected under `/etc/lexigo/tls`. Only the
edge publishes HTTP and HTTPS. Put the API at `api:8080` on the private
network and verify liveness and readiness from that network.

The Compose template at `config/deployment/compose.yml` sets the permitted
host, HTTPS email origin and exact Nginx peer address. Set
`LEXIGO_APP_SUBNET` to a dedicated subnet and `LEXIGO_EDGE_ADDRESS` to the
edge's address in that subnet. Provide `LEXIGO_API_IMAGE`,
`LEXIGO_FRONTEND_DIR`, `LEXIGO_TLS_DIR` and `LEXIGO_API_ENV_FILE` from the
release and private configuration. The API environment file supplies the
database connection, JWT signing key and any enabled email or storage
settings. Keep PostgreSQL and private storage on restricted networks. The
named volume persists API protection keys across replacement. Restrict access
to it and plan encrypted storage and recovery alongside the JWT signing key.
Confirm certificate renewal and deployed log retention before release.

The rehearsal uses password authentication on its disposable Docker network.
Configure verified database TLS for a deployed PostgreSQL connection. A
deployment that uses Kerberos must provide the corresponding runtime library.
[Npgsql connection options](https://www.npgsql.org/doc/connection-string-parameters.html)

## Migration and rollback

Record the application image, database migration and deployment configuration
versions. Back up the database before a schema change and verify the backup
can be restored to a separate database using [the recovery procedure](DATABASE_RECOVERY.md).
Review the SQL for the exact release and apply it once using a migration
identity. Give the application runtime only the database permissions it needs
for normal operation. The development initializer is for local setup.

Start the candidate app and verify readiness, the catalog and signed-in flows
before moving traffic. If the previous application version supports the
applied schema, roll back to that version. For incompatible changes, use the
verified restore procedure and reconcile any writes made after the backup
before switching databases. The deployed network, real credentials, external
services and release rollback still require a staging rehearsal.

## Small presentation-server rehearsal

Use `./scripts/Verify-NginxEdge.ps1 -ApiImage lexigo-api:deployment -SmallServer`
to repeat the functional HTTPS checks with the API capped at 384 MiB,
PostgreSQL at 192 MiB and Nginx at 64 MiB. PostgreSQL uses 32 MiB shared
buffers and at most 30 connections in this mode. Container swap is disabled
for these checks. This mode passed locally on 2026-11-23 with the current
production image. It does not emulate a complete 1 GiB VM: the operating
system, Docker and private object storage also need memory, and this is not
a concurrent-load test. Build release artifacts locally before transfer;
do not run Angular/.NET builds on the small presentation server.

DigitalOcean is metered while a Droplet exists, including while powered off.
A ten-day deployment must have a verified deletion mechanism before creation.
A final invoice can arrive after deletion for previously accrued usage.
Turning off the website or server is not cancellation. No automatic-deletion
mechanism is established by these templates alone; the verified live state
is recorded above.

## Presentation expiry guard

`tools/deployment/expire_droplet.py` checks one configured DigitalOcean ID,
the exact `lexigo-presentation` name and creation
timestamp before deletion. Its configured UTC deadline must be after
creation and at most ten days later. Before the deadline it makes no API
request unless `--dry-run` is selected to verify identity without deletion. Unit tests
cover early execution, deadline boundaries, wrong targets and provider failure.
The human-visible `lexigo-expiring` tag is not an API identity check: the
minimum token lacks `tag:read`, so DigitalOcean hides tags in its response.
The guard uses the immutable numeric ID and expected creation timestamp
alongside the name, without expanding the token's permissions.

The accompanying `lexigo-expiry.service` and `.timer` are installation
templates, not an already scheduled deletion. Install them only on the
approved server. Place the script at `/opt/lexigo/tools/deployment/`, the
private provider token at `/etc/lexigo/private/expiry-token`, and a JSON file
at `/etc/lexigo/private/expiry.json` containing `droplet_id`, `name`,
`created_at` and `delete_after`. Both timestamps must explicitly include UTC.
Use a root-owned directory with mode 0700 and private files with mode 0600.
The token needs only `droplet:delete` and its required read scopes; it must
remain valid beyond the deletion deadline. Never commit it or put it into
startup metadata, shell arguments, logs or the application's environment.

After identity checks, install and enable the timer, inspect its next run
and run the service before the deadline to verify that it reports “Not due”.
Use a separate synthetic configuration in local tests to exercise due
deletion safely. The timer retries every minute, including after reboot.
It depends on a functioning server clock, scheduler, network, credentials
and provider API; it is not a provider-enforced spending cap. Arrange an
independent deadline check and verify actual resource deletion. Deletion
permanently takes the public website and its server data offline: export
required data to protected local storage beforehand.
