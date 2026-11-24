function Start-EdgeApiFixture {
    param(
        [string]$Image, [string]$Api, [string]$Database, [string]$Network,
        [string]$KeyVolume, [string]$ProxyAddress, [string]$WebOrigin, [switch]$SmallServer
    )

    $password = [Guid]::NewGuid().ToString('N')
    $names = @('POSTGRES_PASSWORD', 'ConnectionStrings__DefaultConnection', 'Authentication__SigningPrivateKeyPem')
    $previous = @{}
    foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
    $rsa = [System.Security.Cryptography.RSA]::Create(3072)
    try {
        $env:POSTGRES_PASSWORD = $password
        $env:ConnectionStrings__DefaultConnection = "Host=$Database;Database=postgres;Username=postgres;Password=$password;GSS Encryption Mode=Disable"
        $env:Authentication__SigningPrivateKeyPem = $rsa.ExportRSAPrivateKeyPem()
        $databaseLimits = if ($SmallServer) { @('--memory=192m', '--memory-swap=192m', '--cpus=1') } else { @() }
        $databaseSettings = if ($SmallServer) { @('-c', 'shared_buffers=32MB', '-c', 'max_connections=30') } else { @() }
        docker run --rm -d --name $Database --network $Network @databaseLimits -e POSTGRES_PASSWORD postgres:17 @databaseSettings | Out-Null
        if ($LASTEXITCODE) { throw 'Could not start the disposable PostgreSQL database.' }

        $ready = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            docker exec $Database pg_isready -h 127.0.0.1 -U postgres -q
            if (!$LASTEXITCODE) { $ready = $true; break }
            Start-Sleep -Seconds 1
        }
        if (!$ready) { throw 'Disposable PostgreSQL did not become ready.' }

        docker run --rm --network $Network --workdir /tmp --entrypoint dotnet `
            -e ASPNETCORE_ENVIRONMENT=Production -e ConnectionStrings__DefaultConnection `
            -e Authentication__SigningPrivateKeyPem -e Smtp__DeliveryEnabled=false `
            $Image /app/LanguageLearning.Api.dll --install-bundled-content *> $null
        if ($LASTEXITCODE -eq 0) { throw 'Content maintenance must refuse an unmigrated database.' }

        foreach ($operation in @('--initialize-database', '--install-starter-lessons')) {
            docker run --rm --network $Network --workdir /tmp --entrypoint dotnet `
                -e ASPNETCORE_ENVIRONMENT=Development -e ConnectionStrings__DefaultConnection `
                -e Smtp__DeliveryEnabled=false -e Logging__LogLevel__Default=Warning `
                $Image /app/LanguageLearning.Api.dll $operation
            if ($LASTEXITCODE) { throw "Database fixture operation $operation failed." }
        }

        foreach ($attempt in 1..2) {
            docker run --rm --network $Network --workdir /tmp --entrypoint dotnet `
                -e ASPNETCORE_ENVIRONMENT=Production -e ConnectionStrings__DefaultConnection `
                -e Authentication__SigningPrivateKeyPem -e Smtp__DeliveryEnabled=false `
                -e Logging__LogLevel__Default=Warning `
                $Image /app/LanguageLearning.Api.dll --install-bundled-content
            if ($LASTEXITCODE) { throw "Production content maintenance attempt $attempt failed." }
        }

        $apiLimits = if ($SmallServer) { @('--memory=384m', '--memory-swap=384m', '--cpus=1') } else { @() }
        docker run --rm -d --name $Api --network $Network --network-alias api @apiLimits `
            -e ASPNETCORE_ENVIRONMENT=Production -e ASPNETCORE_HTTPS_PORT=443 `
            -e DataProtection__KeyDirectory=/var/lib/lexigo/keys `
            --mount "type=volume,source=$KeyVolume,target=/var/lib/lexigo/keys" `
            -e "ReverseProxy__Address=$ProxyAddress" -e "AllowedHosts=$(([uri]$WebOrigin).Host)" `
            -e "AccountEmail__WebOrigin=$WebOrigin" -e Smtp__DeliveryEnabled=false `
            -e ConnectionStrings__DefaultConnection -e Authentication__SigningPrivateKeyPem $Image | Out-Null
        if ($LASTEXITCODE) { throw 'Could not start the production API container.' }
    }
    finally {
        $rsa.Dispose()
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
    }
}
