using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SadGallery.Tests.Integration.Infrastructure;
using SadGallery.Web.Security;
using Xunit;

namespace SadGallery.Tests.Integration.Administration;

public sealed class SessionValidationTests
{
    private sealed class UnavailableValidator : ISecurityStampValidator
    {
        public Task ValidateAsync(CookieValidatePrincipalContext context) => throw new InvalidOperationException("PrivateValidationFailureCanary");
    }
    [Fact]
    public async Task FailedSessionValidationRejectsCookieWithoutAnExceptionPage()
    {
        using var baseFactory = new CatalogWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Scoped<ISecurityStampValidator, UnavailableValidator>())));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "123"), new Claim(ClaimTypes.Role, "Admin"), new Claim(AppClaims.IsActive, "true")], IdentityConstants.ApplicationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) }, IdentityConstants.ApplicationScheme);
        client.DefaultRequestHeaders.Add("Cookie", "SadGallery.Auth=" + options.TicketDataFormat.Protect(ticket));
        var ct = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync("/Admin/Dashboard", ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Contains("/Account/Login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), x => x.StartsWith("SadGallery.Auth=;", StringComparison.Ordinal));
        var html = await client.GetStringAsync("/market/gold", ct);
        Assert.Contains("ماشین‌حساب", html, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivateValidationFailureCanary", html, StringComparison.Ordinal); Assert.DoesNotContain("InvalidOperationException", html, StringComparison.Ordinal);
    }
}
