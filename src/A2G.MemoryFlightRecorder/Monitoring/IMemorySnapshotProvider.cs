namespace A2G.MemoryFlightRecorder.Monitoring;

public interface IMemorySnapshotProvider
{
    MemorySnapshot Capture();
}
