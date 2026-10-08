using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

/// <summary>
/// صفحه‌های عمومی بازار (مهمان): نمای بازار، جزئیات، حباب‌سنج و ماشین‌حساب طلا.
/// کش نرخ پیش از هر تست مستقیم پر می‌شود (کارخانه پیش‌فرض هیچ درخواست خروجی نمی‌زند).
/// </summary>
public sealed class MarketPagesTests
{
    private static void SeedCache(SadGalleryWebFactory factory, TimeSpan age = default, string providerId = "Tgn")
    {
        var now = factory.Services.GetRequiredService<Application.Abstractions.IClock>().UtcNow;
        var cache = factory.Services.GetRequiredService<RateSnapshotCache>();
        var at = now - age;

        var rates = new List<NormalizedRate>
        {
            Build("GOLD_GRAM_18", "طلای ۱۸ عیار (هر گرم)", 3_000_000m, AssetKind.GoldGram, at, providerId),
            Build("COIN_EMAMI", "سکه تمام (امامی)", 29_500_000m, AssetKind.Coin, at, providerId),
            Build("CURRENCY_USD", "دلار", 62_000m, AssetKind.Currency, at, providerId),
        };

        cache.Set(providerId, rates, [], at);
    }

    private static NormalizedRate Build(string code, string title, decimal amount, AssetKind kind, DateTimeOffset at, string providerId) => new()
    {
        AssetCode = code,
        Title = title,
        Amount = amount,
        QuoteUnit = CurrencyUnit.Irt,
        ProviderId = providerId,
        QuotedAtUtc = at,
        FetchedAtUtc = at,
        Quality = RateQuality.Live,
        ScaleApplied = 1m,
    };

    private static async Task<string> GetHtmlAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [Fact]
    public async Task MarketIndex_ShowsSections_PersianUnits_AndFreshness()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        SeedCache(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await GetHtmlAsync(client, "/market", cancellationToken);

        Assert.Contains("طلا و مظنه", html, StringComparison.Ordinal);
        Assert.Contains("مسکوکات", html, StringComparison.Ordinal);
        Assert.Contains("ارز", html, StringComparison.Ordinal);
        Assert.Contains("رمزارزها", html, StringComparison.Ordinal);
        Assert.Contains("تومان", html, StringComparison.Ordinal);
        Assert.Contains("لحظه‌ای", html, StringComparison.Ordinal);
        // ارقام لاتین نباید در مقدار نرخ نمایش داده شود
        Assert.Contains("۳٬۰۰۰٬۰۰۰", html, StringComparison.Ordinal);
        Assert.DoesNotContain("3000000", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarketDetails_ShowsAsset_AndUnknownCodeIs404()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        SeedCache(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await GetHtmlAsync(client, "/market/rate/GOLD_GRAM_18", cancellationToken);
        Assert.Contains("طلای ۱۸ عیار", html, StringComparison.Ordinal);

        using var missing = await client.GetAsync(new Uri("/market/rate/DOES_NOT_EXIST", UriKind.Relative), cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task BubblePage_ForGuest_ShowsFormulaAndPrefilledStandardCoin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        SeedCache(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await GetHtmlAsync(client, "/market/bubble", cancellationToken);

        Assert.Contains(BubbleFormula.Version, html, StringComparison.Ordinal);
        Assert.Contains("value=\"8.133\"", html, StringComparison.Ordinal);   // وزن استاندارد پیش‌پر (ورودی فرم ASCII است)
        Assert.Contains("value=\"0.900\"", html, StringComparison.Ordinal);   // خلوص استاندارد (مقیاس اعشار حفظ می‌شود)
        Assert.Contains("محاسبه حباب", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BubblePost_WithValidInputs_ComputesBubble_WithPersianNumbers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        SeedCache(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await GetHtmlAsync(client, "/market/bubble", cancellationToken);
        var token = ExtractAntiforgeryToken(html);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/market/bubble", UriKind.Relative));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["MarketPrice"] = "۳۲٬۰۰۰٬۰۰۰",
            ["Weight"] = "۸٫۱۳۳",
            ["Purity"] = "۰٫۹",
            ["ReferenceRate"] = "۳٬۰۰۰٬۰۰۰",
            ["MintingCost"] = "0",
        });

        using var response = await client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var resultHtml = await response.Content.ReadAsStringAsync(cancellationToken);

        // ارزش ذاتی = ۲۹٬۲۷۸٬۸۰۰ و حباب = ۲٬۷۲۱٬۲۰۰
        Assert.Contains("۲۹٬۲۷۸٬۸۰۰", resultHtml, StringComparison.Ordinal);
        Assert.Contains("۲٬۷۲۱٬۲۰۰", resultHtml, StringComparison.Ordinal);
        Assert.Contains("تومان", resultHtml, StringComparison.Ordinal);
        Assert.Contains(BubbleFormula.Version, resultHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BubblePost_WithUnreadableNumber_ShowsPersianError_AndNoResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        SeedCache(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await GetHtmlAsync(client, "/market/bubble", cancellationToken);
        var token = ExtractAntiforgeryToken(html);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/market/bubble", UriKind.Relative));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["MarketPrice"] = "هزار تومان",
            ["Weight"] = "8",
            ["Purity"] = "0.9",
            ["ReferenceRate"] = "3000000",
        });

        using var response = await client.SendAsync(request, cancellationToken);
        var resultHtml = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Contains("عدد واردشده خوانا نیست", resultHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("sg-result__value", resultHtml, StringComparison.Ordinal); // بلوک نتیجه ساخته نشود
    }

    [Fact]
    public async Task GoldCalculator_PostWithKnownMesghalValue_MatchesExactFormula()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        SeedCache(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await GetHtmlAsync(client, "/market/gold", cancellationToken);
        var token = ExtractAntiforgeryToken(html);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/market/gold", UriKind.Relative));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Amount"] = "1",
            ["Unit"] = "Mesghal",
            ["Karat"] = "18",
            ["ReferenceRate"] = "3000000",
        });

        using var response = await client.SendAsync(request, cancellationToken);
        var resultHtml = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Contains("۴٫۶۰۸۳", resultHtml, StringComparison.Ordinal);   // وزن به گرم
        Assert.Contains("۱۳٬۸۲۴٬۹۰۰", resultHtml, StringComparison.Ordinal); // ۴٫۶۰۸۳ × ۳٬۰۰۰٬۰۰۰
    }

    [Fact]
    public async Task StoredRates_AreNeverLabeledLive_AndOfflineBannerExists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        // دریافت گذشته (۴۰ دقیقه قبل) ⇒ کهنه
        SeedCache(factory, TimeSpan.FromMinutes(40));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await GetHtmlAsync(client, "/", cancellationToken);

        Assert.Contains("آخرین نرخ ثبت‌شده", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">لحظه‌ای<", html, StringComparison.Ordinal);
        Assert.Contains("sg-offline-banner", html, StringComparison.Ordinal);
        Assert.Contains("ذخیره‌شده", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OfflineScript_IsServedLocally_AndRelabelsLiveBadges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(new Uri("/js/offline-status.js", UriKind.Relative), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var script = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("navigator.onLine", script, StringComparison.Ordinal);
        Assert.Contains("آخرین نرخ ثبت‌شده", script, StringComparison.Ordinal);
        Assert.Contains("sg-offline", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GuestCanUseBubbleAndGold_WithoutLogin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new SadGalleryWebFactory();
        SeedCache(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        foreach (var path in new[] { "/market", "/market/bubble", "/market/gold" })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant);
        Assert.True(match.Success, "توکن ضدجعل در فرم پیدا نشد.");
        return match.Groups[1].Value;
    }
}
