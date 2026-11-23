# Private audio storage

The storage foundation uses the S3 SDK with a local Garage service. It is separate from microphone practice: `/speaking` clips remain transient browser memory and are never uploaded by this setup.

## Local setup

Start Docker Desktop and prepare the existing database `.env`, then run from the repository root:

```powershell
./scripts/Initialize-Storage.ps1
docker compose --profile storage up -d --wait storage
```

The script generates three random local credentials once, saves them in ignored `.env`, and passes API settings into .NET User Secrets over stdin. It rejects duplicate, partial or invalid credentials instead of silently rotating them. It writes the non-secret Garage configuration into ignored `work/storage/garage.toml`. Do not print `.env`, User Secrets, container environment variables or signed URLs in support logs.

Garage's S3 port binds only to `127.0.0.1:3900`. RPC stays inside the container; no public website or administrative HTTP endpoint is enabled. Metadata and object bytes use separate persistent Docker volumes. The optional `storage` profile leaves ordinary database/mail startup independent. Never run `docker compose down -v` against project data.

The image is pinned to Garage v2.5.0 and its pulled digest. AWSSDK.S3 4.0.104.7 and transitive AWSSDK.Core 4.0.102.14 are recorded in NuGet lockfiles. API configuration defaults to disabled until explicitly configured. Disabled mode does not construct a client or fall back to ambient cloud credentials.

## Internal boundary

`MediaObjectStore` uses server-generated `media/{uuid}` object keys. Its low-level stream method checks transport bounds and MIME only; this does **not** establish valid audio. The authenticated workflow now uses WaveAudioInspector and a durable SQL reservation before writing canonical PCM WAV bytes. It checks actual format/duration and requires source/permission metadata plus independent language review. MIME labels and file extensions are insufficient. See MEDIA_WORKFLOW.md for the implemented lifecycle and access rules.

Files are private. GET playback links expire within five minutes and request `private, no-store` response caching. Signed links are temporary bearer credentials: do not log, persist in public DTO caches or share them. An issued URL may remain usable until expiry after a permission change; future API services must check current rights before issuing each link. Deleting the object invalidates access to its bytes. Existing links do not grant listing, writing or deletion permission.

Garage's observed expiry response is HTTP 400 with the S3 `InvalidRequest` code. The integration test first proves the same link returns the original bytes successfully, waits beyond its five-second lifetime, then checks rejection. Anonymous and altered-signature requests return HTTP 403. Provider error codes must not be guessed from AWS behavior.

The configured endpoint must be an HTTP(S) origin without embedded credentials, paths, query or fragment. HTTP is permitted only for loopback in Development; production requires HTTPS. The AWS signing request explicitly follows that validated protocol. Credentials are bounded and required only when enabled. No ACL/public-read flag is sent.

## Remaining integration and release gates

The original media upload/delete APIs now have language/owner/MFA checks, PCM content inspection, source/license metadata, independent review, SQL linkage and durable pending/deletion cleanup. Reviewed exercise attachment and owned saved-session playback now support Listening selection/transcription; see LISTENING_LESSONS.md. Other codecs, Speaking, audio Matching/Alphabet association, real licensed curriculum and browser/device checks remain required. Reference protection must expand as future vocabulary/dialogue/session/avatar media modules are added. See MEDIA_WORKFLOW.md for evidence and remaining gates.

Single-node development storage has no redundancy and is **not** the production deployment. Production provider/least-privilege credentials, HTTPS reverse proxy, backup and actual restore drills, retention/deletion, monitoring, image vulnerability/license review and all existing security release gates remain open. Garage does not provide S3 object versioning; application revisions must continue using distinct immutable object keys. Do not assume AWS bucket-policy semantics apply to Garage's key permissions.

Primary references: [Garage quick start](https://garagehq.deuxfleurs.fr/documentation/quick-start/), [configuration](https://garagehq.deuxfleurs.fr/documentation/reference-manual/configuration/), [S3 compatibility](https://garagehq.deuxfleurs.fr/documentation/reference-manual/s3-compatibility/). The quick start explicitly limits single-node setups to development because they lack redundancy.

## Verification checkpoint — 2026-10-28

Full backend suite:184passed,0failed/skipped (`work/test-results/storage-final.trx`), including20 storage cases against an isolated real Garage container. Covered original bytes/MIME/length, unsigned denial, tampered signature, successful playback before real expiry and rejection afterward, deletion, key/lifetime/input bounds, endpoint/TLS rules and disabled configuration. C# whitespace verification passed. NuGet advisory check including transitives reported0known findings (`work/storage-dependency-audit.json`); this does not establish an independent audit or a container-image scan. Repeat initialization preserved the .env credential file. Main storage container is healthy; API live/ready, site and phone preview returned200. No SQL migration or public upload API was added.
