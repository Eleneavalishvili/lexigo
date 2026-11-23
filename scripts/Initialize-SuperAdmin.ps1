#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Write-Host 'Register and confirm your account, enroll an authenticator, then use this local command once.'
Write-Host 'This does not create an account or default password. Keep the API stopped while running it.'
$accountEmail = Read-Host 'Confirmed account email'
$passwordProof = Read-Host 'Current password' -AsSecureString
$authenticatorProof = Read-Host 'Fresh authenticator code' -AsSecureString
$passwordPointer = [IntPtr]::Zero
$codePointer = [IntPtr]::Zero
try {
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($passwordProof)
    $codePointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($authenticatorProof)
    $proofJson = @{
        Email = $accountEmail
        Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
        Code = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($codePointer)
    } | ConvertTo-Json -Compress
    $proofJson | & dotnet run --no-build --project (Join-Path $projectRoot 'backend/LanguageLearning.Api') --launch-profile http -- --bootstrap-super-admin
    if ($LASTEXITCODE -ne 0) { throw 'Initial administrator provisioning was refused.' }
} finally {
    if ($passwordPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer) }
    if ($codePointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($codePointer) }
    $proofJson = $null
    $passwordProof.Dispose()
    $authenticatorProof.Dispose()
}
