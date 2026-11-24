# PostgreSQL recovery

Use PostgreSQL 17 tools and a private directory outside the repository. Treat
a dump as personal data because it can contain account and learning records.
Keep it encrypted at rest and restrict read access to the operators who need it.
Use your deployment's configured PostgreSQL connection and credential store.

Create a custom-format backup with `pg_dump -Fc -f lexigo.dump`. Record the
application version, backup time and latest applied migration alongside it.
Before restoring, verify the expected source and backup checksum and create a
separate empty database. Run `pg_restore --exit-on-error --single-transaction
-d lexigo_restored lexigo.dump` using an authorized restore connection.

Compare the migration history, core table counts and constraints against the
source or backup inventory. Start the matching application build against the
restored database and verify liveness, readiness and the relevant signed-in
flows before using it. The PostgreSQL dump preserves media references. Private
object storage and secrets such as signing keys need their own recovery plans.

The repository drill runs on synthetic schema data, creates a custom-format
dump, restores into a separate database and compares migration, course, skill
and constraint counts. It uses a disposable container with no published ports.
Run it locally with `./scripts/Verify-DatabaseRestore.ps1`. GitHub Actions
invokes the same drill. Production recovery and object restoration require a
separate exercise using the deployed configuration.

## Presentation deployment recovery evidence — 2026-11-24

The disposable SQL recovery drill passed again, including all 66 migrations.
The initial live cutover separately restored six accounts, 161 lessons and
88 dictionary entries, plus private storage metadata; public catalog and
signed storage checks passed afterward. This does not replace a fresh,
signed-in rehearsal of every user's state or an incident-response drill.

The three local cutover archives are now Windows CurrentUser DPAPI-encrypted,
restricted to the owner and SYSTEM, and verified by decrypting and comparing
SHA256. Plaintext local archives and matching temporary server copies were
removed after verification. Their protected location is under ignored
`work/deployment/`. DPAPI recovery depends on the original Windows credentials
and machine; a separately protected portable/offsite recovery copy and full
operational rotation/revocation exercises remain required for long-term use.
