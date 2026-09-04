$ErrorActionPreference = "Stop"

$repository = Split-Path -Parent $PSScriptRoot
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporary = Join-Path $temporaryRoot "darkws-version-$([Guid]::NewGuid().ToString("N"))"

try {
    New-Item -ItemType Directory -Path (Join-Path $temporary "packages/darkws") -Force | Out-Null
    Copy-Item (Join-Path $repository "Directory.Build.props") $temporary
    Copy-Item (Join-Path $repository "packages/darkws/package.json") (Join-Path $temporary "packages/darkws")
    Copy-Item (Join-Path $repository "packages/darkws/package-lock.json") (Join-Path $temporary "packages/darkws")

    $version = & (Join-Path $PSScriptRoot "check-version.ps1") -Repository $temporary
    if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
        throw "Unexpected initial version: $version"
    }

    & (Join-Path $PSScriptRoot "set-version.ps1") -Repository $temporary -Version "1.2.3-rc.1" | Out-Null
    $updated = & (Join-Path $PSScriptRoot "check-version.ps1") `
        -Repository $temporary `
        -ExpectedVersion "v1.2.3-rc.1"
    if ($updated -ne "1.2.3-rc.1") { throw "Version update failed: $updated" }

    $invalidRejected = $false
    try {
        & (Join-Path $PSScriptRoot "set-version.ps1") -Repository $temporary -Version "latest"
    } catch {
        $invalidRejected = $true
    }
    if (-not $invalidRejected) { throw "Invalid version was accepted" }

    Write-Output "Version tests passed"
} finally {
    if (Test-Path -LiteralPath $temporary) {
        $resolved = [System.IO.Path]::GetFullPath($temporary)
        if (-not $resolved.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Temporary path is outside the temporary directory: $resolved"
        }
        Remove-Item -LiteralPath $temporary -Recurse
    }
}
