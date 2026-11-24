[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskConfiguration = Get-Content -LiteralPath (Join-Path $taskRoot 'config/security-scanners.json') -Raw | ConvertFrom-Json
$taskImage = $taskConfiguration.gitleaksImage
$taskReports = Join-Path $taskRoot 'work/security'
New-Item -ItemType Directory -Path $taskReports -Force | Out-Null
$scannerIdentity = @()
if ($IsLinux) {
    $scannerUid = (& id -u).Trim()
    $scannerGid = (& id -g).Trim()
    if ($LASTEXITCODE -ne 0 -or $scannerUid -notmatch '^\d+$' -or $scannerGid -notmatch '^\d+$') {
        throw 'Could not determine the report directory owner for the secret scanner.'
    }
    $scannerIdentity = @('--user', "${scannerUid}:${scannerGid}")
}

& docker image inspect $taskImage --format '{{.Id}}' *> $null
if ($LASTEXITCODE -ne 0) {
    & docker pull $taskImage
    if ($LASTEXITCODE -ne 0) { throw 'The pinned free scanner is unavailable; the scan has not passed.' }
}

# Scan source offline, with no container privileges, read-only source and fully redacted reports.
# Ignored runtime data is excluded; the deliverable .env.example remains in scope.
& docker run --rm @scannerIdentity --network none --read-only --cap-drop ALL --security-opt no-new-privileges `
    --mount "type=bind,source=$taskRoot,target=/src,readonly" `
    --mount "type=bind,source=$taskReports,target=/reports" `
    $taskImage dir /src --config /src/.gitleaks.toml --redact=100 --report-format json --report-path /reports/gitleaks.json
if ($LASTEXITCODE -ne 0) { throw 'Secret gate failed or the scanner was unavailable. Review the redacted report before release.' }
Write-Output 'Source secret gate passed. This is not a complete security audit or a scan of private runtime storage.'
