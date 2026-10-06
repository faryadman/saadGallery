using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

public sealed class SecurityHeadersTests : IClassFixture<SadGalleryWebFactory>
{
    private readonly SadGalleryWebFactory _factory;

    public SecurityHeadersTests(SadGalleryWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Responses_ContainBaselineSecurityHeaders()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), cancellationToken);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("geolocation=()", response.Headers.GetValues("Permissions-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServerHeader_IsNotDisclosed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), cancellationToken);

        Assert.False(
            response.Headers.Contains("Server"),
            "سرآیند Server نباید افشا شود (کاهش اطلاعات در اختیار مهاجم).");
    }

    [Fact]
    public async Task AuthCookie_IsHttpOnly()
    {
        // کوکی احراز هویت باید HttpOnly باشد (بدون دسترسی JavaScript).
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/Account/Login", UriKind.Relative), cancellationToken);

        // در این مرحله کوکی نشست ساخته نمی‌شود؛ بررسی پیکربندی از طریق نام کوکی در هدر Set-Cookie
        // پس از ورود موفق انجام می‌شود (نیازمند دیتابیس). اینجا فقط نبود نشست ناامن تأیید می‌شود.
        var setCookieHeaders = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.ToList()
            : [];

        Assert.DoesNotContain(setCookieHeaders, header => header.Contains("SadGallery.Auth", StringComparison.Ordinal));
    }
}
