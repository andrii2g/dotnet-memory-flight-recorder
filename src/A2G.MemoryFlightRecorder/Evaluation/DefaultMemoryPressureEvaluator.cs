using A2G.MemoryFlightRecorder.Limits;
using A2G.MemoryFlightRecorder.Monitoring;
using A2G.MemoryFlightRecorder.Options;
using Microsoft.Extensions.Options;

namespace A2G.MemoryFlightRecorder.Evaluation;

public sealed class DefaultMemoryPressureEvaluator : IMemoryPressureEvaluator
{
    private readonly IOptions<MemoryFlightRecorderOptions> _options;
    private readonly IMemoryLimitProvider _memoryLimitProvider;

    public DefaultMemoryPressureEvaluator(
        IOptions<MemoryFlightRecorderOptions> options,
        IMemoryLimitProvider memoryLimitProvider)
    {
        _options = options;
        _memoryLimitProvider = memoryLimitProvider;
    }

    public MemoryPressureDecision Evaluate(MemorySnapshot snapshot)
    {
        var options = _options.Value;
        var limit = _memoryLimitProvider.GetMemoryLimit();

        if (!limit.IsAvailable || limit.Bytes is not long limitBytes)
        {
            return new MemoryPressureDecision(
                MemoryPressureLevel.Normal,
                "Memory limit is unavailable; ratio-based pressure evaluation was skipped.",
                null,
                limit.Source,
                false,
                null,
                null,
                null,
                null);
        }

        var workingSetRatio = Ratio(snapshot.ProcessWorkingSetBytes, limitBytes);
        var privateMemoryRatio = Ratio(snapshot.ProcessPrivateMemoryBytes, limitBytes);
        var managedCommittedRatio = Ratio(snapshot.ManagedCommittedBytes, limitBytes);
        var managedHeapRatio = Ratio(snapshot.ManagedHeapBytes, limitBytes);

        var strongestRatio = Math.Max(
            Math.Max(workingSetRatio, privateMemoryRatio),
            Math.Max(managedCommittedRatio, managedHeapRatio));

        if (strongestRatio >= options.CriticalThreshold)
        {
            return new MemoryPressureDecision(
                MemoryPressureLevel.Critical,
                $"Memory pressure crossed critical threshold {options.CriticalThreshold:P0}.",
                limitBytes,
                limit.Source,
                true,
                workingSetRatio,
                privateMemoryRatio,
                managedCommittedRatio,
                managedHeapRatio);
        }

        if (strongestRatio >= options.WarningThreshold)
        {
            return new MemoryPressureDecision(
                MemoryPressureLevel.Warning,
                $"Memory pressure crossed warning threshold {options.WarningThreshold:P0}.",
                limitBytes,
                limit.Source,
                true,
                workingSetRatio,
                privateMemoryRatio,
                managedCommittedRatio,
                managedHeapRatio);
        }

        return new MemoryPressureDecision(
            MemoryPressureLevel.Normal,
            "Memory pressure is normal.",
            limitBytes,
            limit.Source,
            true,
            workingSetRatio,
            privateMemoryRatio,
            managedCommittedRatio,
            managedHeapRatio);
    }

    private static double Ratio(long value, long limitBytes)
    {
        return Math.Clamp((double)value / limitBytes, 0, 1);
    }
}
