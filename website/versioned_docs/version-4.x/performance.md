---
sidebar_position: 7
title: Performance and quality
---

# Performance and quality

## Design choices that matter for throughput

- **One serialization per broadcast.** A broadcast is serialized once and the same
  bytes are written to every local recipient.
- **Parallel fanout.** Local recipients are written in parallel, each bounded by
  `BroadcastSendTimeout`, so a slow socket is aborted without holding up the rest.
- **Bounded work per connection.** At most `MaxConcurrentRequestsPerConnection`
  requests run at once; further reads wait, so one busy client cannot queue
  unbounded work.
- **Serialized writes.** Each socket has one writer at a time; responses and
  broadcasts never interleave frames.

## Benchmarks

Microbenchmarks from a local Release run of tag `v4.0.0` on .NET 10.0.12, AMD Ryzen 7
3700X, Windows 11 (median of five samples, 364-byte payload). The transport is an
in-memory sink that performs no network I/O, so these numbers show library overhead,
not end-to-end latency.

| Scenario | Recipients | Time per operation | Allocated per operation |
| --- | ---: | ---: | ---: |
| Serialize per recipient | 100 | 0.356 ms | 39.2 KB |
| Serialize once | 100 | 0.004 ms | 1.2 KB |
| In-memory fanout | 100 | 0.210 ms | 36.3 KB |
| Serialize per recipient | 1 000 | 3.693 ms | 385 KB |
| Serialize once | 1 000 | 0.010 ms | 1.2 KB |
| In-memory fanout | 1 000 | 2.680 ms | 268 KB |
| Serialize per recipient | 10 000 | 6.431 ms | 3.84 MB |
| Serialize once | 10 000 | 0.023 ms | 1.2 KB |
| In-memory fanout | 10 000 | 13.656 ms | 2.65 MB |
| Parse 1 000 requests | – | 2.349 ms | 984 KB |

- *Serialize per recipient* and *serialize once* compare naive per-client
  serialization with DarkWS's shared payload.
- *In-memory fanout* is a real broadcast through `IBroadcaster` and the in-memory
  backplane to every recipient.
- *Parse 1 000 requests* parses request envelopes, about 2 µs per request.

5.0 reworked local delivery: the same fanout to 1 000 recipients takes about 0.14 ms
and 12 KB there (see the 5.x documentation).

Results depend on hardware and runtime; compare runs on the same machine. They do not
model TLS, slow clients, or handler work.

## Tests

The 4.0.0 suite runs on .NET 8, 9, and 10:

| Suite | Tests |
| --- | ---: |
| Server (`DarkWS`) | 109 |
| .NET client and DI | 41 |
| Redis backplane (real Redis in Docker) | 14 |
| Browser client (Vitest) | 41 |

Coverage gates fail the build below 90% lines or 80% branches for any package.
Coverage at 4.0.0:

| Package | Lines | Branches |
| --- | ---: | ---: |
| `DarkWS` | 95.4% | 91.8% |
| `DarkWS.Redis` | 96.1% | 100% |
| `DarkWS.Client` | 98.4% | 91.7% |
| `DarkWS.Client.DependencyInjection` | 100% | 100% |
| `darkws` | 97.4% | 92.6% |

Besides unit tests, the gate tests the Redis backplane against StackExchange.Redis
2.13.17 and 3.2.1, packs every NuGet package, checks its XML documentation and
symbols, installs it into a clean consumer project, runs it on all target frameworks,
and packs the npm package from a clean copy to verify its build.

## API stability

Public APIs are tracked with PublicApiAnalyzers: any change to the public surface must
be declared and reviewed, or the build fails. Breaking changes ship only in major
versions, with migration notes in the changelog.
