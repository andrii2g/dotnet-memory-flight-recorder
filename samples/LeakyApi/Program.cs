using DotNet.MemoryFlightRecorder.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5000");

builder.Services.AddMemoryFlightRecorder(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(2);
    options.WarningThreshold = 0.50;
    options.CriticalThreshold = 0.65;
    options.DumpDirectory = Path.Combine(AppContext.BaseDirectory, "memory-dumps");
    options.DumpCooldown = TimeSpan.FromMinutes(2);
    options.MemoryLimitBytes = 512L * 1024 * 1024;
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
