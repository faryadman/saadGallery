using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using SadGallery.Infrastructure.Market;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Market;

/// <summary>
/// تست‌های زنده مخزن نرخ روی SQL Server واقعی (در نبود سرور، صریح Skip می‌شوند).
/// اجرا: مقدار <c>SADGALLERY_TEST_SQL</c> را به رشته اتصال یک دیتابیس آزمایشی بدهید.
/// </summary>
public sealed class RateStoreSqlTests
{
    private static readonly DateTimeOffset QuotedAt = new(2026, 10, 8, 8, 0, 0, TimeSpan.Zero);

    [RequiresSqlServerFact]
    public async Task SaveAndReadLatest_ReturnsNewestPerAsset_ExcludingAnomalies()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken);

        var store = new RateStore(db, new TestClock(QuotedAt));
        var runId = await store.BeginRunAsync(new RateFetchRunStart("Tgn", "Manual", QuotedAt), cancellationToken);

        await store.SaveRatesAsync(runId, [Rate("GOLD_GRAM_18", 1_400_000m), Rate("COIN_EMAMI", 14_000_000m)], cancellationToken);
        await store.SaveRatesAsync(runId, [Rate("GOLD_GRAM_18", 1_450_000m)], cancellationToken);
        await store.SaveRatesAsync(runId, [Rate("SILVER_OUNCE_USD", 22m, anomaly: true, unit: CurrencyUnit.Usd)], cancellationToken);

        var latest = await store.GetLatestAsync(cancellationToken);

        var gram = Assert.Single(latest, rate => rate.AssetCode == "GOLD_GRAM_18");
        Assert.Equal(1_450_000m, gram.Amount);   // بزرگ‌ترین شناسه = جدیدترین نرخ

        Assert.DoesNotContain(latest, rate => rate.AssetCode == "SILVER_OUNCE_USD"); // جهش مشکوک منتشر نمی‌شود
        Assert.All(latest, rate => Assert.Empty(NormalizedRate.FindInvariantViolations(rate)));
    }

    [RequiresSqlServerFact]
    public async Task History_ReturnsOnlyRequestedAssetWithinRange_InOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken);

        var store = new RateStore(db, new TestClock(QuotedAt));
        var runId = await store.BeginRunAsync(new RateFetchRunStart("Tgn", "Manual", QuotedAt), cancellationToken);

        await store.SaveRatesAsync(
            runId,
            [
                Rate("GOLD_GRAM_18", 1_400_000m, quotedAt: QuotedAt.AddHours(-2)),
                Rate("GOLD_GRAM_18", 1_410_000m),
                Rate("COIN_EMAMI", 14_000_000m),
            ],
            cancellationToken);

        var history = await store.GetHistoryAsync("GOLD_GRAM_18", QuotedAt.AddHours(-1), QuotedAt.AddHours(1), cancellationToken);

        var entry = Assert.Single(history);
        Assert.Equal(1_410_000m, entry.Amount);
    }

    [RequiresSqlServerFact]
    public async Task Runs_AreRecordedWithOutcomeAndCounts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken);

        var store = new RateStore(db, new TestClock(QuotedAt));
        var runId = await store.BeginRunAsync(new RateFetchRunStart("Tgn", "Scheduled", QuotedAt), cancellationToken);

        await store.CompleteRunAsync(
            runId,
            new RateFetchRunFinish(RateFetchOutcomeKind.SuccessWithProblems, QuotedAt.AddSeconds(2), 2000, 15, 1, 2, 200, null, "کلیدهای ناشناخته پاسخ: Pelatin"),
            cancellationToken);

        var run = await db.MarketPriceFetchRuns.SingleAsync(item => item.Id == runId, cancellationToken);

        Assert.Equal((int)RateFetchOutcomeKind.SuccessWithProblems, run.Outcome);
        Assert.Equal(15, run.AcceptedCount);
        Assert.Equal(1, run.FlaggedCount);
        Assert.Equal(2, run.RejectedCount);
        Assert.Equal(200, run.HttpStatusCode);
        Assert.NotNull(run.FinishedAtUtc);
    }

    [RequiresSqlServerFact]
    public async Task Lease_IsExclusive_AcrossOwners_AndReleasable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken);

        var store = new RateStore(db, new TestClock(QuotedAt));
        var ttl = TimeSpan.FromMinutes(1);

        Assert.True(await store.TryAcquireLeaseAsync("instance-A", ttl, cancellationToken));
        Assert.False(await store.TryAcquireLeaseAsync("instance-B", ttl, cancellationToken));   // نمونه دیگر نمی‌تواند
        Assert.True(await store.TryAcquireLeaseAsync("instance-A", ttl, cancellationToken));    // همان دارنده می‌تواند تمدید کند

        await store.ReleaseLeaseAsync("instance-A", cancellationToken);

        Assert.True(await store.TryAcquireLeaseAsync("instance-B", ttl, cancellationToken));
    }

    [RequiresSqlServerFact]
    public async Task ExpiredLease_IsReclaimable_AfterCrash()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken);

        var clock = new TestClock(QuotedAt);
        var store = new RateStore(db, clock);

        Assert.True(await store.TryAcquireLeaseAsync("crashed-instance", TimeSpan.FromSeconds(30), cancellationToken));

        // نمونه قبلی «کرش» کرده و هرگز آزاد نکرده؛ پس از انقضای اجاره، اجاره خودکار آزاد می‌شود.
        clock.Advance(TimeSpan.FromSeconds(31));

        Assert.True(await store.TryAcquireLeaseAsync("live-instance", TimeSpan.FromSeconds(30), cancellationToken));
    }

    [RequiresSqlServerFact]
    public async Task SaveRates_RejectsRatesMissingRequiredPieces()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken);

        var store = new RateStore(db, new TestClock(QuotedAt));
        var runId = await store.BeginRunAsync(new RateFetchRunStart("Tgn", "Manual", QuotedAt), cancellationToken);

        var broken = new NormalizedRate
        {
            AssetCode = "GOLD_GRAM_18",
            Title = "طلای ۱۸ عیار",
            Amount = 1_000_000m,
            QuoteUnit = CurrencyUnit.Irt,
            ProviderId = string.Empty,       // نقض عمدی: نبود شناسه منبع
            QuotedAtUtc = QuotedAt,
            FetchedAtUtc = QuotedAt,
            Quality = RateQuality.Live,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveRatesAsync(runId, [broken], cancellationToken));
    }

    private static SadGalleryDbContext CreateContext()
    {
        var connectionString = RequiresSqlServerFactAttribute.ConnectionString!;

        var optionsBuilder = new DbContextOptionsBuilder<SadGalleryDbContext>()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(60));

        return new SadGalleryDbContext(optionsBuilder.Options);
    }

    private static NormalizedRate Rate(
        string code,
        decimal amount,
        bool anomaly = false,
        CurrencyUnit unit = CurrencyUnit.Irt,
        DateTimeOffset? quotedAt = null) => new()
    {
        AssetCode = code,
        Title = code,
        Amount = amount,
        QuoteUnit = unit,
        ProviderId = "Tgn",
        QuotedAtUtc = quotedAt ?? QuotedAt,
        FetchedAtUtc = quotedAt ?? QuotedAt,
        Quality = RateQuality.Live,
        IsAnomalySuspected = anomaly,
    };

    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void Advance(TimeSpan delta) => UtcNow += delta;
    }
}
