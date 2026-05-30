using Microsoft.Extensions.Logging;

namespace DotNet.MemoryFlightRecorder.Dumping;

public sealed class DumpRetentionPolicy
{
    private readonly ILogger<DumpRetentionPolicy> _logger;

    public DumpRetentionPolicy(ILogger<DumpRetentionPolicy> logger)
    {
        _logger = logger;
    }

    public void Apply()
    {
        _logger.LogDebug("Dump retention policy is not implemented in this phase.");
    }
}
