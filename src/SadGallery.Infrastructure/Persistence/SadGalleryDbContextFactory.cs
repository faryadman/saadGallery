using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SadGallery.Infrastructure.Persistence;

/// <summary>
/// Factory زمان طراحی، فقط برای ابزار <c>dotnet ef</c> (add/script/remove).
/// نکته: دستورهای <c>migrations add</c> و <c>migrations script</c> به دیتابیس **وصل نمی‌شوند**؛
/// این رشته اتصال صرفاً برای ساخت مدل کافی است و هیچ راز واقعی اینجا نیست.
/// برای دستورهایی که واقعاً به دیتابیس وصل می‌شوند (مثل <c>database update</c>)،
/// مقدار را از متغیر محیطی <c>SADGALLERY_CONNECTION</c> یا User Secrets بدهید.
/// </summary>
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

        var options = new DbContextOptionsBuilder<SadGalleryDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(SadGalleryDbContext).Assembly.FullName))
            .Options;

        return new SadGalleryDbContext(options);
    }
}
