using A2G.MemoryFlightRecorder.Evaluation;
using A2G.MemoryFlightRecorder.Monitoring;
using A2G.MemoryFlightRecorder.Options;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace A2G.MemoryFlightRecorder.Dumping;

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

    public async Task<bool> TryWriteDumpAsync(
        MemorySnapshot snapshot,
        MemoryPressureDecision decision,
        CancellationToken cancellationToken)
    {
        var options = _options.Value;

        if (!options.EnableDumpGeneration)
        {
            _logger.LogWarning("Memory dump generation is disabled. Reason={Reason}", decision.Reason);
            return false;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var dumpPath = DumpFileNamer.CreateDumpPath(options.DumpDirectory, Environment.ProcessId, timestamp);
        var snapshotPath = Path.ChangeExtension(dumpPath, ".snapshot.json");

        try
        {
            Directory.CreateDirectory(options.DumpDirectory);

            var root = Path.GetPathRoot(Path.GetFullPath(options.DumpDirectory));
            if (string.IsNullOrWhiteSpace(root))
            {
                _logger.LogError(
                    "Skipping memory dump because the dump directory root could not be determined. DumpDirectory={DumpDirectory}",
                    options.DumpDirectory);
                return false;
            }

            var drive = new DriveInfo(root);
            if (drive.AvailableFreeSpace < options.MinFreeDiskBytesBeforeDump)
            {
                _logger.LogError(
                    "Skipping memory dump because free disk space is too low. Available={Available:n0}, Required={Required:n0}",
                    drive.AvailableFreeSpace,
                    options.MinFreeDiskBytesBeforeDump);
                return false;
            }

            var client = new DiagnosticsClient(Environment.ProcessId);

            await client.WriteDumpAsync(
                options.DumpType,
                dumpPath,
                options.LogDumpGeneration,
                cancellationToken);
        }
        catch (DiagnosticsClientException ex)
        {
            _logger.LogError(
                ex,
                "Failed to write memory dump through .NET diagnostics. Check DOTNET_EnableDiagnostics, DOTNET_EnableDiagnostics_IPC, runtime compatibility, and dump directory permissions.");
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to write memory dump to {DumpPath}", dumpPath);
            return false;
        }

        if (options.WriteSnapshotJson)
        {
            try
            {
                var payload = new
                {
                    Snapshot = snapshot,
                    Decision = decision,
                    Options = new
                    {
                        options.WarningThreshold,
                        options.CriticalThreshold,
                        options.DumpType,
                        options.DumpCooldown
                    }
                };

                await File.WriteAllTextAsync(
                    snapshotPath,
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "Memory dump was written, but snapshot JSON failed. SnapshotPath={SnapshotPath}",
                    snapshotPath);
            }
        }

        _logger.LogCritical(
            "Memory dump written. DumpPath={DumpPath}, SnapshotPath={SnapshotPath}, Reason={Reason}",
            dumpPath,
            options.WriteSnapshotJson ? snapshotPath : "disabled",
            decision.Reason);

        return true;
    }
}
