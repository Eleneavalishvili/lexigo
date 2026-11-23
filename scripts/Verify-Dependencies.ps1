[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskReports = Join-Path $taskRoot 'work/security'
New-Item -ItemType Directory -Path $taskReports -Force | Out-Null
$taskDotnetReport = Join-Path $taskReports 'nuget-audit.json'
$taskNpmReport = Join-Path $taskReports 'npm-audit.json'

Push-Location $taskRoot
try {
    & dotnet list backend/LexiGo.slnx package --vulnerable --include-transitive --no-restore --format json > $taskDotnetReport
    if ($LASTEXITCODE -ne 0) { throw 'NuGet audit was unavailable. Read work/security/nuget-audit.json; do not treat this as a clean scan.' }
    $taskNuget = Get-Content -LiteralPath $taskDotnetReport -Raw | ConvertFrom-Json
    if ($taskNuget.problems) { throw 'NuGet reported incomplete audit data; review the saved report.' }
    $taskFindings = @(
        foreach ($taskProject in $taskNuget.projects) {
            foreach ($taskFramework in $taskProject.frameworks) {
                foreach ($taskPackage in @($taskFramework.topLevelPackages) + @($taskFramework.transitivePackages)) {
                    foreach ($taskFinding in $taskPackage.vulnerabilities) {
                        [pscustomobject]@{ Package = $taskPackage.id; Severity = $taskFinding.severity }
                    }
                }
            }
        }
    )

    Push-Location (Join-Path $taskRoot 'frontend')
    try {
        $taskNpm = if ($IsWindows) { (Get-Command npm.cmd).Source } else { (Get-Command npm).Source }
        & $taskNpm audit --json > $taskNpmReport
        $taskNpmExit = $LASTEXITCODE
    }
    finally { Pop-Location }
    $taskAudit = Get-Content -LiteralPath $taskNpmReport -Raw | ConvertFrom-Json
    if ($taskNpmExit -gt 1 -or $taskAudit.error -or -not $taskAudit.metadata.vulnerabilities) {
        throw 'Npm audit was unavailable. Read the saved report; do not treat missing data as zero findings.'
    }
    $taskCounts = $taskAudit.metadata.vulnerabilities
    $taskBlocked = @($taskFindings | Where-Object { $_.Severity -in @('High', 'Critical') })
    Write-Output ('NuGet known findings: ' + $taskFindings.Count)
    Write-Output ('Npm known findings: ' + $taskCounts.total)
    if ($taskBlocked.Count -gt 0 -or $taskCounts.high -gt 0 -or $taskCounts.critical -gt 0) {
        throw 'Release blocked: high/critical dependency findings require remediation and retest.'
    }
    Write-Output 'Dependency gate passed. This does not establish application security or satisfy the other release gates.'
}
finally { Pop-Location }
exit 0
