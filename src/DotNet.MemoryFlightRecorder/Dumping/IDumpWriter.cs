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
