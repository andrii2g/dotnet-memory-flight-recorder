using DotNet.MemoryFlightRecorder.Dumping;
using DotNet.MemoryFlightRecorder.Evaluation;
using DotNet.MemoryFlightRecorder.Limits;
using DotNet.MemoryFlightRecorder.Monitoring;
using DotNet.MemoryFlightRecorder.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotNet.MemoryFlightRecorder.DependencyInjection;

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
