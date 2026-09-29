using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DarkWS.Test")]
[assembly: InternalsVisibleTo("DarkWS.Testing")]
// Redis delivery tests register custom slow recipients; public-API contract tests live in DarkWS.Testing.Test.
[assembly: InternalsVisibleTo("DarkWS.Redis.Test")]
// The unpublished benchmark registers allocation-free sink connections directly in storage.
[assembly: InternalsVisibleTo("DarkWS.Benchmarks")]
