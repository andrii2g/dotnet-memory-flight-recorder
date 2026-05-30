using DotNet.MemoryFlightRecorder.Monitoring;

namespace DotNet.MemoryFlightRecorder.Evaluation;

public interface IMemoryPressureEvaluator
{
    MemoryPressureDecision Evaluate(MemorySnapshot snapshot);
}
