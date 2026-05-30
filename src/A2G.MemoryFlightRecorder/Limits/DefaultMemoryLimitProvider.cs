using A2G.MemoryFlightRecorder.Options;
using Microsoft.Extensions.Options;

namespace A2G.MemoryFlightRecorder.Limits;

public sealed class DefaultMemoryLimitProvider : IMemoryLimitProvider
{
    private readonly IOptions<MemoryFlightRecorderOptions> _options;

    public DefaultMemoryLimitProvider(IOptions<MemoryFlightRecorderOptions> options)
    {
        _options = options;
    }

    public MemoryLimit GetMemoryLimit()
    {
        if (_options.Value.MemoryLimitBytes is long explicitLimit)
        {
            return explicitLimit > 0
                ? MemoryLimit.Available(explicitLimit, "Options.MemoryLimitBytes")
                : MemoryLimit.Unavailable("Options.MemoryLimitBytes was configured with an invalid non-positive value.");
        }

        var gcInfo = GC.GetGCMemoryInfo();
        if (gcInfo.TotalAvailableMemoryBytes > 0)
        {
            return MemoryLimit.Available(
                gcInfo.TotalAvailableMemoryBytes,
                "GC.GetGCMemoryInfo().TotalAvailableMemoryBytes");
        }

        return MemoryLimit.Unavailable(
            "No usable memory limit was reported by GC.GetGCMemoryInfo().TotalAvailableMemoryBytes. Configure MemoryLimitBytes to enable ratio-based pressure decisions.");
    }
}
