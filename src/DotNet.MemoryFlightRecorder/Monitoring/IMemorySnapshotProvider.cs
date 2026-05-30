namespace DotNet.MemoryFlightRecorder.Monitoring;

public interface IMemorySnapshotProvider
{
    MemorySnapshot Capture();
}
