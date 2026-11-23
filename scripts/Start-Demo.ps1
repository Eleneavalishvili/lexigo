#requires -Version 7.0
[CmdletBinding()]
param([switch]$OpenBrowser)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$frontendPath = Join-Path $projectRoot 'frontend'
$logPath = Join-Path $projectRoot 'work'

function Test-DemoPage([string]$Address) {
    try {
        return (Invoke-WebRequest -Uri $Address -TimeoutSec 3).StatusCode -eq 200
    } catch {
        return $false
    }
}

function Wait-DemoPage([string]$Address) {
    for ($attempt = 0; $attempt -lt 45; $attempt++) {
        if (Test-DemoPage $Address) { return }
        Start-Sleep -Seconds 1
    }
    throw "The local service did not become ready at $Address. Check its log in work."
}

Push-Location $projectRoot
try {
    if (!(Test-Path -LiteralPath '.env')) { throw 'Create .env using the README setup instructions first.' }
    if (!(Test-Path -LiteralPath 'frontend/node_modules/@angular/cli/bin/ng.js')) {
        throw 'Install the frontend dependencies with npm ci in the frontend folder first.'
    }
    & docker info --format '{{.ServerVersion}}' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Open Docker Desktop and wait for its engine, then retry.' }
    $services = @('database', 'mail')
    if (Test-Path -LiteralPath 'work/storage/garage.toml') { $services += 'storage' }
    & docker compose --profile storage up -d --wait @services
    if ($LASTEXITCODE -ne 0) { throw 'The local Docker services did not become ready.' }
    New-Item -ItemType Directory -Path $logPath -Force | Out-Null
    $run = Get-Date -Format 'yyyyMMdd-HHmmss'
    $apiReady = Test-DemoPage 'http://localhost:5187/health/ready'
    $siteReady = Test-DemoPage 'http://localhost:4200/'
    $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    if (!$apiReady -and ($listeners | Where-Object Port -eq 5187)) {
        throw 'Port 5187 is occupied by an unhealthy service. Inspect it before starting another API.'
    }
    if (!$siteReady -and ($listeners | Where-Object Port -eq 4200)) {
        throw 'Port 4200 is occupied. Wait for that preview or inspect its log.'
    }

    if (!$apiReady) {
        & dotnet build backend/LanguageLearning.Api --configuration Release --no-restore -m:1
        if ($LASTEXITCODE -ne 0) { throw 'The API build failed.' }
        & dotnet run --project backend/LanguageLearning.Api --configuration Release --no-build --launch-profile http -- --initialize-database
        if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed.' }
        $settings = @{
            ASPNETCORE_ENVIRONMENT = 'Development'
            ASPNETCORE_URLS = 'http://localhost:5187'
            Smtp__Host = '127.0.0.1'
            Smtp__Port = '1025'
            Smtp__Username = ' '
            Smtp__Password = ' '
            Smtp__DeliveryEnabled = 'true'
            Smtp__SenderAddress = 'hello@lexigo.test'
        }
        $previous = @{}
        foreach ($key in $settings.Keys) {
            $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
            [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
        }
        try {
            $apiStart = @{
                FilePath = (Get-Command dotnet).Source
                WindowStyle = 'Hidden'
                ArgumentList = @('run','--project','backend/LanguageLearning.Api','--configuration','Release','--no-build','--launch-profile','http')
                WorkingDirectory = $projectRoot
                RedirectStandardOutput = Join-Path $logPath "demo-api-$run.log"
                RedirectStandardError = Join-Path $logPath "demo-api-$run.err.log"
            }
            Start-Process @apiStart | Out-Null
        } finally {
            foreach ($key in $previous.Keys) {
                [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process')
            }
        }
    } else {
        Write-Host 'Using the API already running on port 5187.'
    }
    if (!$siteReady) {
        $siteStart = @{
            FilePath = (Get-Command node).Source
            WindowStyle = 'Hidden'
            ArgumentList = @('node_modules/@angular/cli/bin/ng.js','serve','--host','localhost','--port','4200')
            WorkingDirectory = $frontendPath
            RedirectStandardOutput = Join-Path $logPath "demo-website-$run.log"
            RedirectStandardError = Join-Path $logPath "demo-website-$run.err.log"
        }
        Start-Process @siteStart | Out-Null
    }
    Wait-DemoPage 'http://localhost:5187/health/ready'
    Wait-DemoPage 'http://localhost:4200/'
    Write-Host 'LexiGo is ready at http://localhost:4200'
    Write-Host 'Local confirmation inbox: http://localhost:8025'
    Write-Host 'API documentation: http://localhost:5187/swagger'
    if ($OpenBrowser) { Start-Process 'http://localhost:4200/' }
} finally {
    Pop-Location
}
