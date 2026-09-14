param([string]$OutputPath = $env:DARKWS_PACKAGE_OUTPUT)

$ErrorActionPreference = "Stop"
$repository = Split-Path -Parent $PSScriptRoot
$packageOutput = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Join-Path ([System.IO.Path]::GetTempPath()) ("darkws-packages-" + [guid]::NewGuid().ToString("N"))
} else { [System.IO.Path]::GetFullPath($OutputPath) }
New-Item -ItemType Directory -Path $packageOutput -Force | Out-Null
[xml]$buildProperties = Get-Content -Raw (Join-Path $repository "Directory.Build.props")
$packageVersion = $buildProperties.SelectSingleNode("/Project/PropertyGroup/VersionPrefix").InnerText
$frameworks = $buildProperties.SelectSingleNode("/Project/PropertyGroup/TargetFrameworks").InnerText.Split(';')
Push-Location $repository
try {
    foreach ($package in "DarkWS", "DarkWS.Redis") {
        dotnet pack "$package/$package.csproj" -c Release --no-restore -o $packageOutput
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $archivePath = Get-Item -LiteralPath (Join-Path $packageOutput "$package.$packageVersion.nupkg")
        $symbolsPath = [System.IO.Path]::ChangeExtension($archivePath.FullName, ".snupkg")
        $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath.FullName)
        $symbols = [System.IO.Compression.ZipFile]::OpenRead($symbolsPath)
        try {
            foreach ($framework in $frameworks) {
                $documentation = $archive.GetEntry("lib/$framework/$package.xml")
                $pdb = $symbols.GetEntry("lib/$framework/$package.pdb")
                if ($null -eq $documentation -or $documentation.Length -eq 0) { throw "Missing XML documentation for $package/$framework" }
                if ($null -eq $pdb -or $pdb.Length -eq 0) { throw "Missing portable symbols for $package/$framework" }
                $xmlReader = [System.IO.StreamReader]::new($documentation.Open())
                try { [xml]$api = $xmlReader.ReadToEnd() } finally { $xmlReader.Dispose() }
                if (@($api.doc.members.member).Count -eq 0) { throw "Empty API documentation for $package/$framework" }

                $pdbBuffer = [System.IO.MemoryStream]::new()
                $pdbEntry = $pdb.Open()
                try { $pdbEntry.CopyTo($pdbBuffer) } finally { $pdbEntry.Dispose() }
                $pdbBuffer.Position = 0
                $metadataProvider = [System.Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream(
                    $pdbBuffer, [System.Reflection.Metadata.MetadataStreamOptions]::Default, 0)
                try {
                    $metadata = $metadataProvider.GetMetadataReader([System.Reflection.Metadata.MetadataReaderOptions]::Default, $null)
                    $embeddedSources = 0
                    foreach ($handle in $metadata.CustomDebugInformation) {
                        $debugInfo = $metadata.GetCustomDebugInformation($handle)
                        if ($metadata.GetGuid($debugInfo.Kind) -eq [guid]'0e8a571b-6926-466e-b4ad-8ab04611f5fe') { $embeddedSources++ }
                    }
                    if ($embeddedSources -eq 0) { throw "No embedded sources in $package/$framework symbols" }
                } finally { $metadataProvider.Dispose(); $pdbBuffer.Dispose() }
            }
        } finally {
            $symbols.Dispose()
            $archive.Dispose()
        }
    }
    Write-Host "Package XML and symbols verified: $packageOutput"

    $smokePath = Join-Path ([System.IO.Path]::GetTempPath()) ("darkws-consumer-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $smokePath | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "package-smoke/PackageSmoke.csproj"), (Join-Path $PSScriptRoot "package-smoke/Program.cs") -Destination $smokePath
    $localFeed = [System.Security.SecurityElement]::Escape($packageOutput)
    @"
<configuration>
  <packageSources><clear/><add key="local" value="$localFeed"/><add key="nuget" value="https://api.nuget.org/v3/index.json"/></packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="DarkWS"/><package pattern="DarkWS.Redis"/></packageSource>
    <packageSource key="nuget"><package pattern="*"/></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $smokePath "NuGet.Config") -Encoding utf8
    $smokeProject = Join-Path $smokePath "PackageSmoke.csproj"
    dotnet restore $smokeProject --configfile (Join-Path $smokePath "NuGet.Config") --packages (Join-Path $smokePath "packages") "-p:DarkWsSmokeVersion=$packageVersion"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    foreach ($framework in $frameworks) {
        dotnet run --project $smokeProject -c Release -f $framework --no-restore "-p:DarkWsSmokeVersion=$packageVersion"
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    # Pack a fresh copy without dist to prove prepack builds the published files.
    $npmSmokePath = Join-Path $smokePath "npm"
    New-Item -ItemType Directory -Path $npmSmokePath | Out-Null
    foreach ($item in "src", "package.json", "tsconfig.json", "README.md", "LICENSE") {
        Copy-Item -LiteralPath (Join-Path $repository "packages/darkws/$item") -Destination $npmSmokePath -Recurse
    }
    $previousPath = $env:PATH
    $env:PATH = (Join-Path $repository "packages/darkws/node_modules/.bin") + [System.IO.Path]::PathSeparator + $previousPath
    Push-Location $npmSmokePath
    try {
        npm pack --pack-destination $packageOutput
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $tarball = Join-Path $packageOutput "darkws-$packageVersion.tgz"
        $files = tar -tf $tarball
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        foreach ($required in "package/dist/index.js", "package/dist/index.d.ts", "package/dist/dark-ws.js", "package/dist/dark-ws.d.ts") {
            if ($files -notcontains $required) { throw "npm tarball is missing $required" }
        }
    } finally {
        Pop-Location
        $env:PATH = $previousPath
    }
    Write-Host "Installed NuGet consumers and clean npm pack verified: $packageOutput"
} finally {
    Pop-Location
}
