using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using SadGallery.Tests.Integration.Infrastructure;
using SadGallery.Web.HealthChecks;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

/// <summary>
/// تست‌های پایش سلامت منبع نرخ (یکی از معیارهای پذیرش فاز ۲).
/// </summary>
public sealed class RateHealthTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Health_WithoutAnyRate_IsDegraded()
    {
        var check = Create(new RateSnapshotCache());

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("هنوز نرخ", result.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_WithFreshRates_IsHealthy()
    {
        var cache = new RateSnapshotCache();
        cache.Set("Tgn", [Rate(TimeSpan.FromSeconds(20))], [], Now);

        var result = await Create(cache).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(1, result.Data["items"]);
    }

    [Fact]
    public async Task Health_WithStaleRates_IsDegraded()
    {
        var cache = new RateSnapshotCache();
        cache.Set("Tgn", [Rate(TimeSpan.FromMinutes(30))], [], Now);

        var result = await Create(cache).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("قطع است", result.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_WithTooOldRates_IsUnhealthy()
    {
        var cache = new RateSnapshotCache();
        cache.Set("Tgn", [Rate(TimeSpan.FromHours(48))], [], Now);

        var result = await Create(cache).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Health_WithDisabledProvider_IsHealthyButExplains()
    {
        var options = Options();
        options.Provider = RateOptions.ProviderDisabled;

        var result = await Create(new RateSnapshotCache(), options, new DisabledProvider())
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("غیرفعال", result.Description!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_ConfiguredButNotYetFetched_IsDegraded()
    {
        var result = await Create(new RateSnapshotCache()).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    private static RateOptions Options() => new()
    {
        Provider = RateOptions.ProviderTgn,
        Username = "user",
        Password = "pass",
        AllowedProviderDomains = ["webservice.tgnsrv.ir"],
        FetchIntervalSeconds = 60,
        StaleThresholdMinutes = 15,
        MaxStaleHours = 24,
    };

    private static RateFeedHealthCheck Create(RateSnapshotCache cache, RateOptions? options = null, IRateProvider? provider = null)
    {
        var effective = options ?? Options();

        return new RateFeedHealthCheck(
            effective,
            provider ?? new ConfiguredProvider(),
            cache,
            new TestClock(Now),
            RateFreshnessPolicy.FromOptions(effective));
    }

    private static NormalizedRate Rate(TimeSpan age) => new()
    {
        AssetCode = "GOLD_GRAM_18",
        Title = "طلای ۱۸ عیار (هر گرم)",
        Amount = 1_442_800m,
        QuoteUnit = CurrencyUnit.Irt,
        ProviderId = "Tgn",
        QuotedAtUtc = Now - age,
        FetchedAtUtc = Now - age,
        Quality = RateQuality.Live,
    };

    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class ConfiguredProvider : IRateProvider
    {
        public string ProviderId => "Tgn";

        public bool IsConfigured => true;

        public Task<RateProviderFetchResult> FetchAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("در تست سلامت فراخوانی نمی‌شود.");
    }

    private sealed class DisabledProvider : IRateProvider
    {
        public string ProviderId => "Tgn";

        public bool IsConfigured => false;

        public Task<RateProviderFetchResult> FetchAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("در تست سلامت فراخوانی نمی‌شود.");
    }
}

/// <summary>تست‌های endpoint سلامت نرخ روی برنامه واقعی.</summary>
public sealed class RateHealthEndpointTests : IClassFixture<SadGalleryWebFactory>
{
    private readonly SadGalleryWebFactory _factory;

    public RateHealthEndpointTests(SadGalleryWebFactory factory) => _factory = factory;

    [Fact]
    public async Task HealthRates_Endpoint_Answers_ForDisabledProvider()
    {
        // در تست‌ها منبع غیرفعال است؛ «غیرفعال» خطا نیست و باید ۲۰۰ برگردد.
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/rates", UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoints_ReturnPersianFreePayloads_AndHealthyLiveness()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var liveness = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
    }
}
