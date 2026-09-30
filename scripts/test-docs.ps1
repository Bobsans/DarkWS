$ErrorActionPreference = "Stop"
$repository = Split-Path -Parent $PSScriptRoot
$temporaryBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporary = Join-Path $temporaryBase ("darkws-docs-" + [guid]::NewGuid().ToString("N"))
$oldScript = $env:DARKWS_DOCS_NODE_SCRIPT
$oldSdk = $env:DARKWS_DOCS_SDK

function Read-CSharpBlocks([string]$path) {
    $content = [System.IO.File]::ReadAllText($path)
    return @([regex]::Matches($content, '(?ms)^```csharp[^\r\n]*\r?\n(.*?)^```\s*$') | ForEach-Object { $_.Groups[1].Value })
}

Push-Location $repository
try {
    New-Item -ItemType Directory -Path $temporary | Out-Null
    $currentSdk = Join-Path $temporary "sdk"
    node packages/darkws/node_modules/typescript/bin/tsc -p packages/darkws/tsconfig.json --outDir $currentSdk --declaration false --declarationMap false --sourceMap false
    if ($LASTEXITCODE -ne 0) { throw "The browser SDK compilation failed" }
    Set-Content -LiteralPath (Join-Path $currentSdk "package.json") -Value '{"type":"module"}'
    $legacySdk = Join-Path $temporary "sdk4"
    npm install --prefix $legacySdk darkws@4.0.0 --ignore-scripts --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw "The documented 4.0.0 browser SDK installation failed" }
    $legacyPackage = Join-Path $legacySdk "node_modules/darkws"
    $legacyManifest = Get-Content -Raw -LiteralPath (Join-Path $legacyPackage "package.json") | ConvertFrom-Json
    $env:DARKWS_DOCS_NODE_SCRIPT = Join-Path $PSScriptRoot "docs-smoke/chat.mjs"
    $cases = @(
        @{ Folder = "website/docs"; Legacy = $false },
        @{ Folder = "website/i18n/ru/docusaurus-plugin-content-docs/current"; Legacy = $false },
        @{ Folder = "website/versioned_docs/version-4.x"; Legacy = $true },
        @{ Folder = "website/i18n/ru/docusaurus-plugin-content-docs/version-4.x"; Legacy = $true }
    )
    foreach ($case in $cases) {
        $env:DARKWS_DOCS_SDK = if ($case.Legacy) { Join-Path $legacyPackage $legacyManifest.main } else { Join-Path $currentSdk "index.js" }
        foreach ($page in @("getting-started.md", "guides/chat.md")) {
            $sourcePath = Join-Path $repository (Join-Path $case.Folder $page)
            $blocks = Read-CSharpBlocks $sourcePath
            $chat = $page -eq "guides/chat.md"
            if ($chat -and [System.IO.File]::ReadAllText($sourcePath) -notmatch '<input\s+aria-label="Message"') {
                throw "The chat message input must have its accessible name: $sourcePath"
            }
            $segments = if ($chat) { @($blocks[4]) + $blocks[0..3] } else { $blocks[0..1] }
            $source = $segments -join "`n"
            $usings = @([regex]::Matches($source, '(?m)^using [\w.]+;\r?$') | ForEach-Object { $_.Value.Trim() } | Select-Object -Unique)
            $source = [regex]::Replace($source, '(?m)^using [\w.]+;\r?\n', '')
            if ([regex]::Matches($source, 'app\.Run\(\);').Count -ne 1) { throw "Expected exactly one runnable server in $sourcePath" }
            $source = $source.Replace('app.Run();', "await DocsSmoke.RunAsync(app, $($chat.ToString().ToLowerInvariant()), $($case.Legacy.ToString().ToLowerInvariant()));")
            $consumer = Join-Path $temporary ([guid]::NewGuid().ToString("N"))
            New-Item -ItemType Directory -Path $consumer | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $consumer "Program.cs"), (($usings -join "`n") + "`n" + $source))
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot "docs-smoke/DocsSmoke.cs") -Destination $consumer
            $reference = if ($case.Legacy) { '<PackageReference Include="DarkWS" Version="4.0.0" />' } else {
                '<ProjectReference Include="' + [System.Security.SecurityElement]::Escape((Join-Path $repository "DarkWS/DarkWS.csproj")) + '" />'
            }
            $project = Join-Path $consumer "DocsSmoke.csproj"
            [System.IO.File]::WriteAllText($project, @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup>$reference</ItemGroup>
</Project>
"@)
            dotnet run --project $project -c Release --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "Documentation snippet failed: $sourcePath" }
            Write-Host "Documentation snippet passed: $($case.Folder)/$page"
        }
    }
} finally {
    $env:DARKWS_DOCS_NODE_SCRIPT = $oldScript
    $env:DARKWS_DOCS_SDK = $oldSdk
    Pop-Location
    $resolvedTemporary = [System.IO.Path]::GetFullPath($temporary)
    if (!$resolvedTemporary.StartsWith($temporaryBase, [System.StringComparison]::OrdinalIgnoreCase) -or !(Split-Path -Leaf $resolvedTemporary).StartsWith("darkws-docs-")) {
        throw "Refusing cleanup outside the temporary documentation workspace"
    }
    if (Test-Path -LiteralPath $resolvedTemporary) { Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force }
}
