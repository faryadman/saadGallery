using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Settings;

public sealed class ConnectionNoticeAuthorizationTests : IClassFixture<MemberWebFactory>
{
    private readonly MemberWebFactory factory;
    public ConnectionNoticeAuthorizationTests(MemberWebFactory factory) => this.factory = factory;
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("Operator", HttpStatusCode.Forbidden)]
    public async Task EditorAndWriteRequireAdmin(string? role, HttpStatusCode expected)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (role != null) { client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, "true"); client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, role); }
        var publicPage = await client.GetStringAsync("/market/gold", TestContext.Current.CancellationToken);
        var token = WebUtility.HtmlDecode(Regex.Match(publicPage, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value);
        Assert.NotEmpty(token);
        using var get = await client.GetAsync("/admin/connection-notice", TestContext.Current.CancellationToken);
        using var post = await client.PostAsync("/admin/connection-notice", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }), TestContext.Current.CancellationToken);
        Assert.Equal(expected, get.StatusCode); Assert.Equal(expected, post.StatusCode);
    }
    [Fact]
    public async Task AdminWriteStillRequiresCsrf()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, "true"); client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, "Admin");
        using var post = await client.PostAsync("/admin/connection-notice", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }
}
