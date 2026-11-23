[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$schema = Join-Path $root 'database/schema.sql'
$seed = Join-Path $root 'database/seed.sql'
$migrationDirectory = Join-Path $root 'backend/LanguageLearning.Infrastructure/Data/Migrations'
$migrationIds = @(Get-ChildItem $migrationDirectory -File -Filter '*.cs' |
    Where-Object { $_.BaseName -match '^\d{14}_\w+$' } |
    ForEach-Object { $_.BaseName })
$schemaText = Get-Content $schema -Raw
foreach ($migrationId in $migrationIds) {
    if (!$schemaText.Contains("'$migrationId'")) { throw "Schema SQL does not include migration $migrationId." }
}
$name = 'lexigo-restore-check-' + [Guid]::NewGuid().ToString('N')
$previousPassword = $env:POSTGRES_PASSWORD
try {
    $env:POSTGRES_PASSWORD = [Guid]::NewGuid().ToString('N')
    $container = & docker run -d --rm --name $name -e POSTGRES_PASSWORD postgres:17
    if ($LASTEXITCODE -ne 0) { throw 'Could not start the disposable PostgreSQL server.' }
}
finally {
    $env:POSTGRES_PASSWORD = $previousPassword
}

try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        & docker exec $container pg_isready -h 127.0.0.1 -U postgres -q
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 1
    }
    if (!$ready) { throw 'Disposable PostgreSQL did not become ready.' }

    & docker cp $schema "${container}:/tmp/lexigo-schema.sql"
    if ($LASTEXITCODE -ne 0) { throw 'Could not copy the schema.' }
    & docker exec $container psql -X -q -v ON_ERROR_STOP=1 -U postgres -d postgres -f /tmp/lexigo-schema.sql
    if ($LASTEXITCODE -ne 0) { throw 'Could not apply the LexiGo schema.' }
    & docker exec $container psql -X -q -v ON_ERROR_STOP=1 -U postgres -d postgres -f /tmp/lexigo-schema.sql
    if ($LASTEXITCODE -ne 0) { throw 'The schema script failed on reapplication.' }

    & docker cp $seed "${container}:/tmp/lexigo-seed.sql"
    if ($LASTEXITCODE -ne 0) { throw 'Could not copy the seed script.' }
    $verifySeedRecovery = @'
DELETE FROM "AspNetRoles" WHERE "NormalizedName" = 'LEARNER';
DELETE FROM "VocabularyItems" WHERE "Id" = '15110000-0000-0000-0000-000000000001';
UPDATE "VocabularyItems" SET "Example" = 'Existing example'
WHERE "Id" = '15110000-0000-0000-0000-000000000002';
'@
    & docker exec $container psql -X -q -v ON_ERROR_STOP=1 -U postgres -d postgres -c $verifySeedRecovery
    if ($LASTEXITCODE -ne 0) { throw 'Could not prepare seed recovery verification.' }
    foreach ($repeat in 1..2) {
        & docker exec $container psql -X -q -v ON_ERROR_STOP=1 -U postgres -d postgres -f /tmp/lexigo-seed.sql
        if ($LASTEXITCODE -ne 0) { throw "Baseline seed failed on application $repeat." }
    }

    & docker exec $container pg_dump -Fc -U postgres -d postgres -f /tmp/lexigo.dump
    if ($LASTEXITCODE -ne 0) { throw 'Backup failed.' }
    & docker exec $container createdb -U postgres lexigo_restored
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the empty restore target.' }
    & docker exec $container pg_restore --exit-on-error --single-transaction -U postgres -d lexigo_restored /tmp/lexigo.dump
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

    $probe = @'
SELECT
  (SELECT count(*) FROM "__EFMigrationsHistory") || ':' ||
  (SELECT count(*) FROM "Courses") || ':' ||
  (SELECT count(*) FROM "Skills") || ':' ||
  (SELECT count(*) FROM pg_constraint
   WHERE conrelid = 'public."Users"'::regclass AND convalidated) || ':' ||
  (SELECT count(*) FROM "AspNetRoles") || ':' ||
  (SELECT count(*) FROM "Users") || ':' ||
  (SELECT count(*) FROM "Languages") || ':' ||
  (SELECT count(*) FROM "VocabularyItems" WHERE "Example" <> '') || ':' ||
  (SELECT count(*) FROM "VocabularyItems"
   WHERE ("Id" = '15110000-0000-0000-0000-000000000001' AND "Example" = '¡Hola! ¿Cómo estás?')
      OR ("Id" = '15110000-0000-0000-0000-000000000002' AND "Example" = 'Existing example')) || ':' ||
  (SELECT count(*) FROM information_schema.columns
   WHERE table_schema = 'public' AND table_name IN ('Courses', 'Units')
     AND column_name = 'ArchivedAt' AND data_type = 'timestamp with time zone' AND is_nullable = 'YES');
'@
    $original = & docker exec $container psql -X -At -v ON_ERROR_STOP=1 -U postgres -d postgres -c $probe
    if ($LASTEXITCODE -ne 0) { throw 'Source verification failed.' }
    $restored = & docker exec $container psql -X -At -v ON_ERROR_STOP=1 -U postgres -d lexigo_restored -c $probe
    if ($LASTEXITCODE -ne 0 -or $original -ne $restored -or $restored -notmatch "^$($migrationIds.Count):4:17:[1-9]\d*:7:0:5:24:2:2$") {
        throw 'The restored schema or seed state does not match the expected database.'
    }
    Write-Output "PostgreSQL seed and restore verified (migrations:courses:skills:user constraints:roles:users:languages:starter words:seed checks:archive columns = $restored)."
}
finally {
    & docker stop $container | Out-Null
}
