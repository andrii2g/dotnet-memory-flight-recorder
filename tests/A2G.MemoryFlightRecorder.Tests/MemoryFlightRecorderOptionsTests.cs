using A2G.MemoryFlightRecorder.DependencyInjection;
using A2G.MemoryFlightRecorder.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace A2G.MemoryFlightRecorder.Tests;

public sealed class MemoryFlightRecorderOptionsTests
{
    [Fact]
    public void InvalidPollIntervalFailsValidation()
    {
        var result = Validate(options => options.PollInterval = TimeSpan.Zero);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures!, failure => failure.Contains("PollInterval", StringComparison.Ordinal));
    }

    [Fact]
    public void CriticalThresholdLessThanOrEqualToWarningFailsValidation()
    {
        var result = Validate(options =>
        {
            options.WarningThreshold = 0.80;
            options.CriticalThreshold = 0.80;
        });

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures!, failure => failure.Contains("CriticalThreshold", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyDumpDirectoryFailsValidation()
    {
        var result = Validate(options => options.DumpDirectory = " ");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures!, failure => failure.Contains("DumpDirectory", StringComparison.Ordinal));
    }

    [Fact]
    public void MaxDumpCountLessThanOneFailsValidation()
    {
        var result = Validate(options => options.MaxDumpCount = 0);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures!, failure => failure.Contains("MaxDumpCount", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidDefaultOptionsPassValidation()
    {
        var result = new MemoryFlightRecorderOptionsValidator().Validate(
            Microsoft.Extensions.Options.Options.DefaultName,
            new MemoryFlightRecorderOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ConfigureDelegateIsNotInvokedDuringRegistration()
    {
        var services = new ServiceCollection();
        var configureCalls = 0;

        services.AddMemoryFlightRecorder(_ => configureCalls++);

        Assert.Equal(0, configureCalls);
    }

    [Fact]
    public void ConfigureDelegateIsAppliedThroughOptionsPipelineOncePerValueResolution()
    {
        var services = new ServiceCollection();
        var configureCalls = 0;

        services.AddMemoryFlightRecorder(options =>
        {
            configureCalls++;
            options.WarningThreshold = 0.72;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MemoryFlightRecorderOptions>>().Value;

        Assert.Equal(1, configureCalls);
        Assert.Equal(0.72, options.WarningThreshold);
    }

    private static ValidateOptionsResult Validate(Action<MemoryFlightRecorderOptions> configure)
    {
        var options = new MemoryFlightRecorderOptions();
        configure(options);
        return new MemoryFlightRecorderOptionsValidator().Validate(
            Microsoft.Extensions.Options.Options.DefaultName,
            options);
    }
}
