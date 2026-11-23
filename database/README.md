# Database scripts

- `schema.sql`: idempotent SQL for the EF migration history, including course and unit archive columns. It creates the application schema and includes the reference data from migrations.
- `seed.sql`: inserts any missing application roles without users, passwords or elevated accounts. Safe to rerun after schema creation.
- EF migrations are authoritative for schema evolution. Keep SQL regenerated as migrations are added.

Run `./scripts/Verify-DatabaseRestore.ps1` from the repository root to check that the schema can be applied twice and restored to a fresh database. The local development volume is persistent and separate from Vacay.
