# Public API maintenance

All four libraries reference Microsoft.CodeAnalysis.PublicApiAnalyzers as a private
build dependency. Ordinary builds and CI reject undeclared API additions/removals,
duplicate entries, missing baseline files, and inconsistent removal markers.
Nullability is tracked alongside signatures and optional parameter defaults.
A build target requires both baseline files before compilation, so deleting both
cannot silently disable the analyzer.

`PublicAPI.Shipped.txt` records the released 4.0.0 surface. The initial 2.1.0
baseline and reviewed migration remain available in Git history;
`PublicAPI.Unshipped.txt` is reserved for changes after 4.0.0. 5.0.0 removes the
legacy static forwarding classes `Configuration` and `RedisConfiguration`, and wire
envelope records are internal: the JSON protocol is documented in the README, not
as .NET types.

## Naming

Public types belong to one of three families; a new type follows the nearest one.

- Configuration and infrastructure keep the `DarkWs` prefix, because their plain
  names would clash with ASP.NET or application types: options, builder, session,
  context, authenticator, backplane, connections, protocol, hooks, the base
  exception, and the test host (`DarkWsOptions`, `IDarkWsSession`, `DarkWsConnectionHooks`).
- Handler-authoring types have short names: `HandlerBase`, `HandlerAttribute`,
  `ActionAttribute`, `IResponse`, `SuccessResponse`, `ErrorResponse`, `ResponseContext`,
  `ErrorResponseException`, and `IWebSocketConnection`.
- Broadcast types use the `Broadcast` prefix: `BroadcastTarget`, `BroadcastTargetType`,
  `BroadcastExclusion`, `BroadcastMessage`, and the service `IBroadcaster`.

The same concept keeps the same member name on every type: a recipient kind is
`Type` (`TargetType` on the envelope), its key `Id` (`TargetId`), a group union
`GroupNames`, and exclusions `Except`.

The new client and DI packages record their complete initial 4.0.0 API in Shipped.
The client's initial request overloads intentionally distinguish omitted payload
from explicit null; RS0026 is suppressed locally for these declared overloads.

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
