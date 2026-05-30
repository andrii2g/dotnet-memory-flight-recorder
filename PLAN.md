# PLAN.md — DotNet.MemoryFlightRecorder

## Project Summary

Build a small, reusable .NET library that runs inside an ASP.NET Core or Worker Service application and watches memory pressure. When memory usage becomes dangerous, the library writes a diagnostic memory dump plus a compact JSON snapshot before the process crashes, throws `OutOfMemoryException`, or gets killed by a container limit.

Working project name:

```text
DotNet.MemoryFlightRecorder
```

One-line value proposition:

```text
Capture memory evidence before your .NET service dies.
```

## Codex Instructions

Implement this repository from scratch. Keep the first version practical and not over-engineered.

Primary goal:

- Create a NuGet-ready class library that can be enabled with `services.AddMemoryFlightRecorder(...)`.

Secondary goal:

- Add a tiny demo application that intentionally leaks memory and proves that warning logs, critical logs, dumps, and snapshot files are generated.

Do not implement these in the MVP:

- Cloud upload.
- Encryption.
- Kubernetes operator or sidecar.
- Native heap profiler.
- UI dashboard.
- OpenTelemetry exporter.
- Complex cgroup parsing.

These can be future enhancements after the MVP works.

## Source Idea

The original concept proposed an in-process `IHostedService` / `BackgroundService` that periodically checks memory metrics with `GC.GetGCMemoryInfo()` and process metrics, logs warnings, and uses `Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient` to write a `.dmp` file on critical memory pressure.

This plan keeps that core idea, but fixes the known implementation traps before coding.

## Non-Negotiable Correctness Rules

Apply these rules exactly:

1. Do not call this project "zero dependency".
   - Dump generation depends on `Microsoft.Diagnostics.NETCore.Client`.
   - Use wording such as "small dependency footprint" or "minimal dependency".

2. Do not use `GC.GetGCMemoryInfo().MemoryLoadBytes / TotalAvailableMemoryBytes` as the process memory usage ratio.
   - `MemoryLoadBytes` is GC-observed physical memory load context.
   - Use `Process.WorkingSet64` and `Process.PrivateMemorySize64` for process pressure.
   - Use GC metrics for managed heap pressure.

3. Use the correct GC pause property.
   - Use `GCMemoryInfo.PauseTimePercentage`.
   - Do not use `PauseDurationPercentage`.

4. Do not describe `DumpType.WithHeap` as tiny.
   - It is useful for managed-memory analysis, but can be large.
   - Make dump type configurable.
   - Use disk checks, retention, cooldown, and conservative thresholds.

5. Do not wait until memory is almost exhausted.
   - In containers, writing a dump can itself increase memory pressure.
   - Use conservative defaults:
     - warning threshold: `0.70`
     - critical dump threshold: `0.85`

6. Do not write dumps into `AppContext.BaseDirectory` by default.
   - Use a configurable dump directory.
   - Default to `./memory-dumps`.

7. Handle diagnostics-disabled environments.
   - Dump generation can fail if diagnostics are disabled with environment variables such as `DOTNET_EnableDiagnostics=0` or `DOTNET_EnableDiagnostics_IPC=0`.
   - Catch failures and log actionable messages.

8. Failed dump attempts must not arm the successful-dump cooldown.
   - Update the cooldown timestamp only after the dump writer confirms that a `.dmp` file was written.
   - Diagnostics-disabled failures, disk failures, cancellation, and writer exceptions must not suppress the next critical-memory attempt.

9. Unknown memory limits must not become fake 1-byte limits.
   - If no valid memory limit is available, return an explicit unavailable limit result.
   - The evaluator must skip ratio-based warning/critical decisions instead of creating permanent 100% pressure.

10. Retention must treat a dump and its `.snapshot.json` as one logical artifact pair.
    - Count-based retention deletes pairs together.
    - Directory-size retention deletes pairs together.
    - Orphan `.snapshot.json` files can be deleted.

11. Options configuration must not run twice during registration.
    - Do not manually instantiate options and invoke the user's `configure` callback.
    - Register the callback in the normal options pipeline.
    - Validate the same options instance that dependency injection will provide.

12. Validate the diagnostics API surface before implementing.
    - The plan uses the documented `DiagnosticsClient.WriteDumpAsync(DumpType, string, bool, CancellationToken)` overload.
    - The documented `WriteDumpAsync(DumpType, string, WriteDumpFlags, CancellationToken)` overload is also valid if the implementation chooses flags.
    - `DumpType.WithHeap` is a valid dump type.

## Target Audience

This library is for:

- ASP.NET Core APIs.
- Worker Services.
- Long-running background processors.
- Services running in Docker or Kubernetes.
- Developers investigating managed heap leaks, LOH pressure, unmanaged memory growth, or unexplained OOM kills.

## Repository Layout

Create this structure:

```text
.
├── PLAN.md
├── README.md
├── LICENSE
├── .gitignore
├── DotNet.MemoryFlightRecorder.sln
├── src
│   └── DotNet.MemoryFlightRecorder
│       ├── DotNet.MemoryFlightRecorder.csproj
│       ├── DependencyInjection
│       │   └── ServiceCollectionExtensions.cs
│       ├── Dumping
│       │   ├── DiagnosticsDumpWriter.cs
│       │   ├── DumpFileNamer.cs
│       │   ├── DumpRetentionPolicy.cs
│       │   └── IDumpWriter.cs
│       ├── Evaluation
│       │   ├── DefaultMemoryPressureEvaluator.cs
│       │   ├── IMemoryPressureEvaluator.cs
│       │   ├── MemoryPressureDecision.cs
│       │   └── MemoryPressureLevel.cs
│       ├── Limits
│       │   ├── DefaultMemoryLimitProvider.cs
│       │   ├── IMemoryLimitProvider.cs
│       │   └── MemoryLimit.cs
│       ├── Monitoring
│       │   ├── IMemorySnapshotProvider.cs
│       │   ├── MemoryFlightRecorderService.cs
│       │   ├── MemorySnapshot.cs
│       │   └── ProcessMemorySnapshotProvider.cs
│       └── Options
│           ├── MemoryFlightRecorderOptions.cs
│           └── MemoryFlightRecorderOptionsValidator.cs
├── samples
│   └── LeakyApi
│       ├── LeakyApi.csproj
│       └── Program.cs
└── tests
    └── DotNet.MemoryFlightRecorder.Tests
        ├── DotNet.MemoryFlightRecorder.Tests.csproj
        ├── DefaultMemoryPressureEvaluatorTests.cs
        ├── DumpRetentionPolicyTests.cs
        ├── MemoryFlightRecorderOptionsTests.cs
        └── MemoryFlightRecorderServiceTests.cs
```

## Initial Commands

Run these commands from the repository root:

```bash
dotnet new sln -n DotNet.MemoryFlightRecorder

dotnet new classlib \
  -n DotNet.MemoryFlightRecorder \
  -o src/DotNet.MemoryFlightRecorder \
  -f net8.0

dotnet new web \
  -n LeakyApi \
  -o samples/LeakyApi \
  -f net8.0

dotnet new xunit \
  -n DotNet.MemoryFlightRecorder.Tests \
  -o tests/DotNet.MemoryFlightRecorder.Tests \
  -f net8.0

dotnet sln add src/DotNet.MemoryFlightRecorder/DotNet.MemoryFlightRecorder.csproj
dotnet sln add samples/LeakyApi/LeakyApi.csproj
dotnet sln add tests/DotNet.MemoryFlightRecorder.Tests/DotNet.MemoryFlightRecorder.Tests.csproj

dotnet add samples/LeakyApi/LeakyApi.csproj reference src/DotNet.MemoryFlightRecorder/DotNet.MemoryFlightRecorder.csproj
dotnet add tests/DotNet.MemoryFlightRecorder.Tests/DotNet.MemoryFlightRecorder.Tests.csproj reference src/DotNet.MemoryFlightRecorder/DotNet.MemoryFlightRecorder.csproj
```

Add packages:

```bash
dotnet add src/DotNet.MemoryFlightRecorder/DotNet.MemoryFlightRecorder.csproj package Microsoft.Diagnostics.NETCore.Client
dotnet add src/DotNet.MemoryFlightRecorder/DotNet.MemoryFlightRecorder.csproj package Microsoft.Extensions.Hosting.Abstractions
dotnet add src/DotNet.MemoryFlightRecorder/DotNet.MemoryFlightRecorder.csproj package Microsoft.Extensions.Options
dotnet add src/DotNet.MemoryFlightRecorder/DotNet.MemoryFlightRecorder.csproj package Microsoft.Extensions.Logging.Abstractions
dotnet add src/DotNet.MemoryFlightRecorder/DotNet.MemoryFlightRecorder.csproj package Microsoft.Extensions.DependencyInjection.Abstractions
```

## Project File Requirements

Update `src/DotNet.MemoryFlightRecorder/DotNet.MemoryFlightRecorder.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>true</IsPackable>
    <PackageId>DotNet.MemoryFlightRecorder</PackageId>
    <Title>DotNet.MemoryFlightRecorder</Title>
    <Description>In-process memory pressure monitor and pre-OOM dump collector for .NET services.</Description>
    <Authors>YOUR_NAME</Authors>
    <PackageTags>dotnet;diagnostics;memory;dump;oom;gc;aspnetcore;worker-service</PackageTags>
  </PropertyGroup>
</Project>
```

Replace `YOUR_NAME` with the repository owner's name or GitHub username.

## Public API Shape

The library should be enabled like this:

```csharp
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

The extension method must register:

- `MemoryFlightRecorderOptions`
- `MemoryFlightRecorderOptionsValidator`
- `IMemorySnapshotProvider`
- `IMemoryLimitProvider`
- `IMemoryPressureEvaluator`
- `IDumpWriter`
- `DumpRetentionPolicy`
- `MemoryFlightRecorderService` as a hosted service

## Options

Create `Options/MemoryFlightRecorderOptions.cs`:

```csharp
using Microsoft.Diagnostics.NETCore.Client;

namespace DotNet.MemoryFlightRecorder.Options;

public sealed class MemoryFlightRecorderOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);
    public double WarningThreshold { get; set; } = 0.70;
    public double CriticalThreshold { get; set; } = 0.85;
    public TimeSpan DumpCooldown { get; set; } = TimeSpan.FromMinutes(15);
    public string DumpDirectory { get; set; } = "./memory-dumps";
    public DumpType DumpType { get; set; } = DumpType.WithHeap;
    public bool EnableDumpGeneration { get; set; } = true;
    public bool WriteSnapshotJson { get; set; } = true;
    public bool LogDumpGeneration { get; set; } = true;
    public int MaxDumpCount { get; set; } = 3;
    public long MaxDumpDirectorySizeBytes { get; set; } = 2L * 1024 * 1024 * 1024;
    public long MinFreeDiskBytesBeforeDump { get; set; } = 512L * 1024 * 1024;
    public long? MemoryLimitBytes { get; set; }
}
```

Validation rules:

- `PollInterval` must be greater than zero.
- `WarningThreshold` must be greater than `0` and less than `1`.
- `CriticalThreshold` must be greater than `0` and less than `1`.
- `CriticalThreshold` must be greater than `WarningThreshold`.
- `DumpCooldown` must not be negative.
- `DumpDirectory` must not be empty.
- `MaxDumpCount` must be at least `1`.
- `MaxDumpDirectorySizeBytes` must be greater than zero.
- `MinFreeDiskBytesBeforeDump` must not be negative.
- `MemoryLimitBytes`, when supplied, must be greater than zero.

Validation must be implemented through `IValidateOptions<MemoryFlightRecorderOptions>`. Do not manually create an options instance and invoke the user callback during service registration.

Create `Options/MemoryFlightRecorderOptionsValidator.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace DotNet.MemoryFlightRecorder.Options;

public sealed class MemoryFlightRecorderOptionsValidator : IValidateOptions<MemoryFlightRecorderOptions>
{
    public ValidateOptionsResult Validate(string? name, MemoryFlightRecorderOptions options)
    {
        var failures = new List<string>();

        if (options.PollInterval <= TimeSpan.Zero)
            failures.Add("PollInterval must be greater than zero.");

        if (options.WarningThreshold is <= 0 or >= 1)
            failures.Add("WarningThreshold must be greater than 0 and less than 1.");

        if (options.CriticalThreshold is <= 0 or >= 1)
            failures.Add("CriticalThreshold must be greater than 0 and less than 1.");

        if (options.CriticalThreshold <= options.WarningThreshold)
            failures.Add("CriticalThreshold must be greater than WarningThreshold.");

        if (options.DumpCooldown < TimeSpan.Zero)
            failures.Add("DumpCooldown must not be negative.");

        if (string.IsNullOrWhiteSpace(options.DumpDirectory))
            failures.Add("DumpDirectory must not be empty.");

        if (options.MaxDumpCount < 1)
            failures.Add("MaxDumpCount must be at least 1.");

        if (options.MaxDumpDirectorySizeBytes <= 0)
            failures.Add("MaxDumpDirectorySizeBytes must be greater than zero.");

        if (options.MinFreeDiskBytesBeforeDump < 0)
            failures.Add("MinFreeDiskBytesBeforeDump must not be negative.");

        if (options.MemoryLimitBytes is <= 0)
            failures.Add("MemoryLimitBytes, when supplied, must be greater than zero.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
```

## Memory Snapshot Model

Create `Monitoring/MemorySnapshot.cs`:

```csharp
namespace DotNet.MemoryFlightRecorder.Monitoring;

public sealed record MemorySnapshot(
    DateTimeOffset TimestampUtc,
    int ProcessId,
    string ProcessName,
    long ManagedHeapBytes,
    long ManagedCommittedBytes,
    long FragmentedBytes,
    long TotalAvailableMemoryBytes,
    long MemoryLoadBytes,
    long HighMemoryLoadThresholdBytes,
    double GcPauseTimePercentage,
    long ProcessWorkingSetBytes,
    long ProcessPrivateMemoryBytes,
    long ApproximateUnmanagedBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    int ThreadCount
);
```

Important notes:

- `MemoryLoadBytes` is captured for context only.
- Do not use `MemoryLoadBytes` as the primary process memory ratio.
- `ApproximateUnmanagedBytes` is a heuristic, not a precise native memory measurement.

Recommended heuristic:

```csharp
var approximateUnmanagedBytes = Math.Max(0, process.WorkingSet64 - gcInfo.TotalCommittedBytes);
```

## Snapshot Provider

Create `Monitoring/IMemorySnapshotProvider.cs`:

```csharp
namespace DotNet.MemoryFlightRecorder.Monitoring;

public interface IMemorySnapshotProvider
{
    MemorySnapshot Capture();
}
```

Create `Monitoring/ProcessMemorySnapshotProvider.cs`:

```csharp
using System.Diagnostics;

namespace DotNet.MemoryFlightRecorder.Monitoring;

public sealed class ProcessMemorySnapshotProvider : IMemorySnapshotProvider
{
    private readonly Process _process = Process.GetCurrentProcess();

    public MemorySnapshot Capture()
    {
        _process.Refresh();
        var gcInfo = GC.GetGCMemoryInfo();
        var approximateUnmanagedBytes = Math.Max(0, _process.WorkingSet64 - gcInfo.TotalCommittedBytes);

        return new MemorySnapshot(
            TimestampUtc: DateTimeOffset.UtcNow,
            ProcessId: Environment.ProcessId,
            ProcessName: _process.ProcessName,
            ManagedHeapBytes: gcInfo.HeapSizeBytes,
            ManagedCommittedBytes: gcInfo.TotalCommittedBytes,
            FragmentedBytes: gcInfo.FragmentedBytes,
            TotalAvailableMemoryBytes: gcInfo.TotalAvailableMemoryBytes,
            MemoryLoadBytes: gcInfo.MemoryLoadBytes,
            HighMemoryLoadThresholdBytes: gcInfo.HighMemoryLoadThresholdBytes,
            GcPauseTimePercentage: gcInfo.PauseTimePercentage,
            ProcessWorkingSetBytes: _process.WorkingSet64,
            ProcessPrivateMemoryBytes: _process.PrivateMemorySize64,
            ApproximateUnmanagedBytes: approximateUnmanagedBytes,
            Gen0Collections: GC.CollectionCount(0),
            Gen1Collections: GC.CollectionCount(1),
            Gen2Collections: GC.CollectionCount(2),
            ThreadCount: _process.Threads.Count);
    }
}
```

## Memory Limit Provider

Create `Limits/MemoryLimit.cs`:

```csharp
namespace DotNet.MemoryFlightRecorder.Limits;

public sealed record MemoryLimit(long? Bytes, string Source)
{
    public bool IsAvailable => Bytes is > 0;

    public static MemoryLimit Available(long bytes, string source) => new(bytes, source);

    public static MemoryLimit Unavailable(string source) => new(null, source);
}
```

Create `Limits/IMemoryLimitProvider.cs`:

```csharp
namespace DotNet.MemoryFlightRecorder.Limits;

public interface IMemoryLimitProvider
{
    MemoryLimit GetMemoryLimit();
}
```

Create `Limits/DefaultMemoryLimitProvider.cs`:

```csharp
using DotNet.MemoryFlightRecorder.Options;
using Microsoft.Extensions.Options;

namespace DotNet.MemoryFlightRecorder.Limits;

public sealed class DefaultMemoryLimitProvider : IMemoryLimitProvider
{
    private readonly IOptions<MemoryFlightRecorderOptions> _options;

    public DefaultMemoryLimitProvider(IOptions<MemoryFlightRecorderOptions> options)
    {
        _options = options;
    }

    public MemoryLimit GetMemoryLimit()
    {
        if (_options.Value.MemoryLimitBytes is long explicitLimit)
        {
            return explicitLimit > 0
                ? MemoryLimit.Available(explicitLimit, "Options.MemoryLimitBytes")
                : MemoryLimit.Unavailable("Options.MemoryLimitBytes was configured with an invalid non-positive value.");
        }

        var gcInfo = GC.GetGCMemoryInfo();

        if (gcInfo.TotalAvailableMemoryBytes > 0)
        {
            return MemoryLimit.Available(
                gcInfo.TotalAvailableMemoryBytes,
                "GC.GetGCMemoryInfo().TotalAvailableMemoryBytes");
        }

        return MemoryLimit.Unavailable(
            "No usable memory limit was reported by GC.GetGCMemoryInfo().TotalAvailableMemoryBytes. Configure MemoryLimitBytes to enable ratio-based pressure decisions.");
    }
}
```

Keep this simple for the MVP. Add explicit cgroup parsing only in a later version if needed.

Critical rule: do not coerce an unavailable or invalid limit into `1` byte. If `TotalAvailableMemoryBytes` is not usable and no explicit `MemoryLimitBytes` was configured, return `MemoryLimit.Unavailable(...)` and let the evaluator skip ratio-based warning/critical decisions. Do not fall back to `HighMemoryLoadThresholdBytes` as a memory limit in the MVP.

## Pressure Evaluation

Create `Evaluation/MemoryPressureLevel.cs`:

```csharp
namespace DotNet.MemoryFlightRecorder.Evaluation;

public enum MemoryPressureLevel
{
    Normal = 0,
    Warning = 1,
    Critical = 2
}
```

Create `Evaluation/MemoryPressureDecision.cs`:

```csharp
namespace DotNet.MemoryFlightRecorder.Evaluation;

public sealed record MemoryPressureDecision(
    MemoryPressureLevel Level,
    string Reason,
    long? LimitBytes,
    string LimitSource,
    bool IsLimitAvailable,
    double? WorkingSetRatio,
    double? PrivateMemoryRatio,
    double? ManagedCommittedRatio,
    double? ManagedHeapRatio
);
```

Create `Evaluation/IMemoryPressureEvaluator.cs`:

```csharp
using DotNet.MemoryFlightRecorder.Monitoring;

namespace DotNet.MemoryFlightRecorder.Evaluation;

public interface IMemoryPressureEvaluator
{
    MemoryPressureDecision Evaluate(MemorySnapshot snapshot);
}
```

Create `Evaluation/DefaultMemoryPressureEvaluator.cs`:

```csharp
using DotNet.MemoryFlightRecorder.Limits;
using DotNet.MemoryFlightRecorder.Monitoring;
using DotNet.MemoryFlightRecorder.Options;
using Microsoft.Extensions.Options;

namespace DotNet.MemoryFlightRecorder.Evaluation;

public sealed class DefaultMemoryPressureEvaluator : IMemoryPressureEvaluator
{
    private readonly IOptions<MemoryFlightRecorderOptions> _options;
    private readonly IMemoryLimitProvider _memoryLimitProvider;

    public DefaultMemoryPressureEvaluator(
        IOptions<MemoryFlightRecorderOptions> options,
        IMemoryLimitProvider memoryLimitProvider)
    {
        _options = options;
        _memoryLimitProvider = memoryLimitProvider;
    }

    public MemoryPressureDecision Evaluate(MemorySnapshot snapshot)
    {
        var options = _options.Value;
        var limit = _memoryLimitProvider.GetMemoryLimit();

        if (!limit.IsAvailable || limit.Bytes is not long limitBytes)
        {
            return new MemoryPressureDecision(
                MemoryPressureLevel.Normal,
                "Memory limit is unavailable; ratio-based pressure evaluation was skipped.",
                null,
                limit.Source,
                false,
                null,
                null,
                null,
                null);
        }

        var workingSetRatio = Ratio(snapshot.ProcessWorkingSetBytes, limitBytes);
        var privateMemoryRatio = Ratio(snapshot.ProcessPrivateMemoryBytes, limitBytes);
        var managedCommittedRatio = Ratio(snapshot.ManagedCommittedBytes, limitBytes);
        var managedHeapRatio = Ratio(snapshot.ManagedHeapBytes, limitBytes);

        var strongestRatio = Math.Max(
            Math.Max(workingSetRatio, privateMemoryRatio),
            Math.Max(managedCommittedRatio, managedHeapRatio));

        if (strongestRatio >= options.CriticalThreshold)
        {
            return new MemoryPressureDecision(
                MemoryPressureLevel.Critical,
                $"Memory pressure crossed critical threshold {options.CriticalThreshold:P0}.",
                limitBytes,
                limit.Source,
                true,
                workingSetRatio,
                privateMemoryRatio,
                managedCommittedRatio,
                managedHeapRatio);
        }

        if (strongestRatio >= options.WarningThreshold)
        {
            return new MemoryPressureDecision(
                MemoryPressureLevel.Warning,
                $"Memory pressure crossed warning threshold {options.WarningThreshold:P0}.",
                limitBytes,
                limit.Source,
                true,
                workingSetRatio,
                privateMemoryRatio,
                managedCommittedRatio,
                managedHeapRatio);
        }

        return new MemoryPressureDecision(
            MemoryPressureLevel.Normal,
            "Memory pressure is normal.",
            limitBytes,
            limit.Source,
            true,
            workingSetRatio,
            privateMemoryRatio,
            managedCommittedRatio,
            managedHeapRatio);
    }

    private static double Ratio(long value, long limitBytes)
    {
        if (limitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limitBytes), "limitBytes must be greater than zero.");
        }

        return Math.Clamp((double)value / limitBytes, 0, 1);
    }
}
```

MVP decision rule:

- If the memory limit is unavailable, return `Normal` with `IsLimitAvailable = false` and do not attempt ratio-based dump decisions.
- Critical if any important ratio crosses `CriticalThreshold`.
- Warning if any important ratio crosses `WarningThreshold`.
- Normal otherwise.

Do not add growth velocity in the first pass. Add it only after the base library works and tests pass.

## Dump File Naming

Create `Dumping/DumpFileNamer.cs`:

```csharp
namespace DotNet.MemoryFlightRecorder.Dumping;

public static class DumpFileNamer
{
    public static string CreateDumpPath(string dumpDirectory, int processId, DateTimeOffset timestampUtc)
    {
        var safeTimestamp = timestampUtc.UtcDateTime.ToString("yyyyMMdd_HHmmss_fff");
        return Path.Combine(dumpDirectory, $"memory_{safeTimestamp}_{processId}.dmp");
    }
}
```

## Dump Writer

Create `Dumping/IDumpWriter.cs`:

```csharp
using DotNet.MemoryFlightRecorder.Evaluation;
using DotNet.MemoryFlightRecorder.Monitoring;

namespace DotNet.MemoryFlightRecorder.Dumping;

public interface IDumpWriter
{
    Task<bool> TryWriteDumpAsync(
        MemorySnapshot snapshot,
        MemoryPressureDecision decision,
        CancellationToken cancellationToken);
}
```

`TryWriteDumpAsync` returns `true` only when a `.dmp` file was written successfully. It returns `false` when dump generation is disabled, diagnostics are unavailable, disk space is too low, directory preparation fails, or dump generation fails.

Create `Dumping/DiagnosticsDumpWriter.cs`:

```csharp
using System.Text.Json;
using DotNet.MemoryFlightRecorder.Evaluation;
using DotNet.MemoryFlightRecorder.Monitoring;
using DotNet.MemoryFlightRecorder.Options;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.MemoryFlightRecorder.Dumping;

public sealed class DiagnosticsDumpWriter : IDumpWriter
{
    private readonly IOptions<MemoryFlightRecorderOptions> _options;
    private readonly ILogger<DiagnosticsDumpWriter> _logger;

    public DiagnosticsDumpWriter(
        IOptions<MemoryFlightRecorderOptions> options,
        ILogger<DiagnosticsDumpWriter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<bool> TryWriteDumpAsync(
        MemorySnapshot snapshot,
        MemoryPressureDecision decision,
        CancellationToken cancellationToken)
    {
        var options = _options.Value;

        if (!options.EnableDumpGeneration)
        {
            _logger.LogWarning("Memory dump generation is disabled. Reason={Reason}", decision.Reason);
            return false;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var dumpPath = DumpFileNamer.CreateDumpPath(options.DumpDirectory, Environment.ProcessId, timestamp);
        var snapshotPath = Path.ChangeExtension(dumpPath, ".snapshot.json");

        try
        {
            Directory.CreateDirectory(options.DumpDirectory);

            var root = Path.GetPathRoot(Path.GetFullPath(options.DumpDirectory));
            if (string.IsNullOrWhiteSpace(root))
            {
                _logger.LogError(
                    "Skipping memory dump because the dump directory root could not be determined. DumpDirectory={DumpDirectory}",
                    options.DumpDirectory);
                return false;
            }

            var drive = new DriveInfo(root);
            if (drive.AvailableFreeSpace < options.MinFreeDiskBytesBeforeDump)
            {
                _logger.LogError(
                    "Skipping memory dump because free disk space is too low. Available={Available:n0}, Required={Required:n0}",
                    drive.AvailableFreeSpace,
                    options.MinFreeDiskBytesBeforeDump);
                return false;
            }

            var client = new DiagnosticsClient(Environment.ProcessId);

            await client.WriteDumpAsync(
                options.DumpType,
                dumpPath,
                options.LogDumpGeneration,
                cancellationToken);
        }
        catch (DiagnosticsClientException ex)
        {
            _logger.LogError(
                ex,
                "Failed to write memory dump through .NET diagnostics. Check DOTNET_EnableDiagnostics, DOTNET_EnableDiagnostics_IPC, runtime compatibility, and dump directory permissions.");
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to write memory dump to {DumpPath}", dumpPath);
            return false;
        }

        if (options.WriteSnapshotJson)
        {
            try
            {
                var payload = new
                {
                    Snapshot = snapshot,
                    Decision = decision,
                    Options = new
                    {
                        options.WarningThreshold,
                        options.CriticalThreshold,
                        options.DumpType,
                        options.DumpCooldown
                    }
                };

                await File.WriteAllTextAsync(
                    snapshotPath,
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "Memory dump was written, but snapshot JSON failed. SnapshotPath={SnapshotPath}",
                    snapshotPath);
            }
        }

        _logger.LogCritical(
            "Memory dump written. DumpPath={DumpPath}, SnapshotPath={SnapshotPath}, Reason={Reason}",
            dumpPath,
            options.WriteSnapshotJson ? snapshotPath : "disabled",
            decision.Reason);

        return true;
    }
}
```

Important behavior:

- Write the `.dmp` first.
- Write the `.snapshot.json` after the dump succeeds.
- A snapshot failure must not turn a successful dump into a failed dump.
- A failed dump attempt must return `false` and must not arm cooldown.
- Directory creation and free-space checks must be inside the same failure-handling path as dump creation.

## Dump Retention Policy

Create `Dumping/DumpRetentionPolicy.cs`:

```csharp
using DotNet.MemoryFlightRecorder.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.MemoryFlightRecorder.Dumping;

public sealed class DumpRetentionPolicy
{
    private readonly IOptions<MemoryFlightRecorderOptions> _options;
    private readonly ILogger<DumpRetentionPolicy> _logger;

    public DumpRetentionPolicy(
        IOptions<MemoryFlightRecorderOptions> options,
        ILogger<DumpRetentionPolicy> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void Apply()
    {
        var options = _options.Value;
        if (!Directory.Exists(options.DumpDirectory))
        {
            return;
        }

        var artifacts = LoadArtifacts(options.DumpDirectory);
        DeleteOrphanSnapshots(options.DumpDirectory, artifacts);

        DeleteByCount(artifacts, options.MaxDumpCount);

        artifacts = LoadArtifacts(options.DumpDirectory);
        DeleteByDirectorySize(artifacts, options.MaxDumpDirectorySizeBytes);
    }

    private static List<DumpArtifact> LoadArtifacts(string directory)
    {
        return Directory
            .EnumerateFiles(directory, "memory_*.dmp")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .Select(DumpArtifact.FromDumpFile)
            .ToList();
    }

    private void DeleteByCount(List<DumpArtifact> artifacts, int maxDumpCount)
    {
        foreach (var artifact in artifacts.Skip(maxDumpCount))
        {
            DeleteArtifact(artifact);
        }
    }

    private void DeleteByDirectorySize(List<DumpArtifact> artifacts, long maxBytes)
    {
        var oldestFirst = artifacts
            .OrderBy(artifact => artifact.CreationTimeUtc)
            .ToList();

        long size = oldestFirst.Sum(artifact => artifact.SizeBytes);

        foreach (var artifact in oldestFirst.ToList())
        {
            if (size <= maxBytes || oldestFirst.Count <= 1)
            {
                break;
            }

            size -= artifact.SizeBytes;
            DeleteArtifact(artifact);
            oldestFirst.Remove(artifact);
        }
    }

    private void DeleteOrphanSnapshots(string directory, IReadOnlyCollection<DumpArtifact> artifacts)
    {
        var knownSnapshotPaths = artifacts
            .Select(artifact => artifact.ExpectedSnapshotPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshotPath in Directory.EnumerateFiles(directory, "memory_*.snapshot.json"))
        {
            if (!knownSnapshotPaths.Contains(snapshotPath))
            {
                DeleteQuietly(snapshotPath);
            }
        }
    }

    private void DeleteArtifact(DumpArtifact artifact)
    {
        DeleteQuietly(artifact.DumpPath);
        DeleteQuietly(artifact.ExpectedSnapshotPath);
    }

    private void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete old memory diagnostic file {Path}", path);
        }
    }

    private sealed record DumpArtifact(
        string DumpPath,
        string ExpectedSnapshotPath,
        DateTime CreationTimeUtc,
        long SizeBytes)
    {
        public static DumpArtifact FromDumpFile(FileInfo dumpFile)
        {
            var snapshotPath = Path.ChangeExtension(dumpFile.FullName, ".snapshot.json");
            var snapshotSize = File.Exists(snapshotPath)
                ? new FileInfo(snapshotPath).Length
                : 0;

            return new DumpArtifact(
                DumpPath: dumpFile.FullName,
                ExpectedSnapshotPath: snapshotPath,
                CreationTimeUtc: dumpFile.CreationTimeUtc,
                SizeBytes: dumpFile.Length + snapshotSize);
        }
    }
}
```

Retention rule: a dump and its matching `.snapshot.json` are one logical artifact. Count-based and size-based retention must delete them together. Standalone orphan snapshots may be deleted because they are not useful without the corresponding dump.

## Hosted Service

Create `Monitoring/MemoryFlightRecorderService.cs`:

```csharp
using DotNet.MemoryFlightRecorder.Dumping;
using DotNet.MemoryFlightRecorder.Evaluation;
using DotNet.MemoryFlightRecorder.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.MemoryFlightRecorder.Monitoring;

public sealed class MemoryFlightRecorderService : BackgroundService
{
    private readonly IOptions<MemoryFlightRecorderOptions> _options;
    private readonly IMemorySnapshotProvider _snapshotProvider;
    private readonly IMemoryPressureEvaluator _evaluator;
    private readonly IDumpWriter _dumpWriter;
    private readonly DumpRetentionPolicy _retentionPolicy;
    private readonly ILogger<MemoryFlightRecorderService> _logger;
    private readonly SemaphoreSlim _dumpLock = new(1, 1);

    private DateTimeOffset _lastSuccessfulDumpAtUtc = DateTimeOffset.MinValue;
    private bool _loggedUnavailableLimit;

    public MemoryFlightRecorderService(
        IOptions<MemoryFlightRecorderOptions> options,
        IMemorySnapshotProvider snapshotProvider,
        IMemoryPressureEvaluator evaluator,
        IDumpWriter dumpWriter,
        DumpRetentionPolicy retentionPolicy,
        ILogger<MemoryFlightRecorderService> logger)
    {
        _options = options;
        _snapshotProvider = snapshotProvider;
        _evaluator = evaluator;
        _dumpWriter = dumpWriter;
        _retentionPolicy = retentionPolicy;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Value.PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var snapshot = _snapshotProvider.Capture();
                var decision = _evaluator.Evaluate(snapshot);

                LogDecision(snapshot, decision);

                if (decision.Level == MemoryPressureLevel.Critical)
                {
                    await TryWriteDumpAsync(snapshot, decision, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private void LogDecision(MemorySnapshot snapshot, MemoryPressureDecision decision)
    {
        if (!decision.IsLimitAvailable)
        {
            if (!_loggedUnavailableLimit)
            {
                _loggedUnavailableLimit = true;
                _logger.LogWarning(
                    "Memory limit unavailable; ratio-based memory pressure decisions are disabled. WorkingSet={WorkingSet:n0}, Private={Private:n0}, ManagedCommitted={ManagedCommitted:n0}, LimitSource={LimitSource}",
                    snapshot.ProcessWorkingSetBytes,
                    snapshot.ProcessPrivateMemoryBytes,
                    snapshot.ManagedCommittedBytes,
                    decision.LimitSource);
            }

            return;
        }

        if (decision.Level == MemoryPressureLevel.Warning)
        {
            _logger.LogWarning(
                "High memory pressure. WorkingSet={WorkingSet:n0}, Private={Private:n0}, ManagedHeap={ManagedHeap:n0}, ManagedCommitted={ManagedCommitted:n0}, Fragmented={Fragmented:n0}, Limit={Limit:n0} ({LimitSource}), WorkingSetRatio={WorkingSetRatio:P1}, ManagedCommittedRatio={ManagedCommittedRatio:P1}, GcPause={GcPause:F2}%",
                snapshot.ProcessWorkingSetBytes,
                snapshot.ProcessPrivateMemoryBytes,
                snapshot.ManagedHeapBytes,
                snapshot.ManagedCommittedBytes,
                snapshot.FragmentedBytes,
                decision.LimitBytes,
                decision.LimitSource,
                decision.WorkingSetRatio,
                decision.ManagedCommittedRatio,
                snapshot.GcPauseTimePercentage);
        }
        else if (decision.Level == MemoryPressureLevel.Critical)
        {
            _logger.LogCritical(
                "Critical memory pressure. Reason={Reason}, WorkingSetRatio={WorkingSetRatio:P1}, PrivateMemoryRatio={PrivateMemoryRatio:P1}, ManagedCommittedRatio={ManagedCommittedRatio:P1}, ManagedHeapRatio={ManagedHeapRatio:P1}",
                decision.Reason,
                decision.WorkingSetRatio,
                decision.PrivateMemoryRatio,
                decision.ManagedCommittedRatio,
                decision.ManagedHeapRatio);
        }
    }

    private async Task TryWriteDumpAsync(
        MemorySnapshot snapshot,
        MemoryPressureDecision decision,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var cooldown = _options.Value.DumpCooldown;

        if (now - _lastSuccessfulDumpAtUtc < cooldown)
        {
            _logger.LogWarning(
                "Skipping memory dump because cooldown is active. LastSuccessfulDumpAtUtc={LastSuccessfulDumpAtUtc}, Cooldown={Cooldown}",
                _lastSuccessfulDumpAtUtc,
                cooldown);
            return;
        }

        if (!await _dumpLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("Skipping memory dump because another dump is already in progress.");
            return;
        }

        try
        {
            var dumpWritten = await _dumpWriter.TryWriteDumpAsync(snapshot, decision, cancellationToken);
            if (!dumpWritten)
            {
                return;
            }

            _lastSuccessfulDumpAtUtc = DateTimeOffset.UtcNow;
            _retentionPolicy.Apply();
        }
        finally
        {
            _dumpLock.Release();
        }
    }
}
```

Cooldown rule: `_lastSuccessfulDumpAtUtc` is updated only after `TryWriteDumpAsync` returns `true`. Failed attempts do not arm the successful-dump cooldown.

## Dependency Injection Extension

Create `DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
using DotNet.MemoryFlightRecorder.Dumping;
using DotNet.MemoryFlightRecorder.Evaluation;
using DotNet.MemoryFlightRecorder.Limits;
using DotNet.MemoryFlightRecorder.Monitoring;
using DotNet.MemoryFlightRecorder.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotNet.MemoryFlightRecorder.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMemoryFlightRecorder(
        this IServiceCollection services,
        Action<MemoryFlightRecorderOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<MemoryFlightRecorderOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.ValidateOnStart();

        services.AddSingleton<IValidateOptions<MemoryFlightRecorderOptions>, MemoryFlightRecorderOptionsValidator>();
        services.AddSingleton<IMemorySnapshotProvider, ProcessMemorySnapshotProvider>();
        services.AddSingleton<IMemoryLimitProvider, DefaultMemoryLimitProvider>();
        services.AddSingleton<IMemoryPressureEvaluator, DefaultMemoryPressureEvaluator>();
        services.AddSingleton<IDumpWriter, DiagnosticsDumpWriter>();
        services.AddSingleton<DumpRetentionPolicy>();
        services.AddHostedService<MemoryFlightRecorderService>();

        return services;
    }
}
```

Registration rule: pass `configure` only to `optionsBuilder.Configure(configure)`. Do not manually invoke it for validation. The validator must validate the registered options instance created by the options pipeline.

## Sample App

Use `samples/LeakyApi` as a small ASP.NET Core app.

`Program.cs` should expose:

```http
GET  /memory/status
POST /leak/managed/{megabytes:int}
POST /leak/loh/{megabytes:int}
POST /leak/clear
```

Implementation idea:

```csharp
using DotNet.MemoryFlightRecorder.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5000");

builder.Services.AddMemoryFlightRecorder(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(2);
    options.WarningThreshold = 0.50;
    options.CriticalThreshold = 0.65;
    options.DumpDirectory = Path.Combine(AppContext.BaseDirectory, "memory-dumps");
    options.DumpCooldown = TimeSpan.FromMinutes(2);
    options.MemoryLimitBytes = 512L * 1024 * 1024;
});

builder.Services.AddSingleton<LeakStore>();

var app = builder.Build();

app.MapGet("/memory/status", (LeakStore leakStore) =>
{
    using var process = System.Diagnostics.Process.GetCurrentProcess();
    process.Refresh();
    var gc = GC.GetGCMemoryInfo();

    return Results.Ok(new
    {
        process.Id,
        WorkingSetBytes = process.WorkingSet64,
        PrivateMemoryBytes = process.PrivateMemorySize64,
        ManagedHeapBytes = gc.HeapSizeBytes,
        ManagedCommittedBytes = gc.TotalCommittedBytes,
        gc.FragmentedBytes,
        gc.PauseTimePercentage,
        LeakCount = leakStore.Count
    });
});

app.MapPost("/leak/managed/{megabytes:int}", (int megabytes, LeakStore leakStore) =>
{
    if (megabytes <= 0 || megabytes > 512)
    {
        return Results.BadRequest("megabytes must be between 1 and 512");
    }

    leakStore.Add(new byte[megabytes * 1024 * 1024]);
    return Results.Ok(new { AddedMegabytes = megabytes, LeakCount = leakStore.Count });
});

app.MapPost("/leak/loh/{megabytes:int}", (int megabytes, LeakStore leakStore) =>
{
    if (megabytes <= 0 || megabytes > 512)
    {
        return Results.BadRequest("megabytes must be between 1 and 512");
    }

    for (var i = 0; i < megabytes; i++)
    {
        leakStore.Add(new byte[1024 * 1024]);
    }

    return Results.Ok(new { AddedMegabytes = megabytes, LeakCount = leakStore.Count });
});

app.MapPost("/leak/clear", (LeakStore leakStore) =>
{
    leakStore.Clear();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    return Results.Ok(new { Cleared = true });
});

app.Run();

internal sealed class LeakStore
{
    private readonly object _gate = new();
    private readonly List<byte[]> _buffers = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _buffers.Count;
            }
        }
    }

    public void Add(byte[] buffer)
    {
        lock (_gate)
        {
            _buffers.Add(buffer);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _buffers.Clear();
        }
    }
}
```

Sample rules:

- Bind the sample explicitly to `http://127.0.0.1:5000` so the documented curl commands work unchanged.
- Do not capture a mutable `List<byte[]>` directly in endpoint lambdas.
- Use a sample-only singleton `LeakStore` that serializes `Add`, `Clear`, and `Count` with a lock.
- Keep the leak buffers strongly referenced until `/leak/clear` is called.

## Tests

### Evaluator Tests

Create tests for:

- Returns `Normal` below thresholds.
- Returns `Warning` when working set crosses warning threshold.
- Returns `Critical` when working set crosses critical threshold.
- Returns `Critical` when managed committed memory crosses critical threshold.
- Uses explicit `MemoryLimitBytes` when supplied.
- Returns `Normal` with `IsLimitAvailable = false` when the memory limit provider reports `MemoryLimit.Unavailable(...)`.
- Does not coerce an unavailable or zero memory limit to `1` byte.

Use fake implementations for `IMemoryLimitProvider`.

### Options Tests

Create tests for:

- Invalid `PollInterval` fails validation.
- `CriticalThreshold <= WarningThreshold` fails validation.
- Empty `DumpDirectory` fails validation.
- `MaxDumpCount < 1` fails validation.
- Valid default options pass validation.
- The options `configure` delegate is not invoked manually during registration.
- A non-idempotent `configure` delegate is not executed twice before the host starts.

### Retention Tests

Create tests using a temporary directory:

- Keeps newest `MaxDumpCount` dump files.
- Deletes matching `.snapshot.json` files for deleted dumps.
- Enforces `MaxDumpDirectorySizeBytes` by deleting dump/snapshot pairs together.
- Keeps at least the newest dump artifact even if it alone exceeds `MaxDumpDirectorySizeBytes`.
- Does not delete only one side of a dump/snapshot pair.
- Deletes orphan `.snapshot.json` files that no longer have a matching `.dmp` file.
- Ignores non-matching files.

### Cooldown Tests

Create tests for:

- Failed dump writer result does not update the successful-dump cooldown timestamp.
- Successful dump writer result updates the successful-dump cooldown timestamp.
- Retention policy runs only after a successful dump.

### Sample Concurrency Tests

Create sample-level tests or validation scenarios for:

- Multiple parallel `/leak/managed/{megabytes}` requests do not throw or corrupt leak state.
- Multiple parallel `/leak/loh/{megabytes}` requests do not throw or corrupt leak state.
- `/leak/clear` can run safely while leak requests are in flight.
- `/memory/status` can read the current leak count while other requests are mutating the store.
- The documented curl commands succeed unchanged against `http://127.0.0.1:5000`.

Do not generate real dumps in unit tests. Dump generation should be manually tested with the sample app.

## Manual Test Procedure

Build and test:

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

Run the sample app:

```bash
dotnet run --project samples/LeakyApi/LeakyApi.csproj
```

The sample must listen on `http://127.0.0.1:5000` so the commands below work as written.

Trigger memory growth:

```bash
curl -X POST http://127.0.0.1:5000/leak/managed/100
curl -X POST http://127.0.0.1:5000/leak/managed/100
curl http://127.0.0.1:5000/memory/status
```

Expected result:

- Warning logs appear when memory crosses the warning threshold.
- Critical log appears when memory crosses the critical threshold.
- A dump file appears in the configured dump directory after a successful dump attempt.
- A `.snapshot.json` file appears next to the dump when snapshot writing succeeds.
- Repeated critical checks do not write another successful dump until cooldown expires.
- A failed dump attempt is logged and does not arm the successful-dump cooldown.
- The sample endpoints remain stable under concurrent requests to `/leak/*`, `/memory/status`, and `/leak/clear`.

## README Requirements

Create a concise `README.md` with these sections:

1. Project name and one-line description.
2. Why this exists.
3. Installation placeholder.
4. Basic usage.
5. Configuration options table.
6. What files are generated.
7. Security warning.
8. Limitations.
9. Local demo instructions.

Security warning text:

```text
Memory dumps can contain secrets, tokens, connection strings, request bodies, user data, and other sensitive values. Do not commit dumps to Git. Store them securely and delete them when no longer needed.
```

Limitations section must say:

```text
This library is a pre-OOM diagnostic guard, not a guaranteed OOM catcher. Sudden memory spikes, single huge allocations, disabled diagnostics, disk exhaustion, or immediate container kills can still prevent dump generation.
```

## .gitignore Requirements

Add these entries:

```gitignore
bin/
obj/
.vs/
*.user
*.suo

# Memory diagnostics artifacts
*.dmp
*.dump
*.mdmp
core_*
memory-dumps/
*.snapshot.json
```

## Acceptance Criteria

The MVP is complete when all of these are true:

- `dotnet build` passes.
- `dotnet test` passes.
- The library exposes `AddMemoryFlightRecorder(...)`.
- The hosted service starts automatically when registered.
- The service captures process and GC memory snapshots.
- The evaluator uses working set, private memory, managed heap, and managed committed memory ratios when a valid memory limit is available.
- The evaluator has an explicit memory-limit-unavailable path and never coerces an invalid limit to `1` byte.
- The evaluator does not use `MemoryLoadBytes` as the process usage ratio.
- Warning logs include useful memory details.
- Critical pressure triggers dump generation when a usable memory limit is available.
- Dump generation uses `DiagnosticsClient.WriteDumpAsync(DumpType, string, bool, CancellationToken)` with configurable `DumpType`.
- `IDumpWriter.TryWriteDumpAsync` returns `true` only after a `.dmp` file is written successfully.
- Failed dump attempts do not update the cooldown timestamp.
- Cooldown prevents repeated successful dumps.
- Retention prevents unlimited dump accumulation.
- Retention deletes dump/snapshot pairs together.
- Retention does not delete the newest artifact immediately after capture, even if that single artifact exceeds the directory-size target.
- Diagnostics-disabled failures are logged clearly.
- Dumps are ignored by Git.
- README warns that dumps may contain secrets.
- The options callback is registered once and is not manually invoked during dependency-injection registration.
- Sample app can reproduce warning and critical behavior.

## Future Enhancements

Add these only after the MVP is stable:

1. Growth velocity detection.
2. Better native/unmanaged memory heuristics.
3. Optional compression.
4. Optional dump upload to S3 or Azure Blob Storage.
5. Optional `DumpType.Triage` mode for lower-risk collection.
6. OpenTelemetry metrics.
7. Prometheus endpoint sample.
8. Container-specific memory limit parser.
9. GitHub Actions CI.
10. NuGet publish workflow.

## Implementation Order for Codex

Follow this exact sequence:

1. Create solution and project structure.
2. Add package references.
3. Implement options and `IValidateOptions` validation without manually invoking the user callback.
4. Implement snapshot model and provider.
5. Implement memory limit provider with an explicit unavailable path.
6. Implement pressure evaluator.
7. Implement dump naming.
8. Implement dump writer with `TryWriteDumpAsync` returning a success flag.
9. Implement retention policy that deletes dump/snapshot pairs together.
10. Implement hosted service and successful-dump cooldown.
11. Implement dependency injection extension.
12. Implement unit tests for options, evaluator, retention, and cooldown behavior.
13. Implement sample app.
14. Add README.
15. Add `.gitignore` dump exclusions.
16. Run `dotnet build` and `dotnet test`.
17. Fix compile/test failures.
18. Do one manual sample test.

## Final Quality Bar

Keep the library boring, predictable, and production-safe.

The best first release is not the most feature-rich version. The best first release is one that:

- Runs quietly when memory is normal.
- Logs useful information when memory is high.
- Writes one useful dump when memory is critical and dump generation succeeds.
- Does not flood disk.
- Does not suppress retries after failed dump attempts.
- Does not pretend it can catch every OOM.
- Explains its limitations clearly.

## API Signature Check

Use this documented overload in the dump writer:

```csharp
Task WriteDumpAsync(DumpType dumpType, string dumpPath, bool logDumpGeneration, CancellationToken token)
```

This documented overload is also valid if the implementation chooses explicit flags instead of a Boolean logging parameter:

```csharp
Task WriteDumpAsync(DumpType dumpType, string dumpPath, WriteDumpFlags flags, CancellationToken token)
```

`DumpType.WithHeap` is a valid enum value. Diagnostics-client failures should be handled as `DiagnosticsClientException` or a derived type such as `ServerNotAvailableException` or `ServerErrorException`. Do not invent overloads or rely on APIs not listed in the official `Microsoft.Diagnostics.NETCore.Client` documentation.

## Reference Links

Use these references during implementation:

- `GCMemoryInfo`: https://learn.microsoft.com/en-us/dotnet/api/system.gcmemoryinfo
- `Microsoft.Diagnostics.NETCore.Client`: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/microsoft-diagnostics-netcore-client
- `dotnet-dump`: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump
- .NET diagnostics in containers: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostics-in-containers
- .NET environment variables: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables
