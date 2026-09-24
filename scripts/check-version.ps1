param(
    [string]$ExpectedVersion,
    [string]$Repository = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

[xml]$props = Get-Content -Raw (Join-Path $Repository "Directory.Build.props")
$dotnetVersion = $props.SelectSingleNode("/Project/PropertyGroup/VersionPrefix").InnerText
$npmPackagePath = Join-Path $Repository "packages/darkws/package.json"
$npmVersion = (Get-Content -Raw $npmPackagePath | ConvertFrom-Json).version

if ($dotnetVersion -ne $npmVersion) {
    throw "Version mismatch: .NET=$dotnetVersion, npm=$npmVersion"
}

# package.json edited without npm leaves both lockfile versions behind.
$lock = Get-Content -Raw (Join-Path $Repository "packages/darkws/package-lock.json") | ConvertFrom-Json -AsHashtable
foreach ($lockVersion in $lock["version"], $lock["packages"][""]["version"]) {
    if ($lockVersion -ne $npmVersion) {
        throw "Version mismatch: package.json=$npmVersion, package-lock.json=$lockVersion; update it with npm version"
    }
}

if ($ExpectedVersion -and $dotnetVersion -ne $ExpectedVersion.TrimStart("v")) {
    throw "Version $dotnetVersion does not match expected $ExpectedVersion"
}

Write-Output $dotnetVersion
