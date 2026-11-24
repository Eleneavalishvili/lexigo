function Get-ScannerUserArguments {
    if (!$IsLinux) { return }

    $scannerUid = (& id -u).Trim()
    $scannerGid = (& id -g).Trim()
    if ($LASTEXITCODE -ne 0 -or $scannerUid -notmatch '^\d+$' -or $scannerGid -notmatch '^\d+$') {
        throw 'Could not determine the host directory owner for the scanner.'
    }

    # With all capabilities dropped, even UID 0 cannot write a directory owned
    # by the GitHub runner. Match that owner instead of weakening permissions.
    return @('--user', "${scannerUid}:${scannerGid}")
}
