# Development

Internal notes for maintainers: building, testing, versioning, publishing, and the
documentation site. User documentation lives on the site (`website/`).

## Build and test

Run the complete build, test, Redis integration, and coverage gate:

```powershell
pwsh ./scripts/test-coverage.ps1
```

Docker must be running for Redis integration tests. The gates require at least 90%
line coverage and 80% branch coverage for every package.

The gate also packs all NuGet libraries, verifies documentation/symbols, restores
them into a consumer with an isolated package cache, and runs it on all target
frameworks. A fresh npm copy without `dist` is packed to verify the prepack build.
CI retains the resulting packages as artifacts. Successful actions and malformed
client requests are logged at Debug; unexpected handler failures remain warnings.

All NuGet packages include XML API documentation and portable symbol packages with
embedded source files. `scripts/test-packages.ps1` checks every target's XML and PDB
entries after packing.

Manual performance scenarios live in [benchmarks](../benchmarks/README.md); they are
not run on every PR. All libraries enforce reviewed public API baselines with
PublicApiAnalyzers; see [API maintenance](public-api.md).

## Versioning

The build uses the exact SDK in `global.json`. Common project settings live in
`Directory.Build.props`; NuGet versions are centralized in `Directory.Packages.props`.

All packages use one SemVer version. Update every manifest and the npm lockfile with
one command:

```powershell
pwsh ./scripts/set-version.ps1 4.0.0
```

CI runs `scripts/test-version.ps1` and rejects inconsistent package versions. Release
tags must use the matching `vX.Y.Z` form, including an optional SemVer prerelease
suffix such as `v3.0.0-rc.1`.

The 2.1.0 changelog entry explains the 1 MiB input limit and migration for
applications sending larger messages. Future changes that reject previously accepted
input must include a behavior-change entry, migration/configuration guidance, and a
SemVer compatibility decision before release. Substantial breaking defaults require a
major release or an explicit compatibility option. The release workflow requires a
changelog heading for the published version. Update `website/docs/upgrading.md` (and
its Russian copy) for every major release.

## Publishing

Publishing uses GitHub Actions OIDC trusted publishing. No long-lived NuGet or npm
publish tokens are stored in GitHub.

One-time registry setup:

1. Create GitHub environment `release` and optionally add required reviewers.
2. Add repository variable `NUGET_USER` with the NuGet.org profile name.
3. On NuGet.org, add a trusted publishing policy for owner `Bobsans`, repository
   `DarkWS`, workflow `release.yml`, and environment `release`.
4. On the existing `darkws` npm package, configure its trusted publisher for owner
   `Bobsans`, repository `DarkWS`, workflow `release.yml`, environment `release`,
   with direct `npm publish` allowed.

For each release:

1. Run `scripts/set-version.ps1` and commit the version change.
2. Create a GitHub Release using the matching `vX.Y.Z` tag.
3. The release workflow validates the tag and runs every test and coverage gate in a
   `verify` job that has no publishing permission; the gate packs, inspects, and
   installs the packages it keeps as artifacts. A separate `publish` job in the
   `release` environment, the only job with `id-token: write`, downloads exactly those
   files and publishes all NuGet packages and the npm tarball without installing
   dependencies or rebuilding.

Prerelease versions require a GitHub prerelease and use the npm `next` tag. Stable
versions use the npm `latest` tag. Re-running a release is safe: NuGet uses
`--skip-duplicate`, and npm skips an already published version.

## Documentation site

The Docusaurus site in `website/` is built and deployed to GitHub Pages by
`.github/workflows/docs.yml`. See [website/README.md](../website/README.md) for local
commands, translations, and versioning. Pages exist in English (`website/docs`) and
Russian (`website/i18n/ru/...`); update both when behavior changes.

### Website dependency checks

Run `npm ci --prefix website`, `npm audit --prefix website`,
`pwsh ./scripts/docs-api.ps1`, and `npm run build --prefix website` after changing
the website lockfile. The build generates the TypeDoc reference and both locales.

Run `npm ci --prefix packages/darkws` and `pwsh ./scripts/test-docs.ps1` to check
the actual quickstart/chat C# blocks from both locales and versions. Temporary
consumers use current project sources or NuGet/npm 4.0.0 for the 4.x snapshot.
Real loopback sockets and the browser SDK check startup, presence, reconnect,
logout/replacement, and anonymous connections; the HTML input name is checked too.
Docs CI runs this gate before generating and building the site.

The website overrides `serialize-javascript` to 7.0.5 for the CommonJS consumers
`copy-webpack-plugin` and `css-minimizer-webpack-plugin`. Its Node >=20 requirement
matches Docusaurus 3.10.2 and the site's engine; CI uses Node 24. This fixes
[GHSA-5c6j-r48x-rmvq](https://github.com/advisories/GHSA-5c6j-r48x-rmvq) and
[GHSA-qj8w-gfj5-8c6v](https://github.com/advisories/GHSA-qj8w-gfj5-8c6v).

SockJS uses only `require('uuid').v4()` without a buffer, so its current call site
does not trigger the v3/v5/v6 buffer issue in
[GHSA-w5hq-g745-h8pq](https://github.com/advisories/GHSA-w5hq-g745-h8pq).
The scoped override to uuid 11.1.1 retains its CommonJS export and also removes
that vulnerable version from the lockfile. Recheck the overrides when Docusaurus
updates its dependency ranges.

The 30 September 2026 audit's 21 affected dependency nodes were propagated from
two leaf packages and three advisories. These are conditional website build-tool
risks, not 21 separate DarkWS server vulnerabilities; the runtime SDK has no npm
dependencies. Keep Docusaurus 3.10.2 rather than applying the unrelated downgrade
suggested by `npm audit fix --force`.

The performance page quotes a benchmark run and the current test counts and coverage;
refresh them from `benchmarks/results/` and the coverage gate output before a release.

## Roadmap

- An optional high-level browser API for token refresh, safe read retries, and
  application-level error handling.

## Contributing

Keep changes focused, add behavior tests, and run the complete coverage gate before
opening a pull request. Report vulnerabilities privately as described in
[SECURITY](../SECURITY.md), not in public issues.
