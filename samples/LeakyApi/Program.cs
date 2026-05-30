using A2G.MemoryFlightRecorder.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var sampleOptions = builder.Configuration.GetSection(SampleOptions.SectionName).Get<SampleOptions>() ?? new SampleOptions();

builder.WebHost.UseUrls(sampleOptions.Urls);

builder.Services.AddMemoryFlightRecorder(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(sampleOptions.PollIntervalSeconds);
    options.WarningThreshold = sampleOptions.WarningThreshold;
    options.CriticalThreshold = sampleOptions.CriticalThreshold;
    options.DumpDirectory = sampleOptions.DumpDirectory;
    options.DumpCooldown = TimeSpan.FromSeconds(sampleOptions.DumpCooldownSeconds);
    options.MemoryLimitBytes = sampleOptions.MemoryLimitBytes;
});
builder.Services.AddSingleton<LeakStore>();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/memory/status"));

app.MapGet("/memory/status", (LeakStore leakStore) =>
{
    using var process = System.Diagnostics.Process.GetCurrentProcess();
    process.Refresh();
    var gc = GC.GetGCMemoryInfo();

    return Results.Ok(new
    {
        process.Id,
        WorkingSetBytes = process.WorkingSet64,
        PrivateMemoryBytes = process.PrivateMemorySize64,
        ManagedHeapBytes = gc.HeapSizeBytes,
        ManagedCommittedBytes = gc.TotalCommittedBytes,
        gc.FragmentedBytes,
        gc.PauseTimePercentage,
        LeakCount = leakStore.Count
    });
});

app.MapPost("/leak/managed/{megabytes:int}", (int megabytes, LeakStore leakStore) =>
{
    if (megabytes <= 0 || megabytes > 512)
    {
        return Results.BadRequest("megabytes must be between 1 and 512");
    }

    leakStore.Add(new byte[megabytes * 1024 * 1024]);
    return Results.Ok(new { AddedMegabytes = megabytes, LeakCount = leakStore.Count });
});

app.MapPost("/leak/loh/{megabytes:int}", (int megabytes, LeakStore leakStore) =>
{
    if (megabytes <= 0 || megabytes > 512)
    {
        return Results.BadRequest("megabytes must be between 1 and 512");
    }

    for (var index = 0; index < megabytes; index++)
    {
        leakStore.Add(new byte[1024 * 1024]);
    }

    return Results.Ok(new { AddedMegabytes = megabytes, LeakCount = leakStore.Count });
});

app.MapPost("/leak/clear", (LeakStore leakStore) =>
{
    leakStore.Clear();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    return Results.Ok(new { Cleared = true });
});

app.Run();

internal sealed class LeakStore
{
    private readonly object _gate = new();
    private readonly List<byte[]> _buffers = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _buffers.Count;
            }
        }
    }

    public void Add(byte[] buffer)
    {
        lock (_gate)
        {
            _buffers.Add(buffer);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _buffers.Clear();
        }
    }
}

internal sealed class SampleOptions
{
    public const string SectionName = "MemoryFlightRecorderSample";

    public string Urls { get; set; } = "http://127.0.0.1:5000";

    public string DumpDirectory { get; set; } = "memory-dumps";

    public long MemoryLimitBytes { get; set; } = 512L * 1024 * 1024;

    public int PollIntervalSeconds { get; set; } = 2;

    public double WarningThreshold { get; set; } = 0.50;

    public double CriticalThreshold { get; set; } = 0.65;

    public int DumpCooldownSeconds { get; set; } = 120;
}
