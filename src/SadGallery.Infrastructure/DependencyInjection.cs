using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Data;
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

        // لایه ۱ — بررسی «شکل» رشته: خالی، جای‌نگهدار، کوتیشن‌دار، بدون کلید Server/Database …
        // تا به‌جای پیام مبهم SqlClient («starting at index 0») راهنمای عملی بدهیم (BUG-009).
        var guardError = ConnectionStringGuard.Validate(connectionString);

        if (guardError is not null)
        {
            throw new InvalidOperationException(guardError);
        }

        // لایه ۲ — بررسی نهایی با پارسر خود SqlClient (خطاهای عمیق‌تر مانند نقل‌قول بسته‌نشده)
        try
        {
            _ = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "رشته اتصال «" + ConnectionStringGuard.ConfigurationKey +
                "» با پارسر SQL Server سازگار نیست (نوع خطا: " + exception.GetType().Name + "). " +
                "نمونهٔ صحیح: Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True " +
                "— راهنما: docs/DEPLOYMENT.md §۴.۵ (مقدار رشته اتصال به‌عمد چاپ نمی‌شود).",
                exception);
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
