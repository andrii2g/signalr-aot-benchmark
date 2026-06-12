# SignalR Native AOT Benchmark

## What this is

A tiny benchmark/demo comparing the same ASP.NET Core SignalR chat server published as:

- self-contained JIT
- Native AOT

## Why

To make Native AOT tradeoffs visible for a real-time SignalR workload.

## Prerequisites

- .NET 10 SDK
- Bash on Linux/macOS/WSL
- Python 3 for report generation

## Quick start

```bash
./scripts/publish-jit.sh
./scripts/publish-aot.sh
./scripts/run-benchmark.sh
cat results/report.md
```

## Run the manual chat page

```bash
ASPNETCORE_URLS=http://127.0.0.1:5201 ./artifacts/publish/jit/Chat.Jit.Web
# Open http://127.0.0.1:5201
```

For the AOT server:

```bash
ASPNETCORE_URLS=http://127.0.0.1:5202 ./artifacts/publish/aot/Chat.Aot.Web
# Open http://127.0.0.1:5202
```

## Configure benchmark size

```bash
CONNECTIONS=200 MESSAGES_PER_CONNECTION=200 ./scripts/run-benchmark.sh
```

Other useful overrides:

```bash
MESSAGE_SIZE=256 RECEIVE_TIMEOUT_SECONDS=60 ./scripts/run-benchmark.sh
RID=linux-x64 ./scripts/publish-aot.sh
```

## Metrics explained

- Publish size MB: total size of files in the published app directory.
- Startup ms: elapsed time from server process launch until `/ready` responds.
- Peak RSS MB: highest sampled resident memory for the server process.
- Actual messages sent: number of client-to-hub sends completed successfully.
- Actual messages received: number of broadcasts received by all clients.
- Receive completeness %: received broadcasts divided by expected broadcasts.
- Sent messages/sec: successful sends divided by send duration.
- Received messages/sec: received broadcasts divided by total run duration.
- Average latency ms: average client-observed broadcast latency.
- P50 latency ms: median client-observed broadcast latency.
- P95 latency ms: 95th percentile client-observed broadcast latency.
- P99 latency ms: 99th percentile client-observed broadcast latency.
- Send errors: number of failed hub invocations from the load client.

## Important limitations

- This is not a production-grade benchmark.
- Results vary by machine, OS, CPU power policy, and background load.
- AOT publish is RID-specific.
- Throughput is not guaranteed to improve with AOT.
- Browser chat page uses CDN-hosted SignalR JavaScript; benchmark uses the .NET SignalR client.

## Native AOT notes

The AOT app avoids MVC, Razor Pages, Blazor Server, dynamic serialization, and reflection-heavy application code. SignalR uses the JSON protocol with source-generated `System.Text.Json` metadata shared by both server modes and the load client.
