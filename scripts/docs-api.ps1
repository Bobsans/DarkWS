# Generates the .NET API reference for the documentation site into website/docs/api/dotnet.
$ErrorActionPreference = "Stop"
$repository = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repository "website/docs/api/dotnet"
Push-Location $repository
try {
    $packages = (dotnet msbuild DarkWS/DarkWS.csproj -getProperty:DarkWsPackages).Trim().Split(';')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if (Test-Path $output) { Remove-Item -Recurse -Force $output }
    foreach ($package in $packages) {
        dotnet build "$package/$package.csproj" -c Release -f net10.0
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $packageOutput = Join-Path $output $package
        New-Item -ItemType Directory -Path $packageOutput -Force | Out-Null
        dotnet defaultdocumentation `
            -a "$package/bin/Release/net10.0/$package.dll" `
            -o $packageOutput `
            -n Overview `
            -s Public,Protected,ProtectedInternal `
            -g Namespaces,Types `
            -h Warning
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        # Docusaurus checks anchors by id, not by the legacy name attribute.
        foreach ($page in Get-ChildItem -LiteralPath $packageOutput -Filter *.md) {
            $content = [System.IO.File]::ReadAllText($page.FullName)
            [System.IO.File]::WriteAllText($page.FullName, $content.Replace("<a name='", "<a id='"))
        }
        Set-Content -LiteralPath (Join-Path $packageOutput "_category_.json") -Value "{ `"label`": `"$package`" }"
    }
    Set-Content -LiteralPath (Join-Path $output "_category_.json") -Value '{ "label": ".NET", "position": 1 }'
} finally {
    Pop-Location
}
