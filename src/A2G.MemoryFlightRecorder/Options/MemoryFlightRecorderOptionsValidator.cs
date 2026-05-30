using Microsoft.Extensions.Options;

namespace A2G.MemoryFlightRecorder.Options;

public sealed class MemoryFlightRecorderOptionsValidator : IValidateOptions<MemoryFlightRecorderOptions>
{
    public ValidateOptionsResult Validate(string? name, MemoryFlightRecorderOptions options)
    {
        var failures = new List<string>();

        if (options.PollInterval <= TimeSpan.Zero)
        {
            failures.Add("PollInterval must be greater than zero.");
        }

        if (options.WarningThreshold is <= 0 or >= 1)
        {
            failures.Add("WarningThreshold must be greater than 0 and less than 1.");
        }

        if (options.CriticalThreshold is <= 0 or >= 1)
        {
            failures.Add("CriticalThreshold must be greater than 0 and less than 1.");
        }

        if (options.CriticalThreshold <= options.WarningThreshold)
        {
            failures.Add("CriticalThreshold must be greater than WarningThreshold.");
        }

        if (options.DumpCooldown < TimeSpan.Zero)
        {
            failures.Add("DumpCooldown must not be negative.");
        }

        if (string.IsNullOrWhiteSpace(options.DumpDirectory))
        {
            failures.Add("DumpDirectory must not be empty.");
        }

        if (options.MaxDumpCount < 1)
        {
            failures.Add("MaxDumpCount must be at least 1.");
        }

        if (options.MaxDumpDirectorySizeBytes <= 0)
        {
            failures.Add("MaxDumpDirectorySizeBytes must be greater than zero.");
        }

        if (options.MinFreeDiskBytesBeforeDump < 0)
        {
            failures.Add("MinFreeDiskBytesBeforeDump must not be negative.");
        }

        if (options.MemoryLimitBytes is <= 0)
        {
            failures.Add("MemoryLimitBytes, when supplied, must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
