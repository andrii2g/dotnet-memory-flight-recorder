using A2G.MemoryFlightRecorder.Monitoring;

namespace A2G.MemoryFlightRecorder.Evaluation;

public interface IMemoryPressureEvaluator
{
    MemoryPressureDecision Evaluate(MemorySnapshot snapshot);
}
