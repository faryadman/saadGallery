using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SadGallery.Tests.Integration.Infrastructure;

namespace SadGallery.Tests.Integration.Infrastructure;

/// <summary>
/// کارخانهٔ وبِ ویژهٔ تست‌های کاتالوگ/رسانه (فاز ۴).
/// </summary>
/// <remarks>
/// دو تفاوت با <see cref="SadGalleryWebFactory"/>:
/// <list type="number">
///   <item>ریشهٔ نگهداریِ فایل‌ها به یک پوشهٔ موقت و یکتا برده می‌شود؛ با این کار
///   تست‌ها هرگز چیزی درون مخزنِ کد نمی‌نویسند.</item>
///   <item>اگر <c>SADGALLERY_TEST_SQL</c> تنظیم باشد، همان رشته اتصال به برنامه داده می‌شود
///   تا مسیرِ واقعیِ EF هم آزموده شود (وگرنه همچنان از رشتهٔ ساختگی استفاده می‌شود).</item>
/// </list>
/// </remarks>
public sealed class CatalogWebFactory : WebApplicationFactory<Program>
{
    public CatalogWebFactory()
    {
        StorageRoot = Path.Combine(
            Path.GetTempPath(),
            "sadgallery-media-" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>پوشهٔ موقتِ نگهداریِ فایل‌ها در این نمونهٔ آزمایشی.</summary>
    public string StorageRoot { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        WebFactorySettings.Apply(builder);
        builder.UseSetting("StorageOptions:UploadsRoot", StorageRoot);

        var connectionString = Environment.GetEnvironmentVariable(RequiresSqlServerFactAttribute.EnvironmentVariableName);

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            builder.UseSetting("ConnectionStrings:SadGallery", connectionString);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(StorageRoot))
        {
            try
            {
                Directory.Delete(StorageRoot, recursive: true);
            }
            catch (IOException)
            {
                // پاک‌سازیِ پوشهٔ موقت نباید باعث شکستِ تست شود.
            }
        }
    }
}
