using A2G.MemoryFlightRecorder.Dumping;
using A2G.MemoryFlightRecorder.Evaluation;
using A2G.MemoryFlightRecorder.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace A2G.MemoryFlightRecorder.Monitoring;

public sealed class MemoryFlightRecorderService : BackgroundService
{
    private readonly IOptions<MemoryFlightRecorderOptions> _options;
    private readonly IMemorySnapshotProvider _snapshotProvider;
    private readonly IMemoryPressureEvaluator _evaluator;
    private readonly IDumpWriter _dumpWriter;
    private readonly DumpRetentionPolicy _retentionPolicy;
    private readonly ILogger<MemoryFlightRecorderService> _logger;
    private readonly SemaphoreSlim _dumpLock = new(1, 1);

    private DateTimeOffset _lastSuccessfulDumpAtUtc = DateTimeOffset.MinValue;
    private bool _loggedUnavailableLimit;

    public MemoryFlightRecorderService(
        IOptions<MemoryFlightRecorderOptions> options,
        IMemorySnapshotProvider snapshotProvider,
        IMemoryPressureEvaluator evaluator,
        IDumpWriter dumpWriter,
        DumpRetentionPolicy retentionPolicy,
        ILogger<MemoryFlightRecorderService> logger)
    {
        _options = options;
        _snapshotProvider = snapshotProvider;
        _evaluator = evaluator;
        _dumpWriter = dumpWriter;
        _retentionPolicy = retentionPolicy;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Value.PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var snapshot = _snapshotProvider.Capture();
                var decision = _evaluator.Evaluate(snapshot);

                LogDecision(snapshot, decision);

                if (decision.Level == MemoryPressureLevel.Critical)
                {
                    await TryWriteDumpAsync(snapshot, decision, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private void LogDecision(MemorySnapshot snapshot, MemoryPressureDecision decision)
    {
        if (!decision.IsLimitAvailable)
        {
            if (!_loggedUnavailableLimit)
            {
                _loggedUnavailableLimit = true;
                _logger.LogWarning(
                    "Memory limit unavailable; ratio-based memory pressure decisions are disabled. WorkingSet={WorkingSet:n0}, Private={Private:n0}, ManagedCommitted={ManagedCommitted:n0}, LimitSource={LimitSource}",
                    snapshot.ProcessWorkingSetBytes,
                    snapshot.ProcessPrivateMemoryBytes,
                    snapshot.ManagedCommittedBytes,
                    decision.LimitSource);
            }

            return;
        }

        if (decision.Level == MemoryPressureLevel.Warning)
        {
            _logger.LogWarning(
                "High memory pressure. WorkingSet={WorkingSet:n0}, Private={Private:n0}, ManagedHeap={ManagedHeap:n0}, ManagedCommitted={ManagedCommitted:n0}, Limit={Limit:n0} ({LimitSource}), WorkingSetRatio={WorkingSetRatio:P1}, ManagedCommittedRatio={ManagedCommittedRatio:P1}",
                snapshot.ProcessWorkingSetBytes,
                snapshot.ProcessPrivateMemoryBytes,
                snapshot.ManagedHeapBytes,
                snapshot.ManagedCommittedBytes,
                decision.LimitBytes,
                decision.LimitSource,
                decision.WorkingSetRatio,
                decision.ManagedCommittedRatio);
        }
        else if (decision.Level == MemoryPressureLevel.Critical)
        {
            _logger.LogCritical(
                "Critical memory pressure. Reason={Reason}, WorkingSetRatio={WorkingSetRatio:P1}, PrivateMemoryRatio={PrivateMemoryRatio:P1}, ManagedCommittedRatio={ManagedCommittedRatio:P1}, ManagedHeapRatio={ManagedHeapRatio:P1}",
                decision.Reason,
                decision.WorkingSetRatio,
                decision.PrivateMemoryRatio,
                decision.ManagedCommittedRatio,
                decision.ManagedHeapRatio);
        }
    }

    private async Task TryWriteDumpAsync(
        MemorySnapshot snapshot,
        MemoryPressureDecision decision,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var cooldown = _options.Value.DumpCooldown;

        if (now - _lastSuccessfulDumpAtUtc < cooldown)
        {
            _logger.LogWarning(
                "Skipping memory dump because cooldown is active. LastSuccessfulDumpAtUtc={LastSuccessfulDumpAtUtc}, Cooldown={Cooldown}",
                _lastSuccessfulDumpAtUtc,
                cooldown);
            return;
        }

        if (!await _dumpLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("Skipping memory dump because another dump is already in progress.");
            return;
        }

        try
        {
            var dumpWritten = await _dumpWriter.TryWriteDumpAsync(snapshot, decision, cancellationToken);
            if (!dumpWritten)
            {
                return;
            }

            _lastSuccessfulDumpAtUtc = DateTimeOffset.UtcNow;
            _retentionPolicy.Apply();
        }
        finally
        {
            _dumpLock.Release();
        }
    }
}
