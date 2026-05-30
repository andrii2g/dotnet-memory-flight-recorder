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
