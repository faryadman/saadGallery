using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Administration;

public sealed class AdministrationAuthorizationTests : IClassFixture<MemberWebFactory>
{
    private readonly MemberWebFactory factory;
    public AdministrationAuthorizationTests(MemberWebFactory factory) => this.factory = factory;
    [Theory]
    [InlineData(null)]
    [InlineData("Customer")]
    [InlineData("Operator")]
    public async Task EveryPanelActionEnforcesItsRoleIncludingPost(string? role)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (role != null) { client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, "true"); client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, role); }
        var html = await client.GetStringAsync("/market/gold", ct);
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value);
        Assert.NotEmpty(token);
        var actions = factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(a => a.RouteValues.TryGetValue("area", out var area) && (area == "Admin" || area == "Operator"))
            .Where(a => role != "Operator" || a.RouteValues["area"] == "Admin").ToArray();
        Assert.True(actions.Length >= (role == "Operator" ? 10 : 25));
        foreach (var action in actions)
        {
            var area = action.RouteValues["area"]!;
            var template = action.AttributeRouteInfo?.Template ?? $"/{area}/{action.ControllerName}/{action.ActionName}";
            var path = Regex.Replace(template, "\\{([^}:]+)(?::([^}]+))?\\}", m => m.Groups[2].Value == "guid" ? "11111111-1111-1111-1111-111111111111" : "1", RegexOptions.CultureInvariant);
            path = "/" + path.TrimStart('~', '/');
            var methods = action.ActionConstraints?.OfType<HttpMethodActionConstraint>().SelectMany(x => x.HttpMethods).Distinct().ToArray() ?? ["GET"];
            foreach (var method in methods)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                if (method == "POST") request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token });
                using var response = await client.SendAsync(request, ct);
                Assert.True(response.StatusCode == (role == null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden), $"{role ?? "Anonymous"}: {method} {path}: {response.StatusCode}");
            }
        }
    }
    [Theory]
    [InlineData("/admin/users/1")]
    [InlineData("/admin/settings")]
    public async Task AdministratorWritesRequireAntiforgery(string path)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, "true"); client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, "Admin");
        using var response = await client.PostAsync(path, new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
