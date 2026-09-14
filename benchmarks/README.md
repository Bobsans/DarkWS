# Manual performance measurements

Run on an idle machine with the SDK pinned by `global.json`:

```powershell
dotnet run --project benchmarks/DarkWS.Benchmarks -c Release
```

Use `-- --quick` for a small execution check. This project is built with the
solution, but its measurements do not run in PR tests or coverage gates.

Scenarios cover repeated versus shared broadcast serialization, actual in-memory
backplane fanout through `IBroadcaster` to 100/1000/10000 recipients, and parsing
1000 request envelopes per operation. The transport sink counts every delivery
and performs no network I/O. It does not model TLS, slow clients, or handler work.

CSV output reports the median across five samples (three in quick mode), with
two warm-up operations outside measurements. Allocations are process-wide so
fanout workers are included; unrelated runtime work can still add noise. Compare
Release runs on the same machine/runtime and keep their CSV output with the
review. These are microbenchmarks, not latency guarantees or CI pass thresholds.

The first local Release run is preserved in [results/2026-09-14.csv](results/2026-09-14.csv).
