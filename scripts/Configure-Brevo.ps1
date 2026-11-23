[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SmtpLogin,
    [string]$SenderAddress = 'lexigolanguage@gmail.com'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $projectRoot 'backend/LanguageLearning.Api'
if ([string]::IsNullOrWhiteSpace($SmtpLogin)) { throw 'Enter the SMTP login shown in Brevo, not the relay hostname.' }
$sender = $null
if (-not [System.Net.Mail.MailAddress]::TryCreate($SenderAddress, [ref]$sender) -or $sender.Address -ne $SenderAddress) {
    throw 'Enter the exact sender email address verified in Brevo.'
}

Write-Host 'This stores local development settings only. Delivery remains disabled.'
Write-Host 'Use a Brevo SMTP key, not your account password or an API key.'
$smtpSecret = Read-Host 'Paste the SMTP key (hidden)' -AsSecureString
$secretPointer = [IntPtr]::Zero
$secretText = $null
$settingsJson = $null
try {
    $secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($smtpSecret)
    $secretText = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)
    if ([string]::IsNullOrWhiteSpace($secretText) -or $secretText -match '\s') { throw 'The SMTP key is empty or contains whitespace.' }
    $settingsJson = @{
        'Smtp:Host' = 'smtp-relay.brevo.com'
        'Smtp:Port' = '587'
        'Smtp:Username' = $SmtpLogin.Trim()
        'Smtp:Password' = $secretText
        'Smtp:SenderAddress' = $SenderAddress
        'Smtp:DeliveryEnabled' = 'false'
    } | ConvertTo-Json -Compress
    # JSON over stdin keeps the key out of process arguments and shell history.
    $settingsJson | & dotnet user-secrets set --project $apiProject
    if ($LASTEXITCODE -ne 0) { throw 'Unable to save the local SMTP settings.' }
}
finally {
    if ($secretPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer) }
    $smtpSecret.Dispose()
    $secretText = $null
    $settingsJson = $null
}
Write-Host 'Brevo settings saved in local User Secrets. No email was sent. External delivery is disabled until explicitly enabled.'
