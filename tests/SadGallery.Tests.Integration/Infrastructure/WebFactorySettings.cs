using Microsoft.AspNetCore.Hosting;

namespace SadGallery.Tests.Integration.Infrastructure;

/// <summary>تنظیمات مشترک همه کارخانه‌های تست وب (تست‌ها هرметиک‌اند: بدون تماس خارجی).</summary>
public static class WebFactorySettings
{
    /// <summary>رشته اتصال بی‌خطر برای مسیرهایی که به دیتابیس دست نمی‌زنند.</summary>
    public const string PlaceholderConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SadGallery_WebTests_NotUsed;Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>اعمال تنظیمات پایه روی هر کارخانه تست.</summary>
    public static void Apply(IWebHostBuilder builder)
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
