function Test-EdgeApiSession {
    param([string]$Curl, [string[]]$Connection, [string]$Origin, [string]$Api, [string]$Database, [string]$Directory)

    $cookieJar = Join-Path $Directory 'session.cookies'
    $headers = Join-Path $Directory 'session.headers'
    $email = "edge-$([Guid]::NewGuid().ToString('N'))@example.test"
    $password = "Test!$([Guid]::NewGuid().ToString('N'))"
    $auth = "$Origin/api/v1/auth"
    try {
        $catalog = $null
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            $catalog = & $Curl @Connection --fail "$Origin/api/v1/courses?probe=synthetic-query" 2>$null
            if (!$LASTEXITCODE) { break }
            Start-Sleep -Seconds 1
        }
        if ($LASTEXITCODE -or @(($catalog -join '') | ConvertFrom-Json).Count -ne 4) {
            throw 'The real API did not serve four courses through HTTPS.'
        }

        $registration = @{ email = $email; password = $password; displayName = 'HTTPS Rehearsal' } | ConvertTo-Json -Compress
        $registration | & $Curl @Connection --fail -H 'Content-Type: application/json' --data-binary '@-' "$auth/register" | Out-Null
        if ($LASTEXITCODE) { throw 'HTTPS registration failed.' }

        # SMTP is disabled. Read only this synthetic account's confirmation from its disposable outbox.
        $sql = 'SELECT "UserId", "HtmlBody" FROM "EmailOutboxMessages" WHERE "Recipient" = ' + "'$email';"
        $outbox = $sql | docker exec -i $Database psql -XAtq -v ON_ERROR_STOP=1 -U postgres
        if ($LASTEXITCODE -or $outbox -notmatch '^([0-9a-f-]{36})\|.*token=([A-Za-z0-9_-]+)') {
            throw 'The disposable outbox did not contain a confirmation.'
        }
        $confirmation = @{ userId = $Matches[1]; token = $Matches[2] } | ConvertTo-Json -Compress
        $confirmation | & $Curl @Connection --fail -H 'Content-Type: application/json' --data-binary '@-' "$auth/verify-email" | Out-Null
        if ($LASTEXITCODE) { throw 'HTTPS email confirmation failed.' }

        $csrf = (& $Curl @Connection --fail -c $cookieJar "$auth/csrf" | ConvertFrom-Json).requestToken
        if ($LASTEXITCODE -or !$csrf) { throw 'HTTPS anti-forgery initialization failed.' }
        $credentials = @{ email = $email; password = $password } | ConvertTo-Json -Compress
        $session = $credentials | & $Curl @Connection --fail -b $cookieJar -c $cookieJar -D $headers `
            -H "X-XSRF-TOKEN: $csrf" -H 'Content-Type: application/json' --data-binary '@-' "$auth/login"
        if ($LASTEXITCODE) { throw 'HTTPS sign-in failed.' }
        $token = ($session | ConvertFrom-Json).accessToken
        $refresh = Get-Content -LiteralPath $headers | Where-Object { $_ -match '^Set-Cookie: __Host-lexigo\.refresh=' }
        if (!$token -or $refresh -notmatch ';\s*secure(?:;|$)' -or $refresh -notmatch ';\s*httponly(?:;|$)' -or
            $refresh -notmatch ';\s*samesite=strict(?:;|$)' -or $refresh -notmatch ';\s*path=/(?:;|$)' -or
            $refresh -match ';\s*domain=') {
            throw 'The production refresh cookie did not have the required attributes.'
        }
        $account = & $Curl @Connection --fail -H "Authorization: Bearer $token" "$Origin/api/v1/me"
        if ($LASTEXITCODE -or ($account | ConvertFrom-Json).email -ne $email) {
            throw 'The signed-in account was not accessible through HTTPS.'
        }
        docker restart $Api | Out-Null
        if ($LASTEXITCODE) { throw 'The disposable API did not restart.' }
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            & $Curl @Connection --fail "$Origin/api/v1/courses" 2>$null | Out-Null
            if (!$LASTEXITCODE) { break }
            Start-Sleep -Seconds 1
        }
        if ($LASTEXITCODE) { throw 'The disposable API did not recover after restart.' }
        $refreshed = & $Curl @Connection --fail -b $cookieJar -c $cookieJar -H "X-XSRF-TOKEN: $csrf" -X POST "$auth/refresh"
        if ($LASTEXITCODE -or !($refreshed | ConvertFrom-Json).accessToken) { throw 'HTTPS session refresh after restart failed.' }
        $newToken = ($refreshed | ConvertFrom-Json).accessToken
        Test-EdgeLesson -Curl $Curl -Connection $Connection -Origin $Origin -Token $newToken
        $discard = if ($IsWindows) { 'NUL' } else { '/dev/null' }
        $premiumStatus = & $Curl @Connection -o $discard -w '%{http_code}' `
            -H "Authorization: Bearer $newToken" "$Origin/api/v1/me/weekly-summary/comparison"
        if ($premiumStatus -ne '403') { throw 'The HTTPS API did not enforce Premium comparison access.' }
        Write-Output 'Real API catalog, registration, confirmation, sign-in, secure cookie, session refresh and account verified.'
    }
    finally {
        foreach ($file in @($cookieJar, $headers)) {
            if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
        }
    }
}

function Test-EdgeLesson {
    param([string]$Curl, [string[]]$Connection, [string]$Origin, [string]$Token)

    $api = "$Origin/api/v1"
    $auth = @('-H', "Authorization: Bearer $Token")
    $courseId = '10000000-0000-0000-0000-000000000001'
    $enrollment = @{ courseId = $courseId; startingLevel = 'A1' } | ConvertTo-Json -Compress
    $enrollment | & $Curl @Connection @auth --fail -H 'Content-Type: application/json' --data-binary '@-' "$api/me/enrollments" | Out-Null
    if ($LASTEXITCODE) { throw 'HTTPS course enrollment failed.' }
    $progress = & $Curl @Connection @auth --fail "$api/me/progress?courseId=$courseId" | ConvertFrom-Json
    if ($LASTEXITCODE -or !$progress.isEnrolled) { throw 'HTTPS course progress could not be loaded.' }
    $lesson = $progress.lessons | Where-Object isUnlocked | Select-Object -First 1
    if (!$lesson) { throw 'No beginner lesson was available.' }
    $session = & $Curl @Connection @auth --fail -X POST "$api/lessons/$($lesson.id)/sessions" | ConvertFrom-Json
    if ($LASTEXITCODE -or !$session.currentExercise) { throw 'HTTPS lesson start failed.' }
    $choice = $session.currentExercise.options | Where-Object text -eq 'café'
    if (!$choice) { throw 'The Spanish starter exercise changed.' }
    $answer = @{
        sessionExerciseId = $session.currentExercise.id
        selectedOptionIds = @($choice.id)
        idempotencyKey = [Guid]::NewGuid()
    } | ConvertTo-Json -Compress
    $sessionUrl = "$api/me/learning-sessions/$($session.id)"
    $feedback = $answer | & $Curl @Connection @auth --fail -H 'Content-Type: application/json' --data-binary '@-' "$sessionUrl/answers" | ConvertFrom-Json
    if ($LASTEXITCODE -or !$feedback.isCorrect) { throw 'The HTTPS lesson answer did not grade correctly.' }
    $saved = & $Curl @Connection @auth --fail $sessionUrl | ConvertFrom-Json
    if ($LASTEXITCODE -or $saved.answeredCount -ne 1 -or $saved.correctCount -ne 1) {
        throw 'The HTTPS lesson answer did not survive reload.'
    }
    Write-Output 'Real API Spanish enrollment, lesson answer and saved progress verified through HTTPS.'
}
