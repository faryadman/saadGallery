using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>
/// تست‌های سرویس نمایش نرخ: مسیر سریع کش (بدون کوئری اضافی)، fallback دیتابیس،
/// برچسب کهنگی و رفتار در خرابی دیتابیس.
/// </summary>
public sealed class RateDisplayServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private static RateOptions Options() => new()
    {
        Provider = RateOptions.ProviderTgn,
        FetchIntervalSeconds = 60,
        StaleThresholdMinutes = 15,
        MaxStaleHours = 24,
        CacheTtlSeconds = 30,
    };

    private static NormalizedRate Rate(string code, TimeSpan age, string providerId = "Tgn", bool anomaly = false) => new()
    {
        AssetCode = code,
        Title = code,
        Amount = 14_500_000m,
        QuoteUnit = CurrencyUnit.Irt,
        ProviderId = providerId,
        QuotedAtUtc = Now - age,
        FetchedAtUtc = Now - age,
        Quality = RateQuality.Live,
        IsAnomalySuspected = anomaly,
    };

    private static RateDisplayService CreateService(RateSnapshotCache cache, IRateStore store, RateOptions? options = null)
    {
        var effective = options ?? Options();

        return new RateDisplayService(
            cache,
            store,
            new FixedClock(Now),
            RateFreshnessPolicy.FromOptions(effective),
            effective);
    }

    [Fact]
    public async Task Get_EmptyCache_LoadsFromStoreExactlyOnce()
    {
        var store = new CountingStore([Rate("GOLD_GRAM_18", TimeSpan.FromSeconds(20))]);
        var cache = new RateSnapshotCache();
        var service = CreateService(cache, store);

        var first = await service.GetAsync(TestContext.Current.CancellationToken);
        var second = await service.GetAsync(TestContext.Current.CancellationToken);

        Assert.Single(first.Items);
        Assert.Single(second.Items);
        Assert.Equal(1, store.GetLatestCalls);   // مسیر گرم: هیچ کوئری اضافی
        Assert.False(cache.IsEmpty);
    }

    [Fact]
    public async Task Get_WarmCache_DoesNotTouchStore()
    {
        var store = new CountingStore([]);
        var cache = new RateSnapshotCache();
        cache.Set("Tgn", [Rate("GOLD_GRAM_18", TimeSpan.FromSeconds(10))], [], Now);

        var service = CreateService(cache, store);
        var model = await service.GetAsync(TestContext.Current.CancellationToken);

        Assert.Single(model.Items);
        Assert.Equal(0, store.GetLatestCalls);
        Assert.Equal(RateQuality.Live, model.OverallQuality);
        Assert.Null(model.Notice);
    }

    [Fact]
    public async Task Get_StoreFailure_ReturnsEmptyModelWithPersianNotice()
    {
        var service = CreateService(new RateSnapshotCache(), new ThrowingStore());

        var model = await service.GetAsync(TestContext.Current.CancellationToken);

        Assert.Empty(model.Items);
        Assert.NotNull(model.Notice);
        Assert.Contains("در دسترس نیست", model.Notice!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_DisabledProvider_ExplainsThatNoFakeRateIsShown()
    {
        var options = Options();
        options.Provider = RateOptions.ProviderDisabled;
        var service = CreateService(new RateSnapshotCache(), new CountingStore([]), options);

        var model = await service.GetAsync(TestContext.Current.CancellationToken);

        Assert.Empty(model.Items);
        Assert.Contains("ساختگی", model.Notice!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_DelayedRates_KeepTheirOwnLabel()
    {
        var cache = new RateSnapshotCache();
        cache.Set("Tgn", [Rate("GOLD_GRAM_18", TimeSpan.FromMinutes(5))], [], Now);

        var model = await CreateService(cache, new CountingStore([])).GetAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(model.Items);
        Assert.Equal(RateQuality.Delayed, item.Quality);
        Assert.Equal("تأخیری", item.QualityLabel);
        Assert.Null(model.Notice);
    }

    [Fact]
    public async Task Get_StaleRates_AreShownWithStaleLabelAndNotice()
    {
        var cache = new RateSnapshotCache();
        cache.Set("Tgn", [Rate("GOLD_GRAM_18", TimeSpan.FromMinutes(20))], [], Now);

        var model = await CreateService(cache, new CountingStore([])).GetAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(model.Items);
        Assert.Equal(RateQuality.Stale, item.Quality);
        Assert.Equal("آخرین نرخ ثبت‌شده", item.QualityLabel);
        Assert.Contains("قطع است", model.Notice!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_RatesBeyondMaxAge_AreNotShownAtAll()
    {
        var cache = new RateSnapshotCache();
        cache.Set("Tgn", [Rate("GOLD_GRAM_18", TimeSpan.FromHours(30))], [], Now);

        var model = await CreateService(cache, new CountingStore([])).GetAsync(TestContext.Current.CancellationToken);

        Assert.Empty(model.Items);   // فراتر از ۲۴ ساعت: هیچ عددی نمایش داده نمی‌شود
        Assert.Contains("معتبر نیستند", model.Notice!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_AnomalySuspectedRates_AreNeverDisplayed()
    {
        var cache = new RateSnapshotCache();
        cache.Set("Tgn", [Rate("GOLD_GRAM_18", TimeSpan.FromSeconds(10), anomaly: true)], [], Now);

        var model = await CreateService(cache, new CountingStore([])).GetAsync(TestContext.Current.CancellationToken);

        Assert.Empty(model.Items);
    }

    [Fact]
    public async Task Get_FixtureProvider_IsMarkedAsSampleData()
    {
        var cache = new RateSnapshotCache();
        cache.Set(FixtureRateProvider.Id, [Rate("GOLD_GRAM_18", TimeSpan.FromSeconds(10), FixtureRateProvider.Id)], [], Now);

        var model = await CreateService(cache, new CountingStore([])).GetAsync(TestContext.Current.CancellationToken);

        Assert.True(model.IsSampleData);
        Assert.Contains("نمونهٔ آزمایشی", model.Notice!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_FormatsAmountsWithPersianDigitsAndCorrectUnit()
    {
        var cache = new RateSnapshotCache();
        cache.Set("Tgn", [Rate("COIN_EMAMI", TimeSpan.FromSeconds(10))], [], Now);

        var model = await CreateService(cache, new CountingStore([])).GetAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(model.Items);
        Assert.Equal("۱۴٬۵۰۰٬۰۰۰", item.AmountText);
        Assert.Equal("تومان", item.UnitText);
    }

    [Theory]
    [InlineData(CurrencyUnit.Irt, "تومان")]
    [InlineData(CurrencyUnit.Irr, "ریال")]
    [InlineData(CurrencyUnit.Usd, "دلار")]
    public void Unit_NeverConvertsBetweenUnits(CurrencyUnit unit, string expected)
    {
        Assert.Equal(expected, RateFormat.Unit(unit));
    }

    [Fact]
    public void Amount_FractionalValue_UsesTwoDecimals()
    {
        Assert.Equal("۲۱٫۹۴", RateFormat.Amount(21.94m));
        Assert.Equal("۱٬۸۵۰", RateFormat.Amount(1850m));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class CountingStore(IReadOnlyList<NormalizedRate> rates) : IRateStore
    {
        public int GetLatestCalls { get; private set; }

        public Task<IReadOnlyList<NormalizedRate>> GetLatestAsync(CancellationToken cancellationToken)
        {
            GetLatestCalls++;
            return Task.FromResult(rates);
        }

        public Task<long> BeginRunAsync(RateFetchRunStart start, CancellationToken cancellationToken) => Task.FromResult(0L);

        public Task CompleteRunAsync(long runId, RateFetchRunFinish finish, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveRatesAsync(long runId, IReadOnlyList<NormalizedRate> ratesToSave, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<NormalizedRate>> GetHistoryAsync(string assetCode, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NormalizedRate>>([]);

        public Task<bool> TryAcquireLeaseAsync(string ownerId, TimeSpan ttl, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task ReleaseLeaseAsync(string ownerId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowingStore : IRateStore
    {
        public Task<IReadOnlyList<NormalizedRate>> GetLatestAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("دیتابیس در دسترس نیست (تست).");

        public Task<long> BeginRunAsync(RateFetchRunStart start, CancellationToken cancellationToken) => throw new InvalidOperationException("تست");

        public Task CompleteRunAsync(long runId, RateFetchRunFinish finish, CancellationToken cancellationToken) => throw new InvalidOperationException("تست");

        public Task SaveRatesAsync(long runId, IReadOnlyList<NormalizedRate> ratesToSave, CancellationToken cancellationToken) => throw new InvalidOperationException("تست");

        public Task<IReadOnlyList<NormalizedRate>> GetHistoryAsync(string assetCode, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) => throw new InvalidOperationException("تست");

        public Task<bool> TryAcquireLeaseAsync(string ownerId, TimeSpan ttl, CancellationToken cancellationToken) => throw new InvalidOperationException("تست");

        public Task ReleaseLeaseAsync(string ownerId, CancellationToken cancellationToken) => throw new InvalidOperationException("تست");
    }
}
