$ErrorActionPreference = "Stop"

$repository = Split-Path -Parent $PSScriptRoot
$coveragePath = Join-Path ([System.IO.Path]::GetTempPath()) "darkws-coverage.xml"

Push-Location $repository
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet restore DarkWS.sln
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build DarkWS.sln --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet tool run dotnet-coverage collect `
        "dotnet test DarkWS.sln --no-build" `
        -f cobertura `
        -o $coveragePath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    [xml]$coverage = Get-Content -Raw $coveragePath
    foreach ($packageName in "DarkWS", "DarkWS.Redis") {
        $package = $coverage.coverage.packages.package |
            Where-Object name -EQ $packageName |
            Select-Object -First 1
        if ($null -eq $package) {
            throw "Coverage package '$packageName' was not found"
        }

        $linePercent = [Math]::Round([double]$package."line-rate" * 100, 2)
        $branchPercent = [Math]::Round([double]$package."branch-rate" * 100, 2)
        Write-Host "$packageName coverage: $linePercent% lines, $branchPercent% branches"
        if ($linePercent -lt 90 -or $branchPercent -lt 80) {
            throw "$packageName coverage is below 90% lines / 80% branches"
        }
    }

    npm --prefix packages/darkws run test:coverage
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    npm --prefix packages/darkws run build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & "$PSScriptRoot/test-packages.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Pop-Location
}
