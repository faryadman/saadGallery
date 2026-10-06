using System.Net;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

public sealed class HomePageTests : IClassFixture<SadGalleryWebFactory>
{
    private readonly SadGalleryWebFactory _factory;

    public HomePageTests(SadGalleryWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Home_RendersPersianRtlPage_ForAnonymousVisitor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("dir=\"rtl\"", html, StringComparison.Ordinal);
        Assert.Contains("lang=\"fa\"", html, StringComparison.Ordinal);
        Assert.Contains("گالری صاد", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_DoesNotShowAnyFabricatedRate()
    {
        // قاعده پروژه: تا اتصال به منبع معتبر نرخ، هیچ عدد نرخی — حتی نمونه — نمایش داده نمی‌شود.
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Contains("هیچ عدد یا نرخ ساختگی", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_EmitsPersianTextAsUnicode_NotHtmlEntities()
    {
        // رگرسیون BUG-003: متن فارسی نباید به &#x...; تبدیل شود.
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.DoesNotContain("&#x", html, StringComparison.Ordinal);
    }
}
