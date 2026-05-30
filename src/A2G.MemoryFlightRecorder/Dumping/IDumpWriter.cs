using A2G.MemoryFlightRecorder.Evaluation;
using A2G.MemoryFlightRecorder.Monitoring;

namespace A2G.MemoryFlightRecorder.Dumping;

public interface IDumpWriter
{
    Task<bool> TryWriteDumpAsync(
        MemorySnapshot snapshot,
        MemoryPressureDecision decision,
        CancellationToken cancellationToken);
}
