using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

/// <summary>
/// تست‌های نمایش نرخ روی صفحه اصلی: بدون داده ⇒ پیام صریح (بدون عدد ساختگی)،
/// با داده معتبر ⇒ کارت‌های نرخ با برچسب وضعیت.
/// </summary>
public sealed class HomeRatesTests
{
    // هر تست کارخانه تازه می‌سازد: کش نرخ درون‌فرایندی است و بین تست‌ها نباید به اشتراک بماند.

    [Fact]
    public async Task Home_WithoutRates_ShowsExplicitNoticeAndNoNumbers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync(new Uri("/", UriKind.Relative), cancellationToken);

        Assert.Contains("هیچ عدد یا نرخ ساختگی", html, StringComparison.Ordinal);
        Assert.DoesNotContain("طلای ۱۸ عیار", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_WithFreshRates_ShowsCardsWithQualityLabels()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        SeedCache(factory, TimeSpan.FromSeconds(20));

        using var client = factory.CreateClient();
        var response = await client.GetAsync(new Uri("/", UriKind.Relative), cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("طلای ۱۸ عیار (هر گرم)", html, StringComparison.Ordinal);
        Assert.Contains("۱۴٬۵۰۰٬۰۰۰", html, StringComparison.Ordinal);
        Assert.Contains("تومان", html, StringComparison.Ordinal);
        Assert.Contains("لحظه‌ای", html, StringComparison.Ordinal);
        Assert.DoesNotContain("هیچ عدد یا نرخ ساختگی", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_WithStaleRates_ShowsLastRecordedLabel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        SeedCache(factory, TimeSpan.FromMinutes(30));

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync(new Uri("/", UriKind.Relative), cancellationToken);

        Assert.Contains("آخرین نرخ ثبت‌شده", html, StringComparison.Ordinal);
        Assert.Contains("قطع است", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_WithFixtureData_ShowsSampleWarning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        SeedCache(factory, TimeSpan.FromSeconds(10), providerId: FixtureRateProvider.Id);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync(new Uri("/", UriKind.Relative), cancellationToken);

        Assert.Contains("نمونهٔ آزمایشی", html, StringComparison.Ordinal);
        Assert.Contains("نرخ روز بازار نیست", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_WithTooOldRates_ShowsNoNumbersAtAll()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        SeedCache(factory, TimeSpan.FromHours(30));

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync(new Uri("/", UriKind.Relative), cancellationToken);

        Assert.DoesNotContain("طلای ۱۸ عیار", html, StringComparison.Ordinal);
        Assert.Contains("معتبر نیستند", html, StringComparison.Ordinal);
    }

    private static void SeedCache(SadGalleryWebFactory factory, TimeSpan age, string providerId = "Tgn")
    {
        var now = factory.Services.GetRequiredService<Application.Abstractions.IClock>().UtcNow;
        var cache = factory.Services.GetRequiredService<RateSnapshotCache>();

        cache.Set(
            providerId,
            [
                new NormalizedRate
                {
                    AssetCode = "COIN_EMAMI",
                    Title = "سکه تمام بهار آزادی (طرح جدید / امامی)",
                    Amount = 14_500_000m,
                    QuoteUnit = CurrencyUnit.Irt,
                    ProviderId = providerId,
                    QuotedAtUtc = now - age,
                    FetchedAtUtc = now - age,
                    Quality = RateQuality.Live,
                },
                new NormalizedRate
                {
                    AssetCode = "GOLD_GRAM_18",
                    Title = "طلای ۱۸ عیار (هر گرم)",
                    Amount = 1_442_800m,
                    QuoteUnit = CurrencyUnit.Irt,
                    ProviderId = providerId,
                    QuotedAtUtc = now - age,
                    FetchedAtUtc = now - age,
                    Quality = RateQuality.Live,
                },
            ],
            [],
            now - age);
    }
}
