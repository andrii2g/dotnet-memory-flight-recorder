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
