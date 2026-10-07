using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>
/// تست‌های منبع «نمونهٔ آزمایشی»: باید همان نمونه واقعی مالک را برگرداند و
/// هیچ‌گاه داده ساختگیِ خودساخته تولید نکند.
/// </summary>
public sealed class FixtureRateProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Fetch_ReturnsOwnerSampleWithFifteenRates()
    {
        var provider = new FixtureRateProvider(new FixedClock(Now), new RateOptions { Provider = RateOptions.ProviderFixture });

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.Success, result.Status);
        Assert.Equal("Fixture", result.ProviderId);
        Assert.NotNull(result.Payload);
        Assert.Equal(15, result.Payload!.Rates.Count);
        Assert.Equal(["Pelatin"], result.Payload.UnmappedKeys);
    }

    [Fact]
    public async Task Fetch_WithoutTimeShift_KeepsOriginalQuotedTime()
    {
        var options = new RateOptions { Provider = RateOptions.ProviderFixture, FixtureShiftQuotedTimeToNow = false };
        var provider = new FixtureRateProvider(new FixedClock(Now), options);

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new DateTimeOffset(2022, 6, 9, 7, 44, 48, TimeSpan.Zero), result.Payload!.QuotedAtUtc);
    }

    [Fact]
    public async Task Fetch_WithTimeShift_MovesQuotedTimeToNow()
    {
        var options = new RateOptions { Provider = RateOptions.ProviderFixture, FixtureShiftQuotedTimeToNow = true };
        var provider = new FixtureRateProvider(new FixedClock(Now), options);

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        var quoted = result.Payload!.QuotedAtUtc;

        Assert.NotNull(quoted);
        Assert.Equal(TimeSpan.FromHours(3.5), quoted!.Value.Offset);

        // ۱۲:۰۰ UTC = ۱۵:۳۰ به وقت ایران ⇒ رشته شبیه‌سازی‌شده همان را برمی‌گرداند.
        Assert.Equal(15, quoted.Value.Hour);
        Assert.Equal(30, quoted.Value.Minute);
        Assert.Equal(0, quoted.Value.Second);
        Assert.True(Math.Abs((quoted.Value - Now).TotalSeconds) <= 1);
    }

    [Fact]
    public async Task Fetch_IsAlwaysConfigured_ButHasNoCredentials()
    {
        var provider = new FixtureRateProvider(new FixedClock(Now), new RateOptions { Provider = RateOptions.ProviderFixture });

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.True(provider.IsConfigured);
        Assert.Equal(RateFetchStatus.Success, result.Status);
    }

    [Fact]
    public async Task Fetch_HonorsCancellation()
    {
        var provider = new FixtureRateProvider(new FixedClock(Now), new RateOptions { Provider = RateOptions.ProviderFixture });
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.FetchAsync(source.Token));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
