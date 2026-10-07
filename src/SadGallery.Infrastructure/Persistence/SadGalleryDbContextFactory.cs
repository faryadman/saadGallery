using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SadGallery.Application.Data;

namespace SadGallery.Infrastructure.Persistence;

/// <summary>
/// Factory زمان طراحی، فقط برای ابزار <c>dotnet ef</c> (add/script/remove).
/// نکته: دستورهای <c>migrations add</c> و <c>migrations script</c> به دیتابیس **وصل نمی‌شوند**؛
/// این رشته اتصال صرفاً برای ساخت مدل کافی است و هیچ راز واقعی اینجا نیست.
/// برای دستورهایی که واقعاً به دیتابیس وصل می‌شوند (مثل <c>database update</c>)،
/// مقدار را از متغیر محیطی <c>SADGALLERY_CONNECTION</c> یا User Secrets بدهید.
/// </summary>
/// <remarks>
/// اگر متغیر تنظیم شده باشد اما شکلش نامعتبر باشد، با پیام فارسی و راهنما متوقف می‌شود
/// (پیش‌تر پیام مبهم SqlClient ظاهر می‌شد — BUG-009).
/// </remarks>
public sealed class SadGalleryDbContextFactory : IDesignTimeDbContextFactory<SadGalleryDbContext>
{
    private const string DesignTimeFallbackConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SadGallery_DesignTime;Trusted_Connection=True;TrustServerCertificate=True";

    public SadGalleryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SADGALLERY_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DesignTimeFallbackConnectionString;
        }
        else
        {
            var guardError = ConnectionStringGuard.Validate(connectionString);

            if (guardError is not null)
            {
                throw new InvalidOperationException(
                    guardError + Environment.NewLine +
                    "  (این پیام از مسیر «طرح‌زمان» dotnet ef می‌آید؛ متغیر SADGALLERY_CONNECTION را اصلاح کنید.)");
            }
        }

        var options = new DbContextOptionsBuilder<SadGalleryDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(SadGalleryDbContext).Assembly.FullName))
            .Options;

        return new SadGalleryDbContext(options);
    }
}
