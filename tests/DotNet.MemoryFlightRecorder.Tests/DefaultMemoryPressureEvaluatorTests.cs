using DotNet.MemoryFlightRecorder.Evaluation;
using DotNet.MemoryFlightRecorder.Limits;
using DotNet.MemoryFlightRecorder.Monitoring;
using DotNet.MemoryFlightRecorder.Options;
using Microsoft.Extensions.Options;

namespace DotNet.MemoryFlightRecorder.Tests;

public sealed class DefaultMemoryPressureEvaluatorTests
{
    [Fact]
    public void ReturnsNormalBelowThresholds()
    {
        var evaluator = CreateEvaluator(new FakeMemoryLimitProvider(MemoryLimit.Available(1_000, "test")));
        var snapshot = CreateSnapshot(workingSetBytes: 400, privateMemoryBytes: 450, managedCommittedBytes: 300, managedHeapBytes: 250);

        var decision = evaluator.Evaluate(snapshot);

        Assert.Equal(MemoryPressureLevel.Normal, decision.Level);
        Assert.True(decision.IsLimitAvailable);
    }

    [Fact]
    public void ReturnsWarningWhenWorkingSetCrossesWarningThreshold()
    {
        var evaluator = CreateEvaluator(new FakeMemoryLimitProvider(MemoryLimit.Available(1_000, "test")));
        var snapshot = CreateSnapshot(workingSetBytes: 750, privateMemoryBytes: 500, managedCommittedBytes: 400, managedHeapBytes: 300);

        var decision = evaluator.Evaluate(snapshot);

        Assert.Equal(MemoryPressureLevel.Warning, decision.Level);
        Assert.Equal(0.75, decision.WorkingSetRatio);
    }

    [Fact]
    public void ReturnsCriticalWhenWorkingSetCrossesCriticalThreshold()
    {
        var evaluator = CreateEvaluator(new FakeMemoryLimitProvider(MemoryLimit.Available(1_000, "test")));
        var snapshot = CreateSnapshot(workingSetBytes: 900, privateMemoryBytes: 500, managedCommittedBytes: 400, managedHeapBytes: 300);

        var decision = evaluator.Evaluate(snapshot);

        Assert.Equal(MemoryPressureLevel.Critical, decision.Level);
        Assert.Equal(0.90, decision.WorkingSetRatio);
    }

    [Fact]
    public void ReturnsCriticalWhenManagedCommittedCrossesCriticalThreshold()
    {
        var evaluator = CreateEvaluator(new FakeMemoryLimitProvider(MemoryLimit.Available(1_000, "test")));
        var snapshot = CreateSnapshot(workingSetBytes: 500, privateMemoryBytes: 550, managedCommittedBytes: 860, managedHeapBytes: 600);

        var decision = evaluator.Evaluate(snapshot);

        Assert.Equal(MemoryPressureLevel.Critical, decision.Level);
        Assert.Equal(0.86, decision.ManagedCommittedRatio);
    }

    [Fact]
    public void UsesExplicitMemoryLimitWhenSupplied()
    {
        var evaluator = CreateEvaluator(new FakeMemoryLimitProvider(MemoryLimit.Available(2_000, "Options.MemoryLimitBytes")));
        var snapshot = CreateSnapshot(workingSetBytes: 1_000, privateMemoryBytes: 1_100, managedCommittedBytes: 900, managedHeapBytes: 800);

        var decision = evaluator.Evaluate(snapshot);

        Assert.Equal("Options.MemoryLimitBytes", decision.LimitSource);
        Assert.Equal(0.5, decision.WorkingSetRatio);
    }

    [Fact]
    public void ReturnsNormalWhenMemoryLimitIsUnavailable()
    {
        var evaluator = CreateEvaluator(new FakeMemoryLimitProvider(MemoryLimit.Unavailable("unavailable")));
        var snapshot = CreateSnapshot(workingSetBytes: 10_000, privateMemoryBytes: 9_000, managedCommittedBytes: 8_000, managedHeapBytes: 7_000);

        var decision = evaluator.Evaluate(snapshot);

        Assert.Equal(MemoryPressureLevel.Normal, decision.Level);
        Assert.False(decision.IsLimitAvailable);
        Assert.Null(decision.WorkingSetRatio);
    }

    private static DefaultMemoryPressureEvaluator CreateEvaluator(IMemoryLimitProvider memoryLimitProvider)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new MemoryFlightRecorderOptions());
        return new DefaultMemoryPressureEvaluator(options, memoryLimitProvider);
    }

    private static MemorySnapshot CreateSnapshot(
        long workingSetBytes,
        long privateMemoryBytes,
        long managedCommittedBytes,
        long managedHeapBytes)
    {
        return new MemorySnapshot(
            DateTimeOffset.UtcNow,
            ProcessId: 42,
            ProcessName: "test",
            ManagedHeapBytes: managedHeapBytes,
            ManagedCommittedBytes: managedCommittedBytes,
            FragmentedBytes: 0,
            TotalAvailableMemoryBytes: 0,
            MemoryLoadBytes: 0,
            HighMemoryLoadThresholdBytes: 0,
            GcPauseTimePercentage: 0,
            ProcessWorkingSetBytes: workingSetBytes,
            ProcessPrivateMemoryBytes: privateMemoryBytes,
            ApproximateUnmanagedBytes: 0,
            Gen0Collections: 0,
            Gen1Collections: 0,
            Gen2Collections: 0,
            ThreadCount: 0);
    }

    private sealed class FakeMemoryLimitProvider : IMemoryLimitProvider
    {
        private readonly MemoryLimit _limit;

        public FakeMemoryLimitProvider(MemoryLimit limit)
        {
            _limit = limit;
        }

        public MemoryLimit GetMemoryLimit() => _limit;
    }
}
