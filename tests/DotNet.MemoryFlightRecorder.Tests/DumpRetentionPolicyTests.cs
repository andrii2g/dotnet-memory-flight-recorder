using DotNet.MemoryFlightRecorder.Dumping;
using DotNet.MemoryFlightRecorder.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNet.MemoryFlightRecorder.Tests;

public sealed class DumpRetentionPolicyTests : IDisposable
{
    private readonly string _directory;

    public DumpRetentionPolicyTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "MemoryFlightRecorderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void KeepsNewestMaxDumpCountDumpFiles()
    {
        CreateArtifact("memory_20240101_000000_1", dumpSize: 10, snapshotSize: 5, creationOrder: 1);
        CreateArtifact("memory_20240101_000001_1", dumpSize: 10, snapshotSize: 5, creationOrder: 2);
        CreateArtifact("memory_20240101_000002_1", dumpSize: 10, snapshotSize: 5, creationOrder: 3);

        CreatePolicy(maxDumpCount: 2, maxDirectoryBytes: 1_000).Apply();

        Assert.False(File.Exists(Path.Combine(_directory, "memory_20240101_000000_1.dmp")));
        Assert.False(File.Exists(Path.Combine(_directory, "memory_20240101_000000_1.snapshot.json")));
        Assert.True(File.Exists(Path.Combine(_directory, "memory_20240101_000001_1.dmp")));
        Assert.True(File.Exists(Path.Combine(_directory, "memory_20240101_000002_1.dmp")));
    }

    [Fact]
    public void EnforcesDirectorySizeByDeletingPairsTogether()
    {
        CreateArtifact("memory_20240101_000000_1", dumpSize: 80, snapshotSize: 10, creationOrder: 1);
        CreateArtifact("memory_20240101_000001_1", dumpSize: 80, snapshotSize: 10, creationOrder: 2);
        CreateArtifact("memory_20240101_000002_1", dumpSize: 80, snapshotSize: 10, creationOrder: 3);

        CreatePolicy(maxDumpCount: 5, maxDirectoryBytes: 150).Apply();

        Assert.False(File.Exists(Path.Combine(_directory, "memory_20240101_000000_1.dmp")));
        Assert.False(File.Exists(Path.Combine(_directory, "memory_20240101_000000_1.snapshot.json")));
        Assert.False(File.Exists(Path.Combine(_directory, "memory_20240101_000001_1.dmp")));
        Assert.False(File.Exists(Path.Combine(_directory, "memory_20240101_000001_1.snapshot.json")));
        Assert.True(File.Exists(Path.Combine(_directory, "memory_20240101_000002_1.dmp")));
        Assert.True(File.Exists(Path.Combine(_directory, "memory_20240101_000002_1.snapshot.json")));
    }

    [Fact]
    public void KeepsNewestArtifactEvenIfItAloneExceedsDirectoryLimit()
    {
        CreateArtifact("memory_20240101_000000_1", dumpSize: 10, snapshotSize: 10, creationOrder: 1);
        CreateArtifact("memory_20240101_000001_1", dumpSize: 120, snapshotSize: 20, creationOrder: 2);

        CreatePolicy(maxDumpCount: 5, maxDirectoryBytes: 50).Apply();

        Assert.False(File.Exists(Path.Combine(_directory, "memory_20240101_000000_1.dmp")));
        Assert.True(File.Exists(Path.Combine(_directory, "memory_20240101_000001_1.dmp")));
        Assert.True(File.Exists(Path.Combine(_directory, "memory_20240101_000001_1.snapshot.json")));
    }

    [Fact]
    public void DeletesOrphanSnapshotsAndIgnoresNonMatchingFiles()
    {
        CreateArtifact("memory_20240101_000000_1", dumpSize: 10, snapshotSize: 5, creationOrder: 1);
        File.WriteAllText(Path.Combine(_directory, "memory_20240101_000999_1.snapshot.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "notes.txt"), "keep");

        CreatePolicy(maxDumpCount: 5, maxDirectoryBytes: 1_000).Apply();

        Assert.False(File.Exists(Path.Combine(_directory, "memory_20240101_000999_1.snapshot.json")));
        Assert.True(File.Exists(Path.Combine(_directory, "notes.txt")));
        Assert.True(File.Exists(Path.Combine(_directory, "memory_20240101_000000_1.dmp")));
        Assert.True(File.Exists(Path.Combine(_directory, "memory_20240101_000000_1.snapshot.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private DumpRetentionPolicy CreatePolicy(int maxDumpCount, long maxDirectoryBytes)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new MemoryFlightRecorderOptions
        {
            DumpDirectory = _directory,
            MaxDumpCount = maxDumpCount,
            MaxDumpDirectorySizeBytes = maxDirectoryBytes
        });

        return new DumpRetentionPolicy(options, NullLogger<DumpRetentionPolicy>.Instance);
    }

    private void CreateArtifact(string baseName, int dumpSize, int snapshotSize, int creationOrder)
    {
        var dumpPath = Path.Combine(_directory, $"{baseName}.dmp");
        var snapshotPath = Path.Combine(_directory, $"{baseName}.snapshot.json");
        File.WriteAllBytes(dumpPath, new byte[dumpSize]);
        File.WriteAllBytes(snapshotPath, new byte[snapshotSize]);

        var timestamp = new DateTime(2024, 1, 1, 0, 0, creationOrder, DateTimeKind.Utc);
        File.SetCreationTimeUtc(dumpPath, timestamp);
        File.SetCreationTimeUtc(snapshotPath, timestamp);
    }
}
