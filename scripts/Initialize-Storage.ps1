$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$envPath = Join-Path $projectRoot '.env'
if (-not (Test-Path -LiteralPath $envPath)) {
    throw 'Prepare the local database .env before initializing storage.'
}

$content = [IO.File]::ReadAllText($envPath)
$names = @('LEXIGO_STORAGE_RPC_SECRET', 'LEXIGO_STORAGE_ACCESS_KEY', 'LEXIGO_STORAGE_SECRET_KEY')
$values = @{}
foreach ($name in $names) {
    $matchesForName = [regex]::Matches($content, "(?m)^$name=(.*?)\r?$")
    if ($matchesForName.Count -gt 1) { throw 'Duplicate storage settings in .env; resolve them privately.' }
    if ($matchesForName.Count -eq 1) { $values[$name] = $matchesForName[0].Groups[1].Value }
}
if ($values.Count -ne 0 -and $values.Count -ne 3) {
    throw 'Incomplete storage credentials; restore all three settings rather than rotating them silently.'
}
if ($values.Count -eq 0) {
    $values[$names[0]] = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant()
    $values[$names[1]] = 'GK' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16)).ToLowerInvariant()
    $values[$names[2]] = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant()
    $lines = $names | ForEach-Object { "$_=$($values[$_])" }
    [IO.File]::AppendAllText($envPath, "`n" + ($lines -join "`n") + "`n")
}
if ($values[$names[0]] -cnotmatch '^[a-f0-9]{64}$' -or
    $values[$names[1]] -cnotmatch '^GK[a-f0-9]{32}$' -or
    $values[$names[2]] -cnotmatch '^[a-f0-9]{64}$') {
    throw 'Storage credential format is invalid. No credential was printed or rotated.'
}

$storageDirectory = Join-Path $projectRoot 'work/storage'
[IO.Directory]::CreateDirectory($storageDirectory) | Out-Null
$configuration = @'
metadata_dir = "/var/lib/garage/meta"
data_dir = "/var/lib/garage/data"
db_engine = "sqlite"
replication_factor = 1
rpc_bind_addr = "127.0.0.1:3901"
rpc_public_addr = "127.0.0.1:3901"
[s3_api]
s3_region = "garage"
api_bind_addr = "[::]:3900"
'@
[IO.File]::WriteAllText((Join-Path $storageDirectory 'garage.toml'), $configuration)
$settings = @{
    'ObjectStorage:Enabled' = 'true'
    'ObjectStorage:ServiceUrl' = 'http://localhost:3900'
    'ObjectStorage:Region' = 'garage'
    'ObjectStorage:Bucket' = 'lexigo-media'
    'ObjectStorage:AccessKey' = $values[$names[1]]
    'ObjectStorage:SecretKey' = $values[$names[2]]
}
$settings | ConvertTo-Json -Compress | dotnet user-secrets set --project (Join-Path $projectRoot 'backend/LanguageLearning.Api') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not save local storage settings.' }
Write-Output 'Local storage credentials prepared privately. Start with: docker compose --profile storage up -d --wait storage'
