using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Application.Abstractions;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Time;

namespace SadGallery.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// ثبت زیرساخت‌ها: DbContext (SQL Server)، زمان و مقدارگذاری نقش‌ها.
    /// رشته اتصال فقط از Configuration خوانده می‌شود (User Secrets در Dev،
    /// متغیر محیطی/Secret Store در Prod). هیچ مقدار پیش‌فرض رازی در کد نیست.
    /// </summary>
    public static IServiceCollection AddSadGalleryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("SadGallery");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'SadGallery' is not configured. " +
                "In development use User Secrets (dotnet user-secrets). " +
                "In production use environment variables or a secret store. " +
                "See docs/DEPLOYMENT.md.");
        }

        services.AddDbContext<SadGalleryDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                // تاب‌آوری در برابر خطاهای گذرای شبکه/دیتابیس (مهم در سرور واقعی)
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            }));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IIdentitySeeder, IdentitySeeder>();

        return services;
    }
}
