# Local HTTPS deployment rehearsal

Build Angular with `npm run build` in `frontend`. From the project root,
run `./scripts/Verify-NginxEdge.ps1` with PowerShell 7, curl and Docker.
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
mechanism or paid server is currently configured by this preparation.
