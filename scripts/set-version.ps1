param(
    [Parameter(Mandatory)]
    [string]$Version,
    [string]$Repository = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw "Version must be SemVer without build metadata: $Version"
}

$propsPath = Join-Path $Repository "Directory.Build.props"
$props = [System.IO.File]::ReadAllText($propsPath)
$versionPattern = '<VersionPrefix>[^<]+</VersionPrefix>'
if (-not [regex]::IsMatch($props, $versionPattern)) {
    throw "VersionPrefix was not found in $propsPath"
}
$updated = [regex]::Replace(
    $props,
    $versionPattern,
    "<VersionPrefix>$Version</VersionPrefix>"
)

Push-Location (Join-Path $Repository "packages/darkws")
try {
    npm version $Version --no-git-tag-version --allow-same-version
    if ($LASTEXITCODE -ne 0) {
        throw "npm version failed with exit code $LASTEXITCODE"
    }
} finally {
    Pop-Location
}

[System.IO.File]::WriteAllText($propsPath, $updated)

& (Join-Path $PSScriptRoot "check-version.ps1") `
    -Repository $Repository `
    -ExpectedVersion $Version
