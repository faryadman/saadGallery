using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>
/// تست‌های سیاست تازگی نرخ: مرزهای Live/Delayed/Stale/Invalid.
/// این مرزها تعیین می‌کنند چه زمانی باید برچسب «آخرین نرخ ثبت‌شده» نمایش داده شود.
/// </summary>
public sealed class RateFreshnessPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private static RateFreshnessPolicy CreatePolicy() => new(
        liveWindow: TimeSpan.FromMinutes(2),
        staleAfter: TimeSpan.FromMinutes(15),
        maxAge: TimeSpan.FromHours(24),
        maxFutureSkew: TimeSpan.FromHours(6));

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(120)]   // مرز پنجره زنده
    public void Evaluate_WithinLiveWindow_IsLive(int secondsOld)
    {
        var policy = CreatePolicy();

        Assert.Equal(RateQuality.Live, policy.Evaluate(Now.AddSeconds(-secondsOld), Now));
    }

    [Theory]
    [InlineData(121)]
    [InlineData(600)]
    [InlineData(900)]   // مرز کهنگی
    public void Evaluate_AfterLiveWindow_IsDelayed(int secondsOld)
    {
        var policy = CreatePolicy();

        Assert.Equal(RateQuality.Delayed, policy.Evaluate(Now.AddSeconds(-secondsOld), Now));
    }

    [Theory]
    [InlineData(901)]
    [InlineData(3600)]
    [InlineData(86400)] // مرز حداکثر عمر
    public void Evaluate_BeyondStaleThreshold_IsStale(int secondsOld)
    {
        var policy = CreatePolicy();

        Assert.Equal(RateQuality.Stale, policy.Evaluate(Now.AddSeconds(-secondsOld), Now));
    }

    [Fact]
    public void Evaluate_BeyondMaxAge_IsInvalid()
    {
        var policy = CreatePolicy();

        Assert.Equal(RateQuality.Invalid, policy.Evaluate(Now.AddSeconds(-86401), Now));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Evaluate_SlightlyFutureQuote_IsTolerated(int hoursAhead)
    {
        // انحراف ساعت سرور منبع تا ۶ ساعت تحمل می‌شود (ساعت سرورها همیشه دقیق نیست).
        var policy = CreatePolicy();

        Assert.NotEqual(RateQuality.Invalid, policy.Evaluate(Now.AddHours(hoursAhead), Now));
    }

    [Fact]
    public void Evaluate_BeyondFutureSkew_IsInvalid()
    {
        var policy = CreatePolicy();

        Assert.Equal(RateQuality.Invalid, policy.Evaluate(Now.AddHours(7), Now));
    }

    [Fact]
    public void EvaluateWithReason_ReturnsPersianExplanation()
    {
        var policy = CreatePolicy();

        var (quality, description) = policy.EvaluateWithReason(Now.AddHours(-2), Now);

        Assert.Equal(RateQuality.Stale, quality);
        Assert.Contains("آخرین نرخ ثبت‌شده", description, StringComparison.Ordinal);
    }

    [Fact]
    public void FromOptions_LiveWindowIsTwiceTheFetchInterval_AtMinimumTwoMinutes()
    {
        var fast = RateFreshnessPolicy.FromOptions(new RateOptions { FetchIntervalSeconds = 10, StaleThresholdMinutes = 15, MaxStaleHours = 24 });
        var slow = RateFreshnessPolicy.FromOptions(new RateOptions { FetchIntervalSeconds = 600, StaleThresholdMinutes = 30, MaxStaleHours = 48 });

        Assert.Equal(TimeSpan.FromMinutes(2), fast.LiveWindow);   // ۲×۱۰=۲۰s ⇒ کف ۲ دقیقه
        Assert.Equal(TimeSpan.FromMinutes(20), slow.LiveWindow);  // ۲×۶۰۰s
        Assert.Equal(TimeSpan.FromMinutes(30), slow.StaleAfter);
        Assert.Equal(TimeSpan.FromHours(48), slow.MaxAge);
    }

    [Theory]
    [InlineData(0, 15, 24)]    // پنجره زنده صفر
    [InlineData(60, 0, 24)]    // کهنگی صفر
    [InlineData(60, 900, 1)]   // حداکثر عمر کمتر از کهنگی
    public void Constructor_InvalidWindows_Throws(int liveMinutes, int staleMinutes, int maxAgeHours)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateFreshnessPolicy(
            TimeSpan.FromMinutes(liveMinutes),
            TimeSpan.FromMinutes(staleMinutes),
            TimeSpan.FromHours(maxAgeHours),
            TimeSpan.FromHours(6)));
    }
}
