# Local Docker runtime recovery — October 27, 2026

Docker Desktop 4.90.0 failed before its Linux engine started, while renaming sailor-ingest.sock under the local Docker/run directory. The engine pipe was absent. This matched the user's screenshot and recent Docker backend log, not an application or PostgreSQL migration error.

The supported `docker desktop stop --timeout 30` did not complete successfully. After verifying that the engine was unavailable, only the installed Docker Desktop/backend processes were closed. Their executable paths were checked against the installed Docker Desktop directory, and process exit was verified before filesystem changes.

Two ordinary directories were inspected and contained only zero-byte runtime socket entries. They were renamed, preserving their contents as backups, then empty replacements were created:

- LocalAppData/Docker/run → run.lexigo-recovery-20261027T170345Z
- LocalAppData/docker-secrets-engine → docker-secrets-engine.lexigo-recovery-20261027T170345Z

No factory reset, container/image/volume purge, WSL unregister, database deletion, VHDX operation or diagnostics upload was performed. No recursive deletion was used. Both source/backup paths and ordinary parent-directory attributes were checked before renaming. Backup paths are also recorded under ignored work/docker-runtime-recovery.json; they remain outside the repository under the same local application-data parents.

Docker restarted and reported engine 29.7.2. Only the existing LexiGo Compose services database/mail were started. The existing SQL database still contained two users, four courses and two applied migrations. There were no pending email outbox rows. API readiness and the registration page returned HTTP 200 after restarting the app.

This establishes recovery for this session; recurrence after another restart has not been tested. Review an official Docker Desktop update separately if the issue returns. Do not delete the backups or reset persistent Docker data merely because the workaround succeeded.

References: [Docker issue with this socket failure](https://github.com/docker/desktop-feedback/issues/625), [same-version report](https://github.com/docker/desktop-feedback/issues/675), [official troubleshooting options](https://docs.docker.com/desktop/troubleshoot-and-support/troubleshoot/).

## Recurrence after restart — October 28, 2026

The same sailor-ingest.sock rename failure recurred in the Docker backend log. Supported stop failed; only verified processes under the installed LocalAppData/Programs/DockerDesktop directory were stopped. After process exit was confirmed and both ordinary runtime directories were checked to contain only zero-byte socket reparse entries, they were preserved as run.lexigo-recovery-20261028T061022Z and docker-secrets-engine.lexigo-recovery-20261028T061022Z, then empty replacements were created. Desktop restarted hidden and engine29.7.2 responded. No volume, WSL, VHDX, database or container data was deleted/reset. This is a recurrent Desktop issue; a permanent vendor fix/update remains a separate maintenance task.

## Later vocabulary checkpoint — October 28, 2026

The sailor-ingest rename crash recurred after local tests/migration had succeeded. Supported desktop stop printed failure and hung; interrupted it, then stopped only processes whose executable paths were verified under the installed DockerDesktop directory. Safety guards prevented directory moves while processes remained/reappeared. No new socket backup record/directory rename is claimed. The engine subsequently responded29.7.2; existing LexiGo database/mail/storage containers were brought up with compose --wait and were healthy. SQL still contained13migrations/4users and no real lessons/vocabulary. API readiness and both website previews returned200. No reset, volume/WSL/VHDX deletion, diagnostics upload or credential change. The recurring Desktop startup issue still warrants a separate vendor maintenance review.

