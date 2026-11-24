[CmdletBinding()]
param([string]$ApiImage, [switch]$SmallServer)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root 'frontend/dist/lexigo/browser'
$template = Join-Path $root 'config/nginx/lexigo.conf.template'
if (!(Test-Path (Join-Path $frontend 'index.html'))) { throw 'Build the Angular frontend first.' }

$image = 'lexigo-nginx:patched'
$runId = [Guid]::NewGuid().ToString('N')
$network = "lexigo-edge-$runId"
$edge = "lexigo-edge-web-$runId"
$api = "lexigo-edge-fixture-$runId"
$database = "lexigo-edge-db-$runId"
$keys = "lexigo-edge-keys-$runId"
$run = New-Item -ItemType Directory -Path (Join-Path $root "work/$network") -Force
$hostname = 'lexigo.example.invalid'
$certFile = Join-Path $run 'fullchain.pem'
$keyFile = Join-Path $run 'privkey.pem'
$fixture = Join-Path $run 'api.conf'
$curl = (Get-Command curl -CommandType Application | Select-Object -First 1).Source

try {
    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            "CN=$hostname", $rsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $names = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
        $names.AddDnsName($hostname)
        $request.CertificateExtensions.Add($names.Build())
        $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddHours(1))
        try {
            $certificate.ExportCertificatePem() | Set-Content -LiteralPath $certFile
            $rsa.ExportPkcs8PrivateKeyPem() | Set-Content -LiteralPath $keyFile
        }
        finally { $certificate.Dispose() }
    }
    finally { $rsa.Dispose() }

    @'
server {
    listen 8080;
    location = /api/v1/edge-check {
        default_type text/plain;
        return 200 "$http_x_forwarded_proto|$http_host|$http_x_forwarded_for|$http_forwarded";
    }
    location / { return 404; }
}
'@ | Set-Content -LiteralPath $fixture

    $networkOptions = @()
    if ($ApiImage) {
        $subnet = "172.29.$(Get-Random -Minimum 32 -Maximum 224)"
        $networkOptions = @('--subnet', "$subnet.0/24")
        $proxyAddress = "$subnet.10"
    }
    docker network create @networkOptions $network | Out-Null
    if ($LASTEXITCODE) { throw 'Could not create the isolated edge network.' }
    if ($ApiImage) {
        . (Join-Path $PSScriptRoot 'internal/Start-EdgeApiFixture.ps1')
        Start-EdgeApiFixture -Image $ApiImage -Api $api -Database $database -Network $network `
            -KeyVolume $keys -ProxyAddress $proxyAddress -WebOrigin "https://$hostname" -SmallServer:$SmallServer
    }
    else {
        docker run --rm -d --name $api --network $network --network-alias api `
            --mount "type=bind,source=$fixture,target=/etc/nginx/conf.d/default.conf,readonly" $image | Out-Null
    }
    if ($LASTEXITCODE) { throw 'Could not start the disposable upstream.' }
    $edgeAddress = if ($ApiImage) { @('--ip', $proxyAddress) } else { @() }
    $edgeLimits = if ($SmallServer) { @('--memory=64m', '--memory-swap=64m', '--cpus=1') } else { @() }
    docker run --rm -d --name $edge --network $network @edgeAddress @edgeLimits `
        -p 127.0.0.1::80 -p 127.0.0.1::443 -e "LEXIGO_HOST=$hostname" `
        -e 'NGINX_ENVSUBST_FILTER=^LEXIGO_HOST$' `
        --mount "type=bind,source=$template,target=/etc/nginx/templates/default.conf.template,readonly" `
        --mount "type=bind,source=$frontend,target=/usr/share/nginx/html,readonly" `
        --mount "type=bind,source=$run,target=/etc/lexigo/tls,readonly" $image | Out-Null
    if ($LASTEXITCODE) { throw 'Could not start the HTTPS edge.' }

    $httpsPort = (docker port $edge 443).Split(':')[-1]
    $httpPort = (docker port $edge 80).Split(':')[-1]
    $nullDevice = if ($IsWindows) { 'NUL' } else { '/dev/null' }
    $https = "https://${hostname}:${httpsPort}"
    $connection = @('--noproxy', $hostname, '--resolve', "${hostname}:${httpsPort}:127.0.0.1", '--cacert', $certFile,
        '--silent', '--show-error', '--max-time', '5')
    $page = $null
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        $page = & $curl @connection "$https/learn/next" 2>$null
        if (!$LASTEXITCODE -and ($page -join '') -match '<app-root') { break }
        Start-Sleep -Seconds 1
    }
    if (($page -join '') -notmatch '<app-root') { throw 'TLS deep link did not serve the Angular app.' }

    $redirect = & $curl --noproxy $hostname --resolve "${hostname}:${httpPort}:127.0.0.1" --silent -I "http://${hostname}:${httpPort}/login"
    if (($redirect -join "`n") -notmatch "Location: https://$([regex]::Escape($hostname))/login") {
        throw 'HTTP did not redirect to the configured HTTPS origin.'
    }
    $headers = & $curl @connection -I "$https/login"
    if (($headers -join "`n") -notmatch 'Strict-Transport-Security: max-age=31536000') { throw 'Missing HSTS.' }

    if ($ApiImage) {
        . (Join-Path $PSScriptRoot 'internal/Test-EdgeApiSession.ps1')
        Test-EdgeApiSession -Curl $curl -Connection $connection -Origin $https -Api $api -Database $database -Directory $run.FullName
        $health = docker exec $edge wget -q -O - --header "Host: $hostname" `
            --header 'X-Forwarded-Proto: https' --header 'X-Forwarded-For: 127.0.0.1' http://api:8080/health/ready
        if ($LASTEXITCODE -or $health -ne 'Healthy') { throw 'The internal API readiness check failed.' }
    }
    else {
        $proxied = & $curl @connection -H 'X-Forwarded-For: synthetic-spoof' -H 'X-Forwarded-Proto: http' `
            -H 'Forwarded: for=synthetic-spoof' "$https/api/v1/edge-check?token=synthetic-query"
        if ($LASTEXITCODE) { throw 'The proxy-header rehearsal request failed.' }
        if ($proxied -notmatch "^https\|$([regex]::Escape($hostname))\|[0-9a-f.:]+\|$") {
            throw 'The upstream did not receive the expected proxy headers.'
        }
    }
    $missingPaths = @('/missing.js', '/.env', '/health/ready', '/swagger')
    if (!$ApiImage) { $missingPaths += '/api/v1/unknown' }
    foreach ($path in $missingPaths) {
        $status = & $curl @connection -o $nullDevice -w '%{http_code}' "$https$path"
        if ($status -ne '404') { throw "Expected HTTP 404 for $path." }
    }
    $oversize = & $curl @connection -o $nullDevice -w '%{http_code}' -X POST `
        -H 'Content-Length: 12000000' -H 'Expect: 100-continue' "$https/api/v1/edge-check"
    if ($oversize -ne '413') { throw 'The edge request-size limit did not return HTTP 413.' }
    $logs = docker logs $edge 2>&1 | Out-String
    if ($logs -notmatch '"status":200' -or $logs -match 'synthetic-query|synthetic-spoof') {
        throw 'The expected redacted access log was not produced.'
    }
    Write-Output 'Nginx HTTPS, SPA deep links, API forwarding, 404s, HSTS and redacted access logs verified.'
}
catch {
    docker logs $edge 2>&1 | Out-File (Join-Path $run 'edge.log')
    if ($ApiImage) { docker logs $api 2>&1 | Out-File (Join-Path $run 'api.log') }
    throw
}
finally {
    docker stop $edge $api 2>$null | Out-Null
    if ($ApiImage) {
        docker stop $database 2>$null | Out-Null
        docker volume rm $keys 2>$null | Out-Null
    }
    docker network rm $network 2>$null | Out-Null
    if (Test-Path -LiteralPath $keyFile) { Remove-Item -LiteralPath $keyFile }
}
