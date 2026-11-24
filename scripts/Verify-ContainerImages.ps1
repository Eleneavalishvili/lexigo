[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [hashtable]$Images,
    [switch]$Offline
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$configuration = Get-Content -LiteralPath (Join-Path $projectRoot 'config/security-scanners.json') -Raw | ConvertFrom-Json
$scanner = $configuration.trivyImage
$cache = Join-Path $projectRoot 'work/security/trivy-cache'
$reports = Join-Path $projectRoot 'work/security/images'
New-Item -ItemType Directory -Path $cache, $reports -Force | Out-Null
. (Join-Path $PSScriptRoot 'internal/Get-ScannerUserArguments.ps1')
$scannerIdentity = @(Get-ScannerUserArguments)

if (!$Offline) {
    # Only public vulnerability definitions are downloaded; no source or secrets are mounted.
    & docker run --rm @scannerIdentity --env HOME=/tmp --read-only --tmpfs /tmp:rw,nosuid,nodev,size=512m `
        --cap-drop ALL --security-opt no-new-privileges `
        --mount "type=bind,source=$cache,target=/cache" `
        $scanner image --cache-dir /cache --download-db-only *> (Join-Path $reports 'database-download.log')
    if ($LASTEXITCODE) {
        Get-Content -LiteralPath (Join-Path $reports 'database-download.log') -Tail 15 | Write-Output
        throw 'Container vulnerability definitions could not be downloaded.'
    }
}
elseif (!(Test-Path -LiteralPath (Join-Path $cache 'db/metadata.json'))) {
    throw 'Offline scanning requires a previously downloaded vulnerability database.'
}

$blocked = @()
foreach ($entry in $Images.GetEnumerator()) {
    if ($entry.Key -cnotmatch '^[a-z][a-z0-9-]{0,30}$' -or !$entry.Value) {
        throw 'Use simple image report names and explicit release references.'
    }
    $archive = Join-Path $reports ($entry.Key + '.tar')
    $reportPath = Join-Path $reports ($entry.Key + '.json')
    $logPath = Join-Path $reports ($entry.Key + '.log')
    & docker save --output $archive $entry.Value
    if ($LASTEXITCODE) { throw "Could not export release image $($entry.Key)." }

    # Scan a single image archive offline. Never expose Docker's socket, the
    # repository, private configuration or the surrounding artifact directory.
    & docker run --rm @scannerIdentity --env HOME=/tmp --network none --read-only --tmpfs /tmp:rw,nosuid,nodev,size=1024m `
        --cap-drop ALL --security-opt no-new-privileges `
        --mount "type=bind,source=$cache,target=/cache" `
        --mount "type=bind,source=$archive,target=/image.tar,readonly" `
        --mount "type=bind,source=$reports,target=/reports" `
        $scanner image --cache-dir /cache --skip-db-update --offline-scan --scanners vuln `
        --input /image.tar --severity HIGH,CRITICAL --exit-code 1 --format json `
        --output ("/reports/" + $entry.Key + '.json') *> $logPath
    $scanExit = $LASTEXITCODE
    if (!(Test-Path -LiteralPath $reportPath) -or $scanExit -notin @(0, 1)) {
        Get-Content -LiteralPath $logPath -Tail 15 | Write-Output
        throw "Container scan failed for $($entry.Key); inspect its private log."
    }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $findings = @($report.Results | ForEach-Object { $_.Vulnerabilities } | Where-Object { $_ })
    Write-Output "$($entry.Key): $($findings.Count) high/critical findings."
    if ($scanExit -ne 0 -or $findings.Count -gt 0) {
        $blocked += $entry.Key
        $findings | Select-Object VulnerabilityID, PkgName, InstalledVersion, FixedVersion, Severity
    }
}
if ($blocked.Count) { throw ('Release blocked by image findings: ' + ($blocked -join ', ')) }
Write-Output 'Known high/critical container-image gate passed; this does not replace the threat review.'
