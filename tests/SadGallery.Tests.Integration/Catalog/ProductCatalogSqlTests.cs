using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Catalog;
using SadGallery.Application.Market;
using SadGallery.Application.Media;
using SadGallery.Domain.Catalog;
using SadGallery.Domain.Enums;
using SadGallery.Infrastructure.Catalog;
using SadGallery.Infrastructure.Media;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;
using SadGallery.Tests.Integration.Infrastructure;
using SkiaSharp;
using Xunit;

namespace SadGallery.Tests.Integration.Catalog;

/// <summary>
/// تست‌های زندهٔ کاتالوگ و بارگذاری روی SQL Server واقعی.
/// </summary>
/// <remarks>
/// در نبودِ متغیرِ <c>SADGALLERY_TEST_SQL</c> این تست‌ها صریحاً Skip می‌شوند
/// (نه سبزِ کاذب — همان سیاستِ پروژه در docs/TESTING.md).
/// روی محیط شما با این متغیر اجرا می‌شوند و مسیرِ کاملِ EF + دیسک + HTTP را می‌سنجند.
/// </remarks>
public sealed class ProductCatalogSqlTests : IClassFixture<CatalogWebFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly CatalogWebFactory _factory;

    public ProductCatalogSqlTests(CatalogWebFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    // ─────────── انتشار و دیده‌شدن ───────────

    [RequiresSqlServerFact]
    public async Task UnpublishedProduct_IsNotFoundForPublic_AndHiddenFromPublicList()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var title = UniqueTitle("پیش‌نویس");

        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken);
            var service = CreateProductService(db);

            var outcome = await service.CreateAsync(
                Draft(title, published: false),
                actorUserId: 1,
                cancellationToken);

            Assert.True(outcome.Succeeded, string.Join(" | ", outcome.Errors));

            // درخواست مستقیم با شناسهٔ واقعی باید ۴۰۴ بدهد (معیار پذیرش فاز ۴)
            using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

            var detail = await client.GetAsync($"/products/{outcome.ProductId}", cancellationToken);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, detail.StatusCode);

            var list = await client.GetAsync("/products", cancellationToken);
            var html = await list.Content.ReadAsStringAsync(cancellationToken);
            Assert.DoesNotContain(title, html, StringComparison.Ordinal);

            await CleanupAsync(db, outcome.ProductId!.Value, cancellationToken);
        }
    }

    [RequiresSqlServerFact]
    public async Task PublishedProduct_IsVisibleInPublicCatalog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var title = UniqueTitle("منتشرشده");

        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken);
            var service = CreateProductService(db);

            var outcome = await service.CreateAsync(Draft(title, published: true), 1, cancellationToken);
            Assert.True(outcome.Succeeded, string.Join(" | ", outcome.Errors));

            using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

            var detail = await client.GetAsync($"/products/{outcome.ProductId}", cancellationToken);
            Assert.Equal(System.Net.HttpStatusCode.OK, detail.StatusCode);

            var html = await detail.Content.ReadAsStringAsync(cancellationToken);
            Assert.Contains(title, html, StringComparison.Ordinal);

            await CleanupAsync(db, outcome.ProductId!.Value, cancellationToken);
        }
    }

    [RequiresSqlServerFact]
    public async Task SoftDeletedProduct_DisappearsFromPublicCatalog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var title = UniqueTitle("حذف‌شده");

        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken);
            var service = CreateProductService(db);

            var outcome = await service.CreateAsync(Draft(title, published: true), 1, cancellationToken);
            var id = outcome.ProductId!.Value;

            using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

            Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync($"/products/{id}", cancellationToken)).StatusCode);

            await service.SoftDeleteAsync(id, 1, cancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync($"/products/{id}", cancellationToken)).StatusCode);

            await CleanupAsync(db, id, cancellationToken);
        }
    }

    // ─────────── بارگذاری تصویر ───────────

    [RequiresSqlServerFact]
    public async Task Upload_RejectsExecutableDisguisedAsJpeg_AndStoresNoFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var title = UniqueTitle("بارگذاری-مخرب");

        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken);
            var service = CreateProductService(db);
            var outcome = await service.CreateAsync(Draft(title, published: false), 1, cancellationToken);
            var id = outcome.ProductId!.Value;

            // یک فایل اجرایی واقعی با نام و نوعِ تصویر
            var executable = new byte[8192];
            System.Security.Cryptography.RandomNumberGenerator.Fill(executable);
            executable[0] = 0x4D;
            executable[1] = 0x5A;

            var before = Directory.Exists(_factory.StorageRoot)
                ? Directory.GetFiles(_factory.StorageRoot, "*", SearchOption.AllDirectories).Length
                : 0;

            var upload = await service.AddImageAsync(
                id,
                new UploadCandidate("photo.jpg", "image/jpeg", executable.Length, executable),
                actorUserId: 1,
                cancellationToken);

            Assert.False(upload.Succeeded);
            Assert.NotNull(upload.Error);
            Assert.Contains("تصویر معتبر نیست", upload.Error, StringComparison.Ordinal);

            var after = Directory.Exists(_factory.StorageRoot)
                ? Directory.GetFiles(_factory.StorageRoot, "*", SearchOption.AllDirectories).Length
                : 0;

            // هیچ فایلی روی دیسک نوشته نشده باشد
            Assert.Equal(before, after);
            Assert.Empty(await service.GetImagesAsync(id, cancellationToken));

            await CleanupAsync(db, id, cancellationToken);
        }
    }

    [RequiresSqlServerFact]
    public async Task Upload_AcceptsRealImage_CreatesThumbnail_AndRecordsAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var title = UniqueTitle("بارگذاری-واقعی");
        var image = RealPng(1200, 900);

        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken);
            var service = CreateProductService(db);
            var outcome = await service.CreateAsync(Draft(title, published: false), 1, cancellationToken);
            var id = outcome.ProductId!.Value;

            var upload = await service.AddImageAsync(
                id,
                new UploadCandidate("عکس محصول.png", "image/png", image.Length, image),
                actorUserId: 1,
                cancellationToken);

            Assert.True(upload.Succeeded, upload.Error);

            var images = await service.GetImagesAsync(id, cancellationToken);
            var stored = Assert.Single(images);

            // نام تصادفیِ تولیدشده توسط سامانه (نه نام کاربر)
            Assert.True(MediaFileNaming.IsWellFormed(stored.StoredFileName));
            Assert.True(MediaFileNaming.IsWellFormed(stored.ThumbnailFileName));
            Assert.NotEqual(stored.StoredFileName, stored.ThumbnailFileName);
            Assert.DoesNotContain("عکس محصول", stored.StoredFileName, StringComparison.Ordinal);

            // ممیزی: چه کسی، کِی، با چه نام و نوعِ ادعایی
            // نامِ بارگذار از جدول کاربران (مرجعِ هویت) خوانده می‌شود، نه از رشته‌ای که
            // کلاینت فرستاده است؛ بنابراین در اینجا مقدارش به وجود کاربر شمارهٔ ۱ وابسته است
            // و تنها ثبتِ زمان و نام/نوعِ ادعایی به‌طور قطعی سنجیده می‌شود.
            Assert.NotEqual(default, stored.UploadedAtUtc);
            Assert.Equal("عکس محصول.png", stored.OriginalFileName);
            Assert.Equal("image/png", stored.ClaimedContentType);

            // بهینه‌سازی: بندانگشتی کوچک‌تر از نسخهٔ کامل است
            Assert.True(stored.Width <= 1600);
            Assert.True(stored.SizeBytes <= image.Length);

            // فایل‌های واقعی روی دیسک
            var mediaStore = CreateMediaStore();
            Assert.True(mediaStore.Exists(MediaKind.PublicImage, stored.StoredFileName));
            Assert.True(mediaStore.Exists(MediaKind.PublicImage, stored.ThumbnailFileName));

            var thumbPath = mediaStore.GetPhysicalPath(MediaKind.PublicImage, stored.ThumbnailFileName);
            var fullPath = mediaStore.GetPhysicalPath(MediaKind.PublicImage, stored.StoredFileName);
            Assert.True(new FileInfo(thumbPath).Length < new FileInfo(fullPath).Length);

            await CleanupAsync(db, id, cancellationToken);
        }
    }

    [RequiresSqlServerFact]
    public async Task Upload_EnforcesMaximumImagesPerProduct()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var title = UniqueTitle("محدودیت-تعداد");
        var image = RealPng(400, 300);
        var options = new MediaOptions { UploadsRoot = _factory.StorageRoot, MaxImagesPerProduct = 2 };

        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken);
            var service = CreateProductService(db, options);
            var outcome = await service.CreateAsync(Draft(title, published: false), 1, cancellationToken);
            var id = outcome.ProductId!.Value;

            for (var i = 0; i < 2; i++)
            {
                var ok = await service.AddImageAsync(
                    id,
                    new UploadCandidate($"photo{i}.png", "image/png", image.Length, image),
                    1,
                    cancellationToken);

                Assert.True(ok.Succeeded, ok.Error);
            }

            var third = await service.AddImageAsync(
                id,
                new UploadCandidate("third.png", "image/png", image.Length, image),
                1,
                cancellationToken);

            Assert.False(third.Succeeded);
            Assert.Contains("بیشینه", third.Error!, StringComparison.Ordinal);
            Assert.Equal(2, (await service.GetImagesAsync(id, cancellationToken)).Count);

            await CleanupAsync(db, id, cancellationToken);
        }
    }

    [RequiresSqlServerFact]
    public async Task RemovingImage_DeletesBothFilesFromDisk()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var title = UniqueTitle("حذف-تصویر");
        var image = RealPng(500, 400);

        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(cancellationToken);
            var service = CreateProductService(db);
            var outcome = await service.CreateAsync(Draft(title, published: false), 1, cancellationToken);
            var id = outcome.ProductId!.Value;

            await service.AddImageAsync(
                id,
                new UploadCandidate("photo.png", "image/png", image.Length, image),
                1,
                cancellationToken);

            var stored = Assert.Single(await service.GetImagesAsync(id, cancellationToken));
            var mediaStore = CreateMediaStore();

            Assert.True(mediaStore.Exists(MediaKind.PublicImage, stored.StoredFileName));

            var removal = await service.RemoveImageAsync(id, stored.Id, 1, cancellationToken);
            Assert.True(removal.Succeeded, removal.Error);

            // ردیف و هر دو فایل باید از بین رفته باشند
            Assert.Empty(await service.GetImagesAsync(id, cancellationToken));
            Assert.False(mediaStore.Exists(MediaKind.PublicImage, stored.StoredFileName));
            Assert.False(mediaStore.Exists(MediaKind.PublicImage, stored.ThumbnailFileName));

            await CleanupAsync(db, id, cancellationToken);
        }
    }

    // ─────────── ابزارها ───────────

    private static string UniqueTitle(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static ProductDraft Draft(string title, bool published) => new(
        Title: title,
        Summary: null,
        Description: null,
        CategoryId: null,
        PricePolicy: PricePolicy.QuoteOnly,
        FixedPriceIrt: null,
        WeightGrams: null,
        Karat: null,
        MakingChargePercent: 0m,
        ProfitPercent: ProductPriceFormula.DefaultProfitPercent,
        TaxPercent: ProductPriceFormula.DefaultTaxPercent,
        IsInStock: true,
        IsPublished: published);

    private static byte[] RealPng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Gold);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);

        return data!.ToArray();
    }

    private LocalMediaStore CreateMediaStore(MediaOptions? options = null) =>
        new(options ?? new MediaOptions { UploadsRoot = _factory.StorageRoot }, new TestHostEnvironment());

    private ProductService CreateProductService(SadGalleryDbContext db, MediaOptions? mediaOptions = null)
    {
        var options = mediaOptions ?? new MediaOptions { UploadsRoot = _factory.StorageRoot };
        var mediaStore = CreateMediaStore(options);
        var cache = new RateSnapshotCache();

        return new ProductService(
            store: new ProductStore(db),
            prices: new ProductPriceCalculator(cache),
            uploads: new MediaUploadService(new SkiaImageProcessor(), mediaStore, options),
            media: mediaStore,
            clock: new TestClock(Now),
            productOptions: new ProductOptions(),
            mediaOptions: options);
    }

    private static SadGalleryDbContext CreateContext()
    {
        var connectionString = RequiresSqlServerFactAttribute.ConnectionString
                               ?? throw new InvalidOperationException("SQL test connection string is not configured.");

        var builder = new DbContextOptionsBuilder<SadGalleryDbContext>()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(60));

        return new SadGalleryDbContext(builder.Options);
    }

    private static async Task CleanupAsync(
        SadGalleryDbContext db,
        int productId,
        CancellationToken cancellationToken)
    {
        var images = await db.ProductImages.Where(image => image.ProductId == productId).ToListAsync(cancellationToken);
        db.ProductImages.RemoveRange(images);

        var product = await db.Products.FirstOrDefaultAsync(item => item.Id == productId, cancellationToken);

        if (product is not null)
        {
            db.Products.Remove(product);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed class TestClock(DateTimeOffset now) : SadGallery.Application.Abstractions.IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string ApplicationName { get; set; } = "SadGallery.Tests";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public string EnvironmentName { get; set; } = "Development";
    }
}
