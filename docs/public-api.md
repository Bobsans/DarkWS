# Public API maintenance

Both libraries reference Microsoft.CodeAnalysis.PublicApiAnalyzers as a private
build dependency. Ordinary builds and CI reject undeclared API additions/removals,
duplicate entries, missing baseline files, and inconsistent removal markers.
Nullability is tracked alongside signatures and optional parameter defaults.
A build target requires both baseline files before compilation, so deleting both
cannot silently disable the analyzer.

`PublicAPI.Shipped.txt` was generated from the tracked `v2.1.0` source with the
pinned SDK. `PublicAPI.Unshipped.txt` contains the reviewed differences in this
working version. The old extension entries marked `*REMOVED*` correspond to moving
extension syntax to dedicated classes; their original static forwarding methods
still exist and remain binary-callable.

For an intentional API change:

1. Build and review the analyzer diagnostic. Decide whether the change is compatible.
2. Apply the `Declare public API` IDE code fix (RS0016) or run
   `dotnet format analyzers DarkWS.sln --diagnostics RS0016 --no-restore`.
3. Record intentional removals in Unshipped with the `*REMOVED*` prefix; do not
   silently delete historical Shipped entries.
4. Review the baseline diff, migration notes, and SemVer impact, then run the full
   test/package gate on all target frameworks.

At release, promote approved additions to Shipped and reconcile approved removal
markers with their old Shipped entries. Keep Unshipped for the next release.
Changing baseline files is a review decision, not a way to suppress an unexpected
compatibility diagnostic. No analyzer package is exposed as a runtime dependency
of the published NuGet packages.
