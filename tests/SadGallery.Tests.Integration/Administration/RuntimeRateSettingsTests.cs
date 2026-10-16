using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Administration;
using SadGallery.Application.Market;
using SadGallery.Infrastructure.Market;
using Xunit;

namespace SadGallery.Tests.Integration.Administration;

public sealed class RuntimeRateSettingsTests
{
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class Reader(string mode) : IRuntimeRateSettings
    {
        public Task<RuntimeRateSettings?> GetAsync(CancellationToken ct) => mode switch
        {
            "missing" => Task.FromResult<RuntimeRateSettings?>(null),
            "error" => throw new InvalidOperationException("test-only"),
            _ => Task.FromResult<RuntimeRateSettings?>(new(true, 120, [], null, null))
        };
    }
    private sealed class Provider : IRateProvider
    {
        public string ProviderId => "Tgn"; public bool IsConfigured => true;
        public int Calls { get; private set; }
        public Task<RateProviderFetchResult> FetchAsync(CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(RateProviderFetchResult.Failure(ProviderId, RateFetchStatus.Timeout, DateTimeOffset.UtcNow, "Timeout", "آزمون بدون شبکه")); }
    }
    [Theory]
    [InlineData("paused", "Scheduled")]
    [InlineData("paused", "Manual")]
    [InlineData("missing", "Scheduled")]
    [InlineData("missing", "Manual")]
    [InlineData("error", "Scheduled")]
    [InlineData("error", "Manual")]
    [InlineData("unregistered", "Scheduled")]
    [InlineData("unregistered", "Manual")]
    public async Task PausedOrUnavailableSettingsBlockEveryOutgoingFetch(string mode, string trigger)
    {
        var services = new ServiceCollection(); if (mode != "unregistered") services.AddScoped<IRuntimeRateSettings>(_ => new Reader(mode));
        using var sp = services.BuildServiceProvider(); var source = new Provider(); var options = new RateOptions(); var cache = new RateSnapshotCache();
        var orchestrator = new RateFetchOrchestrator(sp.GetRequiredService<IServiceScopeFactory>(), source, cache,
            new RateNormalizer(RateFreshnessPolicy.FromOptions(options), new RateAnomalyDetector(50), options), options, new Clock(), NullLogger<RateFetchOrchestrator>.Instance);
        var result = await orchestrator.RunOnceAsync(trigger, TestContext.Current.CancellationToken);
        Assert.Equal(RateFetchStatus.NotConfigured, result.Status); Assert.Equal(0, source.Calls); Assert.True(cache.IsEmpty);
    }
    private sealed class MutableReader : IRuntimeRateSettings
    {
        public bool Paused { get; set; } = true;
        public Task<RuntimeRateSettings?> GetAsync(CancellationToken ct) => Task.FromResult<RuntimeRateSettings?>(new(Paused, 120, [], null, null));
    }
    [Fact]
    public async Task PauseAndResumeAreRereadOnTheSameOrchestratorWithoutRestart()
    {
        var reader = new MutableReader(); var services = new ServiceCollection(); services.AddSingleton<IRuntimeRateSettings>(reader);
        using var sp = services.BuildServiceProvider(); var source = new Provider(); var options = new RateOptions(); var cache = new RateSnapshotCache();
        var orchestrator = new RateFetchOrchestrator(sp.GetRequiredService<IServiceScopeFactory>(), source, cache,
            new RateNormalizer(RateFreshnessPolicy.FromOptions(options), new RateAnomalyDetector(50), options), options, new Clock(), NullLogger<RateFetchOrchestrator>.Instance);
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(RateFetchStatus.NotConfigured, (await orchestrator.RunOnceAsync(RunTriggers.Manual, ct)).Status); Assert.Equal(0, source.Calls);
        reader.Paused = false;
        Assert.Equal(RateFetchStatus.Timeout, (await orchestrator.RunOnceAsync(RunTriggers.Manual, ct)).Status); Assert.Equal(1, source.Calls);
        reader.Paused = true;
        Assert.Equal(RateFetchStatus.NotConfigured, (await orchestrator.RunOnceAsync(RunTriggers.Manual, ct)).Status); Assert.Equal(1, source.Calls);
    }

}
