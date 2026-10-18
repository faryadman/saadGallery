using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using SadGallery.Tests.Integration.Infrastructure;
using SadGallery.Web.Pwa;
using Xunit;

namespace SadGallery.Tests.Integration.Pwa;

public sealed class PwaEndpointTests : IClassFixture<SadGalleryWebFactory>
{
    private readonly SadGalleryWebFactory factory;
    public PwaEndpointTests(SadGalleryWebFactory factory) => this.factory = factory;
    [Fact]
    public async Task ManifestAndPublicAssetsExistAndMatchTheWorkersIntegrityList()
    {
        using var client = factory.CreateClient(); var ct = TestContext.Current.CancellationToken;
        using var manifest = JsonDocument.Parse(await client.GetStringAsync("/manifest.webmanifest", ct));
        Assert.Equal("standalone", manifest.RootElement.GetProperty("display").GetString());
        Assert.Equal("rtl", manifest.RootElement.GetProperty("dir").GetString());
        Assert.Equal("/", manifest.RootElement.GetProperty("id").GetString());
        Assert.Contains(manifest.RootElement.GetProperty("icons").EnumerateArray(), x => x.GetProperty("purpose").GetString() == "maskable");
        var assets = factory.Services.GetRequiredService<PwaAssets>();
        foreach(var asset in assets.Assets)
        {
            using var response = await client.GetAsync(asset.Path, ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            Assert.Equal(asset.Integrity, "sha256-" + Convert.ToBase64String(SHA256.HashData(bytes)));
        }
        using var worker = await client.GetAsync("/service-worker.js", ct);
        Assert.Equal("application/javascript", worker.Content.Headers.ContentType!.MediaType); Assert.True(worker.Headers.CacheControl!.NoStore);
        Assert.Equal("/", Assert.Single(worker.Headers.GetValues("Service-Worker-Allowed")));
        Assert.Contains(assets.BuildId, await worker.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
    }
    [Fact]
    public async Task GuestHtmlContainingAntiforgeryTokensIsNeverHttpCached()
    {
        using var client = factory.CreateClient(); var ct = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync("/market/gold", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("__RequestVerificationToken", await response.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl!.NoStore); Assert.True(response.Headers.CacheControl.Private);
    }
    [Fact]
    public async Task PublicRateSnapshotIsLimitedFormattedAndDoesNotSerializePrivateOrProviderData()
    {
        var cache = factory.Services.GetRequiredService<RateSnapshotCache>();
        var now = DateTimeOffset.UtcNow;
        cache.Set("ProviderCredentialCanaryMustNotLeak", [new NormalizedRate
        { AssetCode = "GOLD_GRAM_18", Title = "اختبار", Amount = 1234567.5m, QuoteUnit = CurrencyUnit.Irt,
            ProviderId = "ProviderCredentialCanaryMustNotLeak", QuotedAtUtc = now, FetchedAtUtc = now, Quality = RateQuality.Live }], [], now);
        using var client = factory.CreateClient(); var ct = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync("/pwa/rates", ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("rates-v1", Assert.Single(response.Headers.GetValues("X-SadGallery-Public")));
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var text = await response.Content.ReadAsStringAsync(ct); Assert.DoesNotContain("ProviderCredentialCanaryMustNotLeak", text, StringComparison.Ordinal);
        Assert.DoesNotContain("RequestVerificationToken", text, StringComparison.Ordinal); Assert.DoesNotContain("User", text, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(text); Assert.Equal(1, json.RootElement.GetProperty("schema").GetInt32());
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("۱٬۲۳۴٬۵۶۷٫۵۰", item.GetProperty("amount").GetString()); Assert.Equal("تومان", item.GetProperty("unit").GetString());
        Assert.Equal(now.AddHours(24), item.GetProperty("expiresAtUtc").GetDateTimeOffset());
    }
}
