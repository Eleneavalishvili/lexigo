# Local presentation helper. Credentials remain encrypted on disk and are shown
# only after Windows DPAPI verifies the current Windows user.
param([switch]$Staff)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Security
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
public static class PresentationAuthenticator
{
    public static string CurrentCode(string secret)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var key = new List<byte>();
        int buffer = 0;
        int bits = 0;
        foreach (char letter in secret.TrimEnd('='))
        {
            buffer = (buffer << 5) | alphabet.IndexOf(letter);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                key.Add((byte)(buffer >> bits));
            }
        }
        long counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var bytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        using (var hmac = new HMACSHA1(key.ToArray()))
        {
            var hash = hmac.ComputeHash(bytes);
            int offset = hash[hash.Length - 1] & 15;
            int value = ((hash[offset] & 127) << 24) | (hash[offset + 1] << 16)
                | (hash[offset + 2] << 8) | hash[offset + 3];
            return (value % 1000000).ToString("D6");
        }
    }
}
'@
$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root 'work/deployment/presentation-accounts.dpapi'
$bytes = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($path), $null,
    [Security.Cryptography.DataProtectionScope]::CurrentUser)
try {
    $allAccounts = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
    $teachers = @($allAccounts | Where-Object { if ($Staff) { !$_.teacherAccessCode } else { $_.teacherAccessCode } } | Sort-Object displayName)
    if ((!$Staff -and $teachers.Count -ne 8) -or ($Staff -and $teachers.Count -ne 3)) {
        throw 'The protected presentation accounts have not been installed yet.'
    }
    $form = New-Object Windows.Forms.Form
    $viewerTitle = if ($Staff) { 'LexiGo private presentation staff access' } else { 'LexiGo private presentation teacher access' }
    $form.Text = $viewerTitle
    $form.Size = New-Object Drawing.Size(900, 450)
    $form.StartPosition = 'CenterScreen'
    $form.TopMost = $true
    $form.Add_Shown({ $form.Activate() })
    $grid = New-Object Windows.Forms.DataGridView
    $grid.Dock = 'Fill'
    $grid.ReadOnly = $true
    $grid.SelectionMode = 'FullRowSelect'
    $grid.MultiSelect = $false
    $grid.AllowUserToAddRows = $false
    $grid.RowHeadersVisible = $false
    $grid.AutoSizeColumnsMode = 'Fill'
    [void]$grid.Columns.Add('teacher', $(if ($Staff) { 'Staff account' } else { 'Presentation teacher' }))
    if ($Staff) { [void]$grid.Columns.Add('email', 'Sign-in email') }
    [void]$grid.Columns.Add('access', $(if ($Staff) { 'Private password' } else { 'Private teacher access code' }))
    [void]$grid.Columns.Add('authenticator', 'Current authenticator code')
    foreach ($teacher in $teachers) {
        if ($Staff) { [void]$grid.Rows.Add($teacher.displayName, $teacher.email, $teacher.password, '') }
        else { [void]$grid.Rows.Add($teacher.displayName, $teacher.teacherAccessCode, '') }
    }
    $actions = New-Object Windows.Forms.FlowLayoutPanel
    $actions.Dock = 'Bottom'
    $actions.Height = 45
    $copyAccess = New-Object Windows.Forms.Button
    $copyAccess.Text = $(if ($Staff) { 'Copy selected password' } else { 'Copy selected teacher access code' })
    $copyAccess.Width = 260
    $copyAccess.Height = 35
    $copyAccess.Add_Click({
        if ($grid.CurrentRow) {
            $selected = $teachers[$grid.CurrentRow.Index]
            [Windows.Forms.Clipboard]::SetText($(if ($Staff) { $selected.password } else { $selected.teacherAccessCode }))
        }
    })
    $copyAuthenticator = New-Object Windows.Forms.Button
    $copyAuthenticator.Text = 'Copy selected current authenticator'
    $copyAuthenticator.Width = 280
    $copyAuthenticator.Height = 35
    $copyAuthenticator.Add_Click({
        if ($grid.CurrentRow) {
            $selected = $teachers[$grid.CurrentRow.Index]
            [Windows.Forms.Clipboard]::SetText([PresentationAuthenticator]::CurrentCode($selected.authenticatorKey))
        }
    })
    $actions.Controls.Add($copyAccess)
    $actions.Controls.Add($copyAuthenticator)
    $instructions = New-Object Windows.Forms.Label
    $instructions.Dock = 'Top'
    $instructions.Height = 42
    $instructions.Text = 'Select one row. Copy its access code, then its current authenticator. Paste each into the matching website field. Never reuse an authenticator after successful sign-in.'
    $button = New-Object Windows.Forms.Button
    $button.Text = $(if ($Staff) { 'Open LexiGo staff sign-in' } else { 'Open LexiGo teacher sign-in' })
    $button.Dock = 'Bottom'
    $button.Height = 45
    $button.Add_Click({ Start-Process $(if ($Staff) { 'https://206.189.25.5/login' } else { 'https://206.189.25.5/teacher-login' }) })
    $form.Controls.Add($grid)
    $form.Controls.Add($actions)
    $form.Controls.Add($button)
    $form.Controls.Add($instructions)
    if (!$Staff) {
        for ($index = 0; $index -lt $teachers.Count; $index++) {
            if ($teachers[$index].displayName -eq 'Presentation tutor Sofia') {
                $grid.CurrentCell = $grid.Rows[$index].Cells[0]
                $grid.Rows[$index].Selected = $true
            }
        }
    }
    $refresh = {
        for ($index = 0; $index -lt $teachers.Count; $index++) {
            $column = if ($Staff) { 3 } else { 2 }
            $grid.Rows[$index].Cells[$column].Value = [PresentationAuthenticator]::CurrentCode($teachers[$index].authenticatorKey)
        }
        $remaining = 30 - ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() % 30)
        $form.Text = "$viewerTitle - authenticator refresh in $remaining seconds"
    }
    $timer = New-Object Windows.Forms.Timer
    $timer.Interval = 1000
    $timer.Add_Tick($refresh)
    & $refresh
    $timer.Start()
    try { [void]$form.ShowDialog() }
    finally { $timer.Stop(); $timer.Dispose(); $form.Dispose() }
} finally { [Array]::Clear($bytes, 0, $bytes.Length) }
