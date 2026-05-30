# MemoryFlightRecorder

Capture memory evidence before your .NET service dies.

## Why This Exists

When a service approaches an out-of-memory condition, the useful evidence is often lost with the process. This library watches memory pressure inside an ASP.NET Core or Worker Service process and tries to write a diagnostic dump plus a small JSON snapshot before the service crashes or is killed by a memory limit.

## Installation

Package id:

```text
A2G.MemoryFlightRecorder
```

Until the package is published, reference the local project or create a local package with:

```bash
dotnet pack src/A2G.MemoryFlightRecorder/A2G.MemoryFlightRecorder.csproj -c Release
```

## Basic Usage

```csharp
using A2G.MemoryFlightRecorder.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryFlightRecorder(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(3);
    options.WarningThreshold = 0.70;
    options.CriticalThreshold = 0.85;
    options.DumpCooldown = TimeSpan.FromMinutes(15);
    options.DumpDirectory = "./memory-dumps";
    options.MaxDumpCount = 3;
    options.MaxDumpDirectorySizeBytes = 2L * 1024 * 1024 * 1024;
});
```

The hosted service starts automatically once registered.

## Configuration

| Option | Default | Purpose |
| --- | --- | --- |
| `PollInterval` | `00:00:03` | How often to capture process and GC memory metrics. |
| `WarningThreshold` | `0.70` | Ratio that triggers warning logs. |
| `CriticalThreshold` | `0.85` | Ratio that triggers dump generation attempts. |
| `DumpCooldown` | `00:15:00` | Minimum time between successful dumps. |
| `DumpDirectory` | `./memory-dumps` | Directory where dump artifacts are written. |
| `DumpType` | `DumpType.WithHeap` | Dump content type used by the diagnostics client. |
| `EnableDumpGeneration` | `true` | Allows dump writing to run. |
| `WriteSnapshotJson` | `true` | Writes a JSON sidecar after a successful dump. |
| `LogDumpGeneration` | `true` | Enables diagnostics-client dump logging. |
| `MaxDumpCount` | `3` | Count-based retention limit for dump artifacts. |
| `MaxDumpDirectorySizeBytes` | `2147483648` | Size-based retention limit for dump artifacts. |
| `MinFreeDiskBytesBeforeDump` | `536870912` | Minimum free disk space required before a dump attempt. |
| `MemoryLimitBytes` | `null` | Optional explicit memory limit when GC does not provide one. |

## Generated Files

Successful dump capture can produce:

- `memory_yyyyMMdd_HHmmss_fff_<pid>.dmp`
- `memory_yyyyMMdd_HHmmss_fff_<pid>.snapshot.json`

The snapshot sidecar contains the captured memory metrics, evaluation result, and a compact view of the active options.

## Security Warning

Memory dumps can contain secrets, tokens, connection strings, request bodies, user data, and other sensitive values. Do not commit dumps to Git. Store them securely and delete them when no longer needed.

## Limitations

This library is a pre-OOM diagnostic guard, not a guaranteed OOM catcher. Sudden memory spikes, single huge allocations, disabled diagnostics, disk exhaustion, or immediate container kills can still prevent dump generation.

If the runtime does not expose a usable memory limit and `MemoryLimitBytes` is not configured, ratio-based warning and critical decisions are skipped.

## Local Demo

The sample app is in `samples/LeakyApi` and binds to `http://127.0.0.1:5000`.

Its sample defaults are read from the `MemoryFlightRecorderSample` section in `samples/LeakyApi/appsettings.json` and can be overridden with `MemoryFlightRecorderSample__...` environment variables.

Run it:

```bash
dotnet run --project samples/LeakyApi/LeakyApi.csproj
```

Trigger memory growth:

```bash
curl -X POST http://127.0.0.1:5000/leak/managed/100
curl -X POST http://127.0.0.1:5000/leak/managed/100
curl http://127.0.0.1:5000/memory/status
```

Useful endpoints:

- `GET /memory/status`
- `POST /leak/managed/{megabytes:int}`
- `POST /leak/loh/{megabytes:int}`
- `POST /leak/clear`

## Docker Demo

The repository includes:

- `samples/LeakyApi/Dockerfile`
- `docker-compose.yml`
- `QUICKSTART.md`

Run the containerized sample from the repository root:

```bash
docker compose up --build
```

The compose setup publishes `http://127.0.0.1:5000`, mounts dump artifacts into `./artifacts/docker-memory-dumps`, and overrides the sample's `appsettings` values through environment variables. For the full local and Docker test flow, see `QUICKSTART.md`.

Cleanup command:

```bash
docker compose down --volumes --remove-orphans
```
