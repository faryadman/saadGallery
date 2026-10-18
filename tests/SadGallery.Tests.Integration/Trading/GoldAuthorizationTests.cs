using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Tests.Integration.Infrastructure;
using SadGallery.Web.Security;
using Xunit;
namespace SadGallery.Tests.Integration.Trading;

public sealed class GoldAuthorizationTests(MemberWebFactory factory) : IClassFixture<MemberWebFactory>
{
    [Theory] [InlineData(null)] [InlineData("Customer")] [InlineData("Operator")]
    public async Task EveryForbiddenGoldActionIsGuardedOnServer(string? role)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (role != null) { client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, "true"); client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, role); }
        var token = WebUtility.HtmlDecode(Regex.Match(await client.GetStringAsync("/market/gold", ct), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value); Assert.NotEmpty(token);
        var actions = factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(a => a.ControllerName == "Gold" && (role == null || a.EndpointMetadata.OfType<AuthorizeAttribute>().Any(x => x.Policy == Policies.AdminOnly || (role == "Customer" && x.Policy == Policies.OperatorArea)))).ToList();
        Assert.Equal(role == "Operator" ? 5 : role == "Customer" ? 7 : 12, actions.Count);
        foreach (var action in actions)
        {
            var path = Regex.Replace(action.AttributeRouteInfo!.Template!, "\\{([^}:]+)(?::([^}]+))?\\}", m => m.Groups[2].Value == "guid" ? "11111111-1111-1111-1111-111111111111" : "1", RegexOptions.CultureInvariant);
            var method = action.ActionConstraints!.OfType<HttpMethodActionConstraint>().Single().HttpMethods.Single();
            using var request = new HttpRequestMessage(new HttpMethod(method), "/" + path.TrimStart('/'));
            if (method == "POST") request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token });
            using var response = await client.SendAsync(request, ct);
            Assert.True(response.StatusCode == (role == null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden), $"{role ?? "Anonymous"}: {method} {path}: {response.StatusCode}");
        }
    }
    [Theory] [InlineData("/gold/quote")] [InlineData("/admin/gold/settings")] [InlineData("/admin/gold/wallets/1/credit")] [InlineData("/operator/gold/11111111-1111-1111-1111-111111111111/review")]
    public async Task WritesRequireAntiforgery(string path)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, "true"); client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, "Admin");
        using var response = await client.PostAsync(path, new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
