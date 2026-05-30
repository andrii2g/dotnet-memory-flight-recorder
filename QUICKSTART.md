# Quickstart

This file gives the shortest path to verify the sample app both on your local machine and inside Docker. The Docker flow is intended to work cleanly from WSL as well.

- [Local PC](#local-pc)
- [Docker / WSL](#docker--wsl)

## Local PC

### 1. Build and test

```bash
dotnet build A2G.MemoryFlightRecorder.slnx
dotnet test tests/A2G.MemoryFlightRecorder.Tests/A2G.MemoryFlightRecorder.Tests.csproj
```

### 2. Run the sample

```bash
dotnet run --project samples/LeakyApi/LeakyApi.csproj
```

By default it reads sample settings from:

```text
samples/LeakyApi/appsettings.json
samples/LeakyApi/appsettings.Development.json
```

The shipped defaults set:

```text
http://127.0.0.1:5000
```

You can override any sample setting with `MemoryFlightRecorderSample__...` environment variables when needed.

### 3. Check baseline status

```bash
curl http://127.0.0.1:5000/memory/status
```

### 4. Push memory upward

```bash
curl -X POST http://127.0.0.1:5000/leak/managed/100
curl -X POST http://127.0.0.1:5000/leak/managed/100
curl -X POST http://127.0.0.1:5000/leak/loh/50
curl http://127.0.0.1:5000/memory/status
```

Expected behavior:

- warning logs appear after the warning threshold is crossed
- critical logs appear after the critical threshold is crossed
- a dump and snapshot appear under `samples/LeakyApi/bin/Debug/net8.0/memory-dumps/` or the published app directory if you run a published build

### 5. Reset leaked allocations

```bash
curl -X POST http://127.0.0.1:5000/leak/clear
```

## Docker / WSL

### 1. Build and start the container

From the repository root:

```bash
docker compose up --build
```

The compose file publishes:

```text
http://127.0.0.1:5000
```

Inside the container, the sample listens on `0.0.0.0:5000` and writes dumps to `/app/memory-dumps`.

### 2. Verify the container is reachable

```bash
curl http://127.0.0.1:5000/memory/status
```

### 3. Trigger pressure in the container

```bash
curl -X POST http://127.0.0.1:5000/leak/managed/100
curl -X POST http://127.0.0.1:5000/leak/managed/100
curl -X POST http://127.0.0.1:5000/leak/loh/50
curl http://127.0.0.1:5000/memory/status
```

Expected behavior:

- the app logs warning and critical transitions in `docker compose` output
- dump artifacts are written to `./artifacts/docker-memory-dumps/` on the host

### 4. Inspect generated dump artifacts

```bash
ls -lah artifacts/docker-memory-dumps
```

You should see files like:

```text
memory_yyyyMMdd_HHmmss_fff_<pid>.dmp
memory_yyyyMMdd_HHmmss_fff_<pid>.snapshot.json
```

### 5. Stop the container

```bash
docker compose down
```

### 6. Cleanup containers, volumes, and dump artifacts

If you want to remove the compose containers, network, attached volumes, and any dump files written into the bind-mounted host directory:

```bash
docker compose down --volumes --remove-orphans && rm -rf artifacts/docker-memory-dumps
```

If you want to keep the generated dump files on the host, use:

```bash
docker compose down --volumes --remove-orphans
```

## Useful Environment Variables

The sample app reads the `MemoryFlightRecorderSample` section from `appsettings*.json`. The matching environment-variable overrides are:

- `MemoryFlightRecorderSample__Urls`
- `MemoryFlightRecorderSample__DumpDirectory`
- `MemoryFlightRecorderSample__MemoryLimitBytes`
- `MemoryFlightRecorderSample__PollIntervalSeconds`
- `MemoryFlightRecorderSample__WarningThreshold`
- `MemoryFlightRecorderSample__CriticalThreshold`
- `MemoryFlightRecorderSample__DumpCooldownSeconds`

Environment variables override the values from `appsettings*.json`, which makes local and container testing use the same sample binary with different runtime settings.
