[CmdletBinding()]
param([string]$BrowserChannel, [string]$Spec, [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release', [string]$ApiAssembly, [switch]$PrivateAudio, [switch]$ExportOpenApi, [int]$Workers)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$api = Join-Path $root 'backend/LanguageLearning.Api'
$frontend = Join-Path $root 'frontend'
if ($ApiAssembly) { $apiAssembly = (Resolve-Path -LiteralPath $ApiAssembly).Path }
else { $apiAssembly = Join-Path $api "bin/$Configuration/net10.0/LanguageLearning.Api.dll" }
if (!(Test-Path $apiAssembly)) { throw "Build LanguageLearning.Api in $Configuration first." }
$listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
if ($listeners | Where-Object { $_.Port -in 4280,5280 }) {
    throw 'Ports 4280 and 5280 must be free for the test preview.'
}
$processOptions = @{ PassThru = $true }
if ($IsWindows) { $processOptions.WindowStyle = 'Hidden' }
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$node = (Get-Command node -CommandType Application | Select-Object -First 1).Source

$runId = [Guid]::NewGuid().ToString('N')
$run = New-Item -ItemType Directory -Path (Join-Path $root "work/learner-browser-$runId") -Force
$database = "lexigo-browser-db-$runId"
$inbox = "lexigo-browser-inbox-$runId"
$storage = "lexigo-browser-storage-$runId"
$apiProcess = $frontendProcess = $null
$settings = @{
    POSTGRES_PASSWORD = [Guid]::NewGuid().ToString('N')
    ASPNETCORE_ENVIRONMENT = 'Development'
    ASPNETCORE_URLS = 'http://localhost:5280'
    AccountEmail__WebOrigin = 'http://localhost:4280'
    # All disposable accounts share one loopback IP. Keep this test-only budget
    # above the combined account mutations of the desktop and phone suites.
    AccountRateLimit = '2500'
    # All isolated browser accounts share loopback and a ten-minute limiter window.
    # A full desktop/phone run requests CSRF tokens for hundreds of mutations.
    SessionRefreshRateLimit = '3000'
    LearningRateLimit = '600'
    ContentReadRateLimit = '600'
    ContentWriteRateLimit = '300'
    Smtp__Host = '127.0.0.1'
    Smtp__SenderAddress = 'hello@lexigo.test'
    Smtp__Username = ' '
    Smtp__Password = ' '
    Smtp__DeliveryEnabled = 'true'
    Smtp__Port = $null
    ConnectionStrings__DefaultConnection = $null
    LEXIGO_E2E_URL = 'http://localhost:4280'
    LEXIGO_E2E_INBOX_PORT = $null
    LEXIGO_E2E_DATABASE = $database
    LEXIGO_E2E_BROWSER = $BrowserChannel
    LEXIGO_E2E_PRIVATE_AUDIO = if ($PrivateAudio) { 'true' } else { $null }
    ObjectStorage__Enabled = if ($PrivateAudio) { 'true' } else { 'false' }
    ObjectStorage__CleanupEnabled = 'false'
    ObjectStorage__Region = 'garage'
    ObjectStorage__Bucket = 'lexigo-browser'
    ObjectStorage__ServiceUrl = $null
    ObjectStorage__AccessKey = if ($PrivateAudio) { 'GK' + [Guid]::NewGuid().ToString('N') } else { $null }
    ObjectStorage__SecretKey = if ($PrivateAudio) { [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N') } else { $null }
    GARAGE_RPC_SECRET = if ($PrivateAudio) { [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N') } else { $null }
    GARAGE_DEFAULT_ACCESS_KEY = $null
    GARAGE_DEFAULT_SECRET_KEY = $null
    GARAGE_DEFAULT_BUCKET = $null
    LEXIGO_E2E_PLAN_LIFECYCLE = if ($Spec -eq 'plan-administration.spec.ts') { 'true' } else { $null }
}
$previous = @{}
foreach ($key in $settings.Keys) {
    $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
    [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
}

try {
    if ($PrivateAudio) {
        $config = Join-Path $run 'garage.toml'
        @'
metadata_dir = "/tmp/meta"
data_dir = "/tmp/data"
db_engine = "sqlite"
replication_factor = 1
rpc_bind_addr = "127.0.0.1:3901"
rpc_public_addr = "127.0.0.1:3901"
[s3_api]
s3_region = "garage"
api_bind_addr = "[::]:3900"
'@ | Set-Content $config
        $env:GARAGE_DEFAULT_ACCESS_KEY = $env:ObjectStorage__AccessKey
        $env:GARAGE_DEFAULT_SECRET_KEY = $env:ObjectStorage__SecretKey
        $env:GARAGE_DEFAULT_BUCKET = $env:ObjectStorage__Bucket
        docker run --rm -d --name $storage -p 127.0.0.1::3900 `
            --mount "type=bind,source=$config,target=/etc/garage.toml,readonly" `
            -e GARAGE_RPC_SECRET -e GARAGE_DEFAULT_ACCESS_KEY -e GARAGE_DEFAULT_SECRET_KEY -e GARAGE_DEFAULT_BUCKET `
            --entrypoint /garage dxflrs/garage:v2.5.0@sha256:677e5b758e4425a325b811c425c026bf35a4bf42f7ddbaa773de3f4fd779a5a5 `
            server --single-node --default-bucket | Out-Null
        if ($LASTEXITCODE) { throw 'Could not start disposable private storage.' }
        $storagePort = (docker port $storage 3900).Split(':')[-1]
        $env:ObjectStorage__ServiceUrl = "http://127.0.0.1:$storagePort"
    }
    docker run --rm -d --name $database -p 127.0.0.1::5432 -e POSTGRES_PASSWORD postgres:17 | Out-Null
    if ($LASTEXITCODE) { throw 'Could not start disposable PostgreSQL.' }
    docker run --rm -d --name $inbox -p 127.0.0.1::1025 -p 127.0.0.1::8025 axllent/mailpit:v1.27 | Out-Null
    if ($LASTEXITCODE) { throw 'Could not start disposable Mailpit.' }
    $dbPort = (docker port $database 5432).Split(':')[-1]
    $env:LEXIGO_E2E_INBOX_PORT = (docker port $inbox 8025).Split(':')[-1]
    $env:Smtp__Port = (docker port $inbox 1025).Split(':')[-1]
    $env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=$dbPort;Database=postgres;Username=postgres;Password=$env:POSTGRES_PASSWORD"
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        docker exec $database pg_isready -h 127.0.0.1 -q
        if (!$LASTEXITCODE) { break }
        Start-Sleep -Seconds 1
    }
    if ($LASTEXITCODE) { throw 'Disposable PostgreSQL did not become ready.' }

    Push-Location $api
    try {
        & $dotnet $apiAssembly --initialize-database *> (Join-Path $run 'database.log')
        if ($LASTEXITCODE) { throw "Database initialization failed. See $run." }
        & $dotnet $apiAssembly --install-starter-lessons *> (Join-Path $run 'curriculum.log')
        if ($LASTEXITCODE) { throw "Starter curriculum installation failed. See $run." }
        & $dotnet $apiAssembly --install-starter-vocabulary *> (Join-Path $run 'vocabulary.log')
        if ($LASTEXITCODE) { throw "Starter vocabulary installation failed. See $run." }
        & $dotnet $apiAssembly --install-starter-dialogues *> (Join-Path $run 'dialogues.log')
        if ($LASTEXITCODE) { throw "Starter dialogue installation failed. See $run." }
    }
    finally { Pop-Location }

    $proxy = Join-Path $run 'proxy.json'
    '{"/api/**":{"target":"http://localhost:5280"}}' | Set-Content $proxy
    $apiProcess = Start-Process $dotnet @processOptions -WorkingDirectory $api `
        -ArgumentList ('"' + $apiAssembly + '"') `
        -RedirectStandardOutput (Join-Path $run 'api.log') -RedirectStandardError (Join-Path $run 'api.err.log')
    $frontendProcess = Start-Process $node @processOptions -WorkingDirectory $frontend `
        -ArgumentList 'node_modules/@angular/cli/bin/ng.js', 'serve', '--port', '4280', '--proxy-config', ('"' + $proxy + '"') `
        -RedirectStandardOutput (Join-Path $run 'frontend.log') -RedirectStandardError (Join-Path $run 'frontend.err.log')
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try {
            $response = Invoke-WebRequest http://localhost:4280/api/v1/courses -TimeoutSec 2 -SkipHttpErrorCheck
            if ($response.StatusCode -eq 200) { $ready = $true; break }
        }
        catch [System.Net.Http.HttpRequestException] { }
        catch [System.Threading.Tasks.TaskCanceledException] { }
        if ($apiProcess.HasExited -or $frontendProcess.HasExited) { throw "Test preview stopped. See $run." }
        Start-Sleep -Seconds 1
    }
    if (!$ready) { throw "Test preview did not become ready. See $run." }

    $externalOrigin = 'https://external.example.test'
    $crossOriginCatalog = Invoke-WebRequest http://localhost:5280/api/v1/courses `
        -Headers @{ Origin = $externalOrigin } -TimeoutSec 15 -SkipHttpErrorCheck
    $crossOriginLogin = Invoke-WebRequest http://localhost:5280/api/v1/auth/login -Method Options `
        -Headers @{ Origin = $externalOrigin; 'Access-Control-Request-Method' = 'POST' } `
        -TimeoutSec 15 -SkipHttpErrorCheck
    if ($crossOriginCatalog.StatusCode -ne 200 -or
        $crossOriginCatalog.Headers.ContainsKey('Access-Control-Allow-Origin') -or
        $crossOriginLogin.Headers.ContainsKey('Access-Control-Allow-Origin')) {
        throw 'The backend did not enforce its same-origin API policy.'
    }

    if ($ExportOpenApi) {
        $document = Invoke-WebRequest http://localhost:5280/swagger/v1/swagger.json -TimeoutSec 20
        $parsed = $document.Content | ConvertFrom-Json
        if (!$parsed.openapi -or !$parsed.paths) { throw 'The development API returned an invalid OpenAPI document.' }
        $destination = Join-Path $root 'docs/openapi.json'
        Set-Content -LiteralPath $destination -Value $document.Content -Encoding utf8NoBOM
        Write-Output "Exported OpenAPI to $destination"
    }

    Push-Location $frontend
    try {
        $playwrightArguments = @()
        if ($Spec) { $playwrightArguments += $Spec }
        if ($env:LEXIGO_E2E_PLAN_LIFECYCLE -eq 'true') { $Workers = 1 }
        if ($Workers -gt 0) { $playwrightArguments += "--workers=$Workers" }
        npm run test:e2e -- @playwrightArguments | Tee-Object -FilePath (Join-Path $run 'browser-results.log')
        if ($LASTEXITCODE) { throw 'Browser tests failed.' }
    }
    finally { Pop-Location }
}
finally {
    foreach ($process in @($apiProcess, $frontendProcess)) {
        if ($process -and !$process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
    }
    foreach ($key in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process')
    }
    docker stop $inbox $database 2>$null | Out-Null
    if ($PrivateAudio) { docker stop $storage 2>$null | Out-Null }
}
