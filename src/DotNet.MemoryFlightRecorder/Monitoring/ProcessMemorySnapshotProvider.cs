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
