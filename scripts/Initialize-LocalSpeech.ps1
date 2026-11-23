param([switch] $EnableForDevelopment)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifestPath = Join-Path $repositoryRoot 'config/speech-models.json'
$models = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$modelRoot = Join-Path $repositoryRoot 'work/speech-models'
$downloadRoot = Join-Path $repositoryRoot 'work/speech-downloads'

function Assert-WorkspacePath([string] $Path) {
    $absolute = [IO.Path]::GetFullPath($Path)
    if (!$absolute.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Speech setup path leaves the workspace.'
    }
    $current = $absolute
    while ($current.Length -gt $repositoryRoot.Length) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Speech setup does not follow workspace links.'
            }
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

foreach ($path in @($modelRoot, $downloadRoot)) {
    Assert-WorkspacePath $path
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

$handler = [Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [Net.Http.HttpClient]::new($handler)
try {
    foreach ($model in $models) {
        $uri = [Uri]$model.url
        if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'alphacephei.com' -or $uri.AbsolutePath -ne "/vosk/models/$($model.name).zip" -or $model.name -notmatch '^vosk-model-small-(es|it|de|ru)-[0-9.]+$') {
            throw 'Unexpected model source.'
        }
        $archivePath = Join-Path $downloadRoot ($model.name + '.zip')
        Assert-WorkspacePath $archivePath
        if (!(Test-Path -LiteralPath $archivePath)) {
            $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $uri)
            $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
            try {
                $response.EnsureSuccessStatusCode() | Out-Null
                $inputStream = $response.Content.ReadAsStream()
                $temporaryDownload = $archivePath + '.partial-' + [Guid]::NewGuid().ToString('N')
                Assert-WorkspacePath $temporaryDownload
                $outputStream = [IO.File]::Open($temporaryDownload, [IO.FileMode]::CreateNew)
                try {
                    $buffer = [byte[]]::new(81920)
                    $total = 0
                    while (($read = $inputStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        $total += $read
                        if ($total -gt 80MB) { throw 'Model download exceeded 80 MiB.' }
                        $outputStream.Write($buffer, 0, $read)
                    }
                } finally {
                    $inputStream.Dispose()
                    $outputStream.Dispose()
                }
                if ((Get-FileHash -LiteralPath $temporaryDownload -Algorithm SHA256).Hash -ne $model.sha256) {
                    throw 'Downloaded model integrity check failed.'
                }
                Assert-WorkspacePath $archivePath
                Move-Item -LiteralPath $temporaryDownload -Destination $archivePath
            } finally { $response.Dispose(); $request.Dispose() }
        }
        if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $model.sha256) { throw 'Model archive integrity check failed.' }
        $destination = Join-Path $modelRoot $model.name
        Assert-WorkspacePath $destination
        if (Test-Path -LiteralPath $destination) {
            $receipt = Join-Path $destination '.lexigo-archive-sha256'
            if (!(Test-Path -LiteralPath $receipt) -or (Get-Content -LiteralPath $receipt -Raw).Trim() -ne $model.sha256) {
                throw 'Existing model has no matching setup receipt; it was not overwritten.'
            }
            Write-Output "$($model.language): existing model preserved."
            continue
        }
        $staging = Join-Path $modelRoot ('staging-' + [Guid]::NewGuid().ToString('N'))
        Assert-WorkspacePath $staging
        New-Item -ItemType Directory -Path $staging | Out-Null
        $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
        try {
            $expandedBytes = 0L
            foreach ($entry in $archive.Entries) {
                $parts = $entry.FullName.Split('/')
                if ($parts[0] -ne $model.name -or $parts -contains '..' -or $entry.FullName.Contains('\') -or $entry.FullName.Contains(':') -or (($entry.ExternalAttributes -shr 16) -band 61440) -eq 40960) {
                    throw 'Unsafe model archive entry.'
                }
                $expandedBytes += $entry.Length
                if ($expandedBytes -gt 300MB -or $archive.Entries.Count -gt 10000) { throw 'Model extraction exceeds bounds.' }
            }
        } finally { $archive.Dispose() }
        [IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $staging)
        $source = Join-Path $staging $model.name
        Assert-WorkspacePath $source
        Assert-WorkspacePath $destination
        Move-Item -LiteralPath $source -Destination $destination
        [IO.File]::WriteAllText((Join-Path $destination '.lexigo-archive-sha256'), $model.sha256)
        Write-Output "$($model.language): verified local model installed."
    }
} finally { $client.Dispose() }

Write-Output 'Local models are ready. No cloud account, subscription or microphone access was used.'

if ($EnableForDevelopment) {
    $apiProject = Join-Path $repositoryRoot 'backend/LanguageLearning.Api'
    & dotnet user-secrets set 'LocalSpeech:ModelsDirectory' $modelRoot --project $apiProject
    if ($LASTEXITCODE -ne 0) { throw 'Could not configure the local model directory.' }
    & dotnet user-secrets set 'LocalSpeech:Enabled' 'true' --project $apiProject
    if ($LASTEXITCODE -ne 0) { throw 'Could not enable local voice checking.' }
    Write-Output 'Local development voice checking enabled. Restart the API to load these settings.'
}
