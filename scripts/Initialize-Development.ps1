[CmdletBinding()]
param([switch]$InstallStarterContent)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $projectRoot '.env'
if (-not (Test-Path -LiteralPath $settingsPath)) { throw 'Create .env from .env.example and set POSTGRES_PASSWORD first.' }
$passwordSetting = Get-Content -LiteralPath $settingsPath | Where-Object { $_ -match '^POSTGRES_PASSWORD=' } | Select-Object -First 1
if (-not $passwordSetting) { throw 'POSTGRES_PASSWORD is missing in .env.' }
$developmentPassword = $passwordSetting.Substring('POSTGRES_PASSWORD='.Length).Trim()
if ([string]::IsNullOrWhiteSpace($developmentPassword)) { throw 'Set a random local POSTGRES_PASSWORD in .env first.' }
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString('Host=localhost;Port=5433;Database=lexigo;Username=lexigo_dev')
$connection['Password'] = $developmentPassword
$apiProject = Join-Path $projectRoot 'backend/LanguageLearning.Api'
@{ 'ConnectionStrings:DefaultConnection' = $connection.get_ConnectionString() } |
    ConvertTo-Json -Compress |
    & dotnet user-secrets set --project $apiProject | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not store the local development connection.' }
& dotnet run --project $apiProject --launch-profile http -- --initialize-database
if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed.' }
Write-Host 'Local account database initialized. No administrator account or default password was created.'
if ($InstallStarterContent) {
    & dotnet run --project $apiProject --no-build --launch-profile http -- --install-starter-lessons
    if ($LASTEXITCODE -ne 0) { throw 'Starter lesson installation failed.' }
    & dotnet run --project $apiProject --no-build --launch-profile http -- --install-starter-dialogues
    if ($LASTEXITCODE -ne 0) { throw 'Starter conversation installation failed.' }
}
