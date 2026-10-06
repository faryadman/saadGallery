using System.Net;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

public sealed class HealthEndpointTests : IClassFixture<SadGalleryWebFactory>
{
    private readonly SadGalleryWebFactory _factory;

    public HealthEndpointTests(SadGalleryWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Liveness_DoesNotDependOnDatabase_AndReturnsHealthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);
        var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task Readiness_ReportsDatabaseState_WithoutLeakingDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), cancellationToken);
        var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

        // بدون SQL Server ⇒ ناسالم (503). روی ماشین دارای دیتابیس ⇒ 200. در هیچ حالتی 500 یا پیام افشاگر.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable });
        Assert.Contains(body, new[] { "Healthy", "Unhealthy", "Degraded" });
        Assert.DoesNotContain("Server=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }
}
