using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace SadGallery.Tests.Integration.Infrastructure;

/// <summary>
/// کارخانه ساخت برنامه واقعی برای تست‌های HTTP.
/// نکته: رشته اتصال تزریق می‌شود تا برنامه در شروع کار خطای «رشته اتصال تنظیم نشده» ندهد.
/// تست‌های این پروژه به دیتابیس وصل نمی‌شوند؛ فقط تست‌های <c>RequiresSqlServer</c> واقعاً وصل می‌شوند.
/// </summary>
public sealed class SadGalleryWebFactory : WebApplicationFactory<Program>
{
    /// <summary>رشته اتصال بی‌خطر برای مسیرهایی که به دیتابیس دست نمی‌زنند.</summary>
    public const string PlaceholderConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SadGallery_WebTests_NotUsed;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:SadGallery", PlaceholderConnectionString);

        // تست‌ها «هرمتیک» هستند: منبع نرخ واقعی/Fixture در تست‌های HTTP فعال نمی‌شود تا
        // هیچ درخواست خروجی و هیچ نوشتن خودکار در دیتابیس رخ ندهد. تست‌های مربوط به نمایش نرخ،
        // کش را مستقیم پر می‌کنند.
        builder.UseSetting("RateOptions:Provider", "Disabled");
    }
}
