using A2G.MemoryFlightRecorder.Dumping;
using A2G.MemoryFlightRecorder.Evaluation;
using A2G.MemoryFlightRecorder.Limits;
using A2G.MemoryFlightRecorder.Monitoring;
using A2G.MemoryFlightRecorder.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace A2G.MemoryFlightRecorder.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMemoryFlightRecorder(
        this IServiceCollection services,
        Action<MemoryFlightRecorderOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<MemoryFlightRecorderOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.ValidateOnStart();

        services.AddSingleton<IValidateOptions<MemoryFlightRecorderOptions>, MemoryFlightRecorderOptionsValidator>();
        services.AddSingleton<IMemorySnapshotProvider, ProcessMemorySnapshotProvider>();
        services.AddSingleton<IMemoryLimitProvider, DefaultMemoryLimitProvider>();
        services.AddSingleton<IMemoryPressureEvaluator, DefaultMemoryPressureEvaluator>();
        services.AddSingleton<IDumpWriter, DiagnosticsDumpWriter>();
        services.AddSingleton<DumpRetentionPolicy>();
        services.AddHostedService<MemoryFlightRecorderService>();

        return services;
    }
}
