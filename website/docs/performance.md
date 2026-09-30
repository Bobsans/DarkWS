---
sidebar_position: 7
title: Performance and quality
---

# Performance and quality

## Design choices that matter for throughput

- **One serialization per broadcast.** A broadcast is serialized once and the same
  bytes are written to every recipient.
- **Concurrent fanout.** Local recipients are written in parallel under one shared
  deadline, so a slow socket is aborted without holding up the rest.
- **Bounded work per connection.** A fixed number of concurrent requests and a
  bounded queue keep one busy client from exhausting the server, while heartbeats are
  answered immediately.
- **Serialized writes.** Each socket has one writer at a time; responses and
  broadcasts never interleave frames.
- **Redis delivery slots.** Up to 16 Redis deliveries run concurrently per instance.

## Benchmarks

Microbenchmarks from a local Release run of 5.0 on .NET 10.0.12, AMD Ryzen 7 3700X,
Windows 11 (median of five samples, 364-byte payload). The transport is an in-memory
sink that performs no network I/O, so these numbers show library overhead, not
end-to-end latency.

| Scenario | Recipients | Time per operation | Allocated per operation |
| --- | ---: | ---: | ---: |
| Serialize per recipient | 100 | 0.266 ms | 39.2 KB |
| Serialize once | 100 | 0.004 ms | 1.2 KB |
| In-memory fanout | 100 | 0.021 ms | 3.5 KB |
| Serialize per recipient | 1 000 | 2.901 ms | 385 KB |
| Serialize once | 1 000 | 0.009 ms | 1.2 KB |
| In-memory fanout | 1 000 | 0.143 ms | 11.8 KB |
| Serialize per recipient | 10 000 | 6.040 ms | 3.84 MB |
| Serialize once | 10 000 | 0.021 ms | 1.2 KB |
| In-memory fanout | 10 000 | 2.202 ms | 102 KB |
| Parse 1 000 requests | – | 4.438 ms | 984 KB |

- *Serialize per recipient* and *serialize once* compare naive per-client
  serialization with DarkWS's shared payload.
- *In-memory fanout* is a real `IBroadcaster.PublishAsync` through the in-memory
  backplane to every recipient: about 0.14 µs and 12 bytes per recipient at 1 000
  recipients.
- *Parse 1 000 requests* parses request envelopes, about 4 µs per request.

Run them yourself on an idle machine:

```bash
dotnet run --project benchmarks/DarkWS.Benchmarks -c Release
```

Results depend on hardware and runtime; compare runs on the same machine. They do not
model TLS, slow clients, or handler work.

## Tests

Every change runs the full suite on .NET 8, 9, and 10:

| Suite | Tests |
| --- | ---: |
| Server (`DarkWS`) | 193 |
| .NET client | 56 |
| Testing package | 29 |
| Redis backplane (real Redis in Docker) | 27 |
| Browser client (Vitest) | 161 |

Coverage gates fail the build below 90% lines or 80% branches for any package.
Current coverage:

| Package | Lines | Branches |
| --- | ---: | ---: |
| `DarkWS` | 97.6% | 94.3% |
| `DarkWS.Redis` | 93.2% | 91.7% |
| `DarkWS.Client` | 96.3% | 92.3% |
| `DarkWS.Client.DependencyInjection` | 100% | 100% |
| `DarkWS.Testing` | 100% | 100% |
| `darkws` | 99.0% | 97.1% |

Besides unit tests, the gate:

- runs the browser client, compiled from source, on Node.js against a real DarkWS
  server;
- tests the Redis backplane against StackExchange.Redis 2.13.17 and 3.2.1;
- packs every NuGet package, checks its XML documentation and symbols, installs it
  into a clean consumer project, and runs it on all target frameworks;
- packs the npm package from a clean copy to verify its build;
- runs a Redis load regression that checks pending work and retained memory under a
  stuck recipient.

## API stability

Public APIs are tracked with PublicApiAnalyzers: any change to the public surface must
be declared and reviewed, or the build fails. Breaking changes ship only in major
versions, with migration notes in the changelog and in [Upgrading](upgrading.md).
