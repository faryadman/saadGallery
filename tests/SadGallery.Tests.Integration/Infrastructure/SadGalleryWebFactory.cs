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
    public const string PlaceholderConnectionString = WebFactorySettings.PlaceholderConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder) => WebFactorySettings.Apply(builder);
}
