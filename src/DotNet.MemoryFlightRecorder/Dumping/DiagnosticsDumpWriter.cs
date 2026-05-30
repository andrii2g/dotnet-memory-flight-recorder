using DotNet.MemoryFlightRecorder.Evaluation;
using DotNet.MemoryFlightRecorder.Monitoring;
using DotNet.MemoryFlightRecorder.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.MemoryFlightRecorder.Dumping;

public sealed class DiagnosticsDumpWriter : IDumpWriter
{
    private readonly IOptions<MemoryFlightRecorderOptions> _options;
    private readonly ILogger<DiagnosticsDumpWriter> _logger;

    public DiagnosticsDumpWriter(
        IOptions<MemoryFlightRecorderOptions> options,
        ILogger<DiagnosticsDumpWriter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<bool> TryWriteDumpAsync(
        MemorySnapshot snapshot,
        MemoryPressureDecision decision,
        CancellationToken cancellationToken)
    {
        if (!_options.Value.EnableDumpGeneration)
        {
            _logger.LogWarning("Memory dump generation is disabled. Reason={Reason}", decision.Reason);
            return Task.FromResult(false);
        }

        _logger.LogInformation(
            "Critical memory pressure detected for process {ProcessId}, but dump writing is not implemented in this phase.",
            snapshot.ProcessId);

        return Task.FromResult(false);
    }
}
