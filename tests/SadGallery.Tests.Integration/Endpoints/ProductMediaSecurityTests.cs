using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Media;
using SadGallery.Domain.Enums;
using SadGallery.Infrastructure.Media;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

/// <summary>
/// تست‌های تفکیکِ «تصاویر عمومی» از «فایل‌های خصوصی» (فاز ۴)، بدون نیاز به دیتابیس.
/// </summary>
/// <remarks>
/// معیارِ پذیرش: «تصاویر عمومی و فایل‌های خصوصی از هم جدا» و «ذخیره بیرون از پوشهٔ ارائه‌شده».
/// این تست‌ها نشان می‌دهند که فایلی که در زیرپوشهٔ خصوصی است، از مسیر ایستا در دسترس نیست،
/// در حالی که تصویرِ عمومی در دسترس است.
/// </remarks>
public sealed class ProductMediaSecurityTests : IClassFixture<CatalogWebFactory>
{
    private readonly CatalogWebFactory _factory;

    public ProductMediaSecurityTests(CatalogWebFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    [Fact]
    public async Task PrivateFile_IsNeverReachableThroughStaticMediaPath()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // ذخیرهٔ یک فایل در بخشِ خصوصی (همان مسیری که فایل‌های حساسِ آینده می‌روند)
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMediaStore>();
        var options = scope.ServiceProvider.GetRequiredService<MediaOptions>();
        var secretName = MediaFileNaming.NewStoredName(ImageFormat.Png);

        await store.SaveAsync(
            MediaKind.PrivateFile,
            secretName,
            System.Text.Encoding.UTF8.GetBytes("این یک فایل خصوصی است"),
            cancellationToken);

        // فایل روی دیسک وجود دارد…
        Assert.True(store.Exists(MediaKind.PrivateFile, secretName));

        // …اما از مسیرِ ایستای تصاویر محصول هرگز در دسترس نیست.
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync($"{options.PublicRequestPath}/{secretName}", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PublicImage_IsReachableThroughStaticMediaPath()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMediaStore>();
        var options = scope.ServiceProvider.GetRequiredService<MediaOptions>();
        var publicName = MediaFileNaming.NewStoredName(ImageFormat.Png);

        await store.SaveAsync(
            MediaKind.PublicImage,
            publicName,
            System.Text.Encoding.UTF8.GetBytes("یک تصویر عمومی"),
            cancellationToken);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync($"{options.PublicRequestPath}/{publicName}", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void PublicAndPrivateRoots_AreSeparateDirectories()
    {
        using var scope = _factory.Services.CreateScope();
        var localStore = scope.ServiceProvider.GetRequiredService<LocalMediaStore>();

        Assert.NotEqual(localStore.PublicRoot, localStore.PrivateRoot);
        Assert.StartsWith(_factory.StorageRoot, localStore.PublicRoot, StringComparison.Ordinal);
        Assert.StartsWith(_factory.StorageRoot, localStore.PrivateRoot, StringComparison.Ordinal);
    }

    [Fact]
    public void StorageRoot_IsOutsideTheServedWebRoot()
    {
        // معیارِ پذیرش: «ذخیره بیرون از پوشهٔ کد/ارائه‌شده».
        using var scope = _factory.Services.CreateScope();
        var localStore = scope.ServiceProvider.GetRequiredService<LocalMediaStore>();
        var environment = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();

        var webRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));

        Assert.False(localStore.PublicRoot.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase));
        Assert.False(localStore.PrivateRoot.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Store_RefusesNamesThatAreNotSystemGenerated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMediaStore>();

        // تلاش برای نوشتن با نامی دلخواه (سناریوی عبور از مسیر)
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(MediaKind.PublicImage, "../../evil.png", new byte[] { 1, 2, 3 }, cancellationToken));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(MediaKind.PublicImage, "photo.jpg", new byte[] { 1, 2, 3 }, cancellationToken));
    }

    [Fact]
    public async Task StoredImages_GetLongLivedImmutableCacheHeader()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMediaStore>();
        var options = scope.ServiceProvider.GetRequiredService<MediaOptions>();
        var name = MediaFileNaming.NewStoredName(ImageFormat.Png);

        await store.SaveAsync(MediaKind.PublicImage, name, new byte[] { 1, 2, 3 }, cancellationToken);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"{options.PublicRequestPath}/{name}", cancellationToken);

        // نام فایل‌ها تصادفی و محتوا تغییرناپذیر است؛ کشِ طولانی امن است.
        Assert.Contains("immutable", response.Headers.CacheControl?.ToString() ?? "");
    }
}
