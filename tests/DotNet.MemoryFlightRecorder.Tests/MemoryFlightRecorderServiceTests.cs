using DotNet.MemoryFlightRecorder.Dumping;
using DotNet.MemoryFlightRecorder.Evaluation;
using DotNet.MemoryFlightRecorder.Monitoring;
using DotNet.MemoryFlightRecorder.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNet.MemoryFlightRecorder.Tests;

public sealed class MemoryFlightRecorderServiceTests : IDisposable
{
    private readonly string _directory;

    public MemoryFlightRecorderServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "MemoryFlightRecorderServiceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task FailedDumpWriterDoesNotArmCooldown()
    {
        var dumpWriter = new FakeDumpWriter(result: false);
        CreateArtifact("memory_20240101_000000_1", creationOrder: 1);
        CreateArtifact("memory_20240101_000001_1", creationOrder: 2);
        using var service = CreateService(dumpWriter, pollInterval: TimeSpan.FromMilliseconds(20));

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(120);
        await service.StopAsync(CancellationToken.None);

        Assert.True(dumpWriter.CallCount >= 2);
        Assert.True(File.Exists(Path.Combine(_directory, "memory_20240101_000000_1.dmp")));
        Assert.True(File.Exists(Path.Combine(_directory, "memory_20240101_000001_1.dmp")));
    }

    [Fact]
    public async Task SuccessfulDumpWriterArmsCooldownAndRunsRetention()
    {
        var dumpWriter = new FakeDumpWriter(result: true);
        CreateArtifact("memory_20240101_000000_1", creationOrder: 1);
        CreateArtifact("memory_20240101_000001_1", creationOrder: 2);
        using var service = CreateService(
            dumpWriter,
            pollInterval: TimeSpan.FromMilliseconds(20),
            dumpCooldown: TimeSpan.FromSeconds(10));

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(120);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, dumpWriter.CallCount);
        Assert.False(File.Exists(Path.Combine(_directory, "memory_20240101_000000_1.dmp")));
        Assert.False(File.Exists(Path.Combine(_directory, "memory_20240101_000000_1.snapshot.json")));
        Assert.True(File.Exists(Path.Combine(_directory, "memory_20240101_000001_1.dmp")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private MemoryFlightRecorderService CreateService(
        IDumpWriter dumpWriter,
        TimeSpan pollInterval,
        TimeSpan? dumpCooldown = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new MemoryFlightRecorderOptions
        {
            PollInterval = pollInterval,
            DumpCooldown = dumpCooldown ?? TimeSpan.FromSeconds(5),
            DumpDirectory = _directory,
            MaxDumpCount = 1,
            MaxDumpDirectorySizeBytes = 1_000
        });

        var retentionPolicy = new DumpRetentionPolicy(options, NullLogger<DumpRetentionPolicy>.Instance);

        return new MemoryFlightRecorderService(
            options,
            new FakeMemorySnapshotProvider(),
            new FakeCriticalEvaluator(),
            dumpWriter,
            retentionPolicy,
            NullLogger<MemoryFlightRecorderService>.Instance);
    }

    private void CreateArtifact(string baseName, int creationOrder)
    {
        var dumpPath = Path.Combine(_directory, $"{baseName}.dmp");
        var snapshotPath = Path.Combine(_directory, $"{baseName}.snapshot.json");
        File.WriteAllBytes(dumpPath, new byte[10]);
        File.WriteAllBytes(snapshotPath, new byte[10]);

        var timestamp = new DateTime(2024, 1, 1, 0, 0, creationOrder, DateTimeKind.Utc);
        File.SetCreationTimeUtc(dumpPath, timestamp);
        File.SetCreationTimeUtc(snapshotPath, timestamp);
    }

    private sealed class FakeMemorySnapshotProvider : IMemorySnapshotProvider
    {
        public MemorySnapshot Capture()
        {
            return new MemorySnapshot(
                DateTimeOffset.UtcNow,
                ProcessId: 42,
                ProcessName: "test",
                ManagedHeapBytes: 100,
                ManagedCommittedBytes: 100,
                FragmentedBytes: 0,
                TotalAvailableMemoryBytes: 100,
                MemoryLoadBytes: 0,
                HighMemoryLoadThresholdBytes: 0,
                GcPauseTimePercentage: 0,
                ProcessWorkingSetBytes: 100,
                ProcessPrivateMemoryBytes: 100,
                ApproximateUnmanagedBytes: 0,
                Gen0Collections: 0,
                Gen1Collections: 0,
                Gen2Collections: 0,
                ThreadCount: 0);
        }
    }

    private sealed class FakeCriticalEvaluator : IMemoryPressureEvaluator
    {
        public MemoryPressureDecision Evaluate(MemorySnapshot snapshot)
        {
            return new MemoryPressureDecision(
                MemoryPressureLevel.Critical,
                "critical",
                100,
                "test",
                true,
                1,
                1,
                1,
                1);
        }
    }

    private sealed class FakeDumpWriter : IDumpWriter
    {
        private readonly bool _result;

        public FakeDumpWriter(bool result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<bool> TryWriteDumpAsync(
            MemorySnapshot snapshot,
            MemoryPressureDecision decision,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }
}
