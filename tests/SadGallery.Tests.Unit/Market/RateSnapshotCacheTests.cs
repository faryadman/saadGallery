using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>تست‌های کش درون‌فرایندی نرخ (منبع نمایش سریع صفحه).</summary>
public sealed class RateSnapshotCacheTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private static NormalizedRate Rate(string code, DateTimeOffset fetchedAt) => new()
    {
        AssetCode = code,
        Title = code,
        Amount = 1_000_000m,
        QuoteUnit = CurrencyUnit.Irt,
        ProviderId = "Tgn",
        QuotedAtUtc = fetchedAt,
        FetchedAtUtc = fetchedAt,
        Quality = RateQuality.Live,
    };

    [Fact]
    public void NewCache_IsEmpty()
    {
        var cache = new RateSnapshotCache();

        Assert.True(cache.IsEmpty);
        Assert.Null(cache.Get());
        Assert.Null(cache.Age(Now));
    }

    [Fact]
    public void SetThenGet_ReturnsSnapshot()
    {
        var cache = new RateSnapshotCache();
        var rate = Rate("GOLD_GRAM_18", Now);

        cache.Set("Tgn", [rate], [], Now);

        var snapshot = cache.Get();
        Assert.NotNull(snapshot);
        Assert.Equal("Tgn", snapshot!.ProviderId);
        Assert.Same(rate, Assert.Single(snapshot.Publishable));
        Assert.False(cache.IsEmpty);
    }

    [Fact]
    public void Set_ReplacesAtomically()
    {
        var cache = new RateSnapshotCache();
        cache.Set("Tgn", [Rate("A", Now)], [], Now);
        cache.Set("Tgn", [Rate("B", Now)], [], Now.AddMinutes(1));

        var snapshot = cache.Get();

        Assert.Equal("B", Assert.Single(snapshot!.Publishable).AssetCode);
        Assert.Equal(Now.AddMinutes(1), snapshot.FetchedAtUtc);
    }

    [Fact]
    public void Age_AndExpiry_FollowConfiguredTtl()
    {
        var cache = new RateSnapshotCache();
        cache.Set("Tgn", [Rate("A", Now)], [], Now);

        Assert.Equal(TimeSpan.FromSeconds(30), cache.Age(Now.AddSeconds(30)));
        Assert.False(cache.IsExpired(Now.AddSeconds(30), TimeSpan.FromSeconds(60)));
        Assert.True(cache.IsExpired(Now.AddSeconds(61), TimeSpan.FromSeconds(60)));
        Assert.True(new RateSnapshotCache().IsExpired(Now, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void Set_KeepsFlaggedSeparateFromPublishable()
    {
        var cache = new RateSnapshotCache();
        var rate = Rate("A", Now);
        cache.Set("Tgn", [], [rate], Now);

        var snapshot = cache.Get();

        Assert.Empty(snapshot!.Publishable);
        Assert.Same(rate, Assert.Single(snapshot.Flagged));
    }
}
