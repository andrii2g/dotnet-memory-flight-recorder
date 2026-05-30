namespace A2G.MemoryFlightRecorder.Evaluation;

public sealed record MemoryPressureDecision(
    MemoryPressureLevel Level,
    string Reason,
    long? LimitBytes,
    string LimitSource,
    bool IsLimitAvailable,
    double? WorkingSetRatio,
    double? PrivateMemoryRatio,
    double? ManagedCommittedRatio,
    double? ManagedHeapRatio);
