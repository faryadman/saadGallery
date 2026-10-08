using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

/// <summary>
/// کنترل دسترسی سرور برای امکانات ویژه اعضا (معیار فاز ۳):
/// مهمان باید ۳۰۲ به ورود بگیرد و عضو باید ۲۰۰ — «پنهان‌کردن لینک در UI» هرگز کنترل امنیتی نیست.
/// </summary>
public sealed class MarketAuthorizationTests : IClassFixture<MemberWebFactory>
{
    private readonly MemberWebFactory _factory;

    /// <summary>ساخت تست.</summary>
    public MarketAuthorizationTests(MemberWebFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/market/history")]
    [InlineData("/market/advanced-bubble")]
    [InlineData("/api/rates/GOLD_GRAM_18/history")]
    public async Task Guest_IsRedirectedToLogin(string path)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // مهمان با کارخانه «برنامه واقعی» آزمون می‌شود تا رفتار واقعی چالش کوکی Identity سنجیده شود.
        using var guestFactory = new SadGalleryWebFactory();
        using var client = guestFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Member_CanOpenExtendedHistoryPage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, "true");

        using var response = await client.GetAsync(new Uri("/market/history", UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("تاریخچه نرخ‌ها", html, StringComparison.Ordinal);
        // بدون دیتابیس: پیام صریح فارسی، نه خطای ۵۰۰ و نه عدد ساختگی
        Assert.Contains("دیتابیس در دسترس نیست", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Member_CanOpenAdvancedBubblePage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, "true");

        using var response = await client.GetAsync(new Uri("/market/advanced-bubble", UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("حباب‌سنج پیشرفته", html, StringComparison.Ordinal);
        Assert.Contains("محاسبه انجام نمی‌شود", html, StringComparison.Ordinal); // بدون نرخ، مقایسه انجام نمی‌شود
    }

    [Fact]
    public async Task Member_HistoryApi_ReturnsJsonShape()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, "true");

        using var response = await client.GetAsync(new Uri("/api/rates/COIN_EMAMI/history?days=7", UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;

        Assert.Equal("COIN_EMAMI", root.GetProperty("assetCode").GetString());
        Assert.Equal(7, root.GetProperty("days").GetInt32());
        Assert.Equal("تومان", root.GetProperty("unit").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("points").ValueKind);
    }

    [Fact]
    public async Task HistoryDays_AreClampedToServerLimit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, "true");

        using var response = await client.GetAsync(new Uri("/api/rates/GOLD_GRAM_18/history?days=9999", UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(90, document.RootElement.GetProperty("days").GetInt32()); // سقف سرور
    }
}
