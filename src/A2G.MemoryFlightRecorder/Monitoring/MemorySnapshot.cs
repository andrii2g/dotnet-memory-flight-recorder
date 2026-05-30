namespace A2G.MemoryFlightRecorder.Monitoring;

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
    int ThreadCount);
