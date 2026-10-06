using System.Net;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

public sealed class ErrorPageTests : IClassFixture<SadGalleryWebFactory>
{
    private readonly SadGalleryWebFactory _factory;

    public ErrorPageTests(SadGalleryWebFactory factory) => _factory = factory;

    [Fact]
    public async Task UnknownPath_Returns404_WithPersianMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/this-page-does-not-exist-12345", UriKind.Relative), cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("صفحه یافت نشد", html, StringComparison.Ordinal);
        // بدون افشای جزئیات داخلی
        Assert.DoesNotContain("Exception", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ErrorPage_IsAccessibleDirectly_AndUsesGenericMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/error", UriKind.Relative), cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("خطای غیرمنتظره", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RateLimitRejection_Page_IsAvailable()
    {
        // صفحه مخصوص ۴۲۹ (محدودیت نرخ) باید متن فارسی مناسب داشته باشد.
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/error/429", UriKind.Relative), cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Contains("درخواست‌های زیاد", html, StringComparison.Ordinal);
    }
}
