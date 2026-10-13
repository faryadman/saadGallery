using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;
namespace SadGallery.Tests.Integration.Endpoints;
public sealed class TicketAuthorizationTests : IClassFixture<MemberWebFactory>
{
    private readonly MemberWebFactory _factory;
    public TicketAuthorizationTests(MemberWebFactory factory) => _factory=factory;
    [Theory]
    [InlineData("/tickets")] [InlineData("/tickets/new")] [InlineData("/tickets/11111111-1111-1111-1111-111111111111")]
    [InlineData("/operator/tickets")] [InlineData("/operator/tickets/11111111-1111-1111-1111-111111111111")]
    public async Task GuestCannotReadTicketEndpoints(string path)
    {
        using var client=_factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect=false });
        using var response=await client.GetAsync(path,TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
    }
    [Theory]
    [InlineData("/operator/tickets")] [InlineData("/operator/tickets/11111111-1111-1111-1111-111111111111")]
    public async Task CustomerCannotReadOperatorEndpoints(string path)
    {
        using var client=_factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect=false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName,"true");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName,"Customer");
        using var response=await client.GetAsync(path,TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
    }
    [Theory]
    [InlineData("/tickets/new")] [InlineData("/tickets/11111111-1111-1111-1111-111111111111/reply")]
    [InlineData("/operator/tickets/11111111-1111-1111-1111-111111111111")]
    public async Task WritesWithoutCsrfNeverSucceed(string path)
    {
        using var client=_factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect=false });
        using var response=await client.PostAsync(path,new FormUrlEncodedContent([]),TestContext.Current.CancellationToken);
        Assert.False(response.IsSuccessStatusCode);
    }
}
