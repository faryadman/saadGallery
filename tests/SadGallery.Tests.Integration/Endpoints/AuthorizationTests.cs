using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

/// <summary>
/// تست‌های کنترل دسترسی سمت سرور و محافظت CSRF.
/// قاعده پروژه: «پنهان کردن دکمه در UI» کنترل امنیتی نیست؛ درخواست مستقیم HTTP باید رد شود.
/// </summary>
public sealed class AuthorizationTests : IClassFixture<SadGalleryWebFactory>
{
    private readonly SadGalleryWebFactory _factory;

    public AuthorizationTests(SadGalleryWebFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/Admin/Dashboard")]
    [InlineData("/Operator/Dashboard")]
    [InlineData("/Member")]
    public async Task ProtectedAreas_RedirectAnonymousUserToLogin(string path)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false, // تا خودِ پاسخ ۳۰۲ بررسی شود، نه صفحه ورود
        });

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.Contains("/Account/Login", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginPost_WithoutAntiforgeryToken_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Identifier"] = "09120000000",
            ["Password"] = "TestPassword1",
        });

        using var response = await client.PostAsync(new Uri("/Account/Login", UriKind.Relative), content, cancellationToken);

        // بدون توکن ضدجعل، درخواست باید رد شود (۴۰۰) — نه اینکه پردازش شود.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterPost_WithoutAntiforgeryToken_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Identifier"] = "someone@example.invalid",
            ["Password"] = "TestPassword1",
            ["ConfirmPassword"] = "TestPassword1",
        });

        using var response = await client.PostAsync(new Uri("/Account/Register", UriKind.Relative), content, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
