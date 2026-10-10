using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Data;
using SadGallery.Infrastructure.Identity;
using SadGallery.Application.Catalog;
using SadGallery.Application.Market;
using SadGallery.Application.Media;
using SadGallery.Infrastructure.Catalog;
using SadGallery.Infrastructure.Market;
using SadGallery.Infrastructure.Media;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Time;
using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// ثبت زنجیره نرخ بازار (فاز ۲): تنظیمات، سیاست‌ها، کش، مخزن، منبع، هماهنگ‌کننده و Job.
    /// انتخاب منبع با <c>RateOptions:Provider</c> انجام می‌شود؛ در حالت غیرفعال/تنظیم‌نشده،
    /// هیچ درخواست خروجی ارسال نمی‌شود (fail-closed).
    /// </summary>
    public static IServiceCollection AddSadGalleryMarket(this IServiceCollection services, RateOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton(RateFreshnessPolicy.FromOptions(options));
        services.AddSingleton(new RateAnomalyDetector(options.AnomalyChangeThresholdPercent));
        services.AddSingleton<RateSnapshotCache>();
        services.AddSingleton<RateNormalizer>();
        services.AddScoped<IRateStore, RateStore>();
        services.AddSingleton<RateFetchOrchestrator>();

        if (options.IsFixtureProvider)
        {
            // فقط در توسعه/تست (فعال‌سازی در محیط غیرتوسعه در لایه Web رد می‌شود).
            services.AddSingleton<IRateProvider>(provider =>
                new FixtureRateProvider(provider.GetRequiredService<IClock>(), options));
        }
        else
        {
            services.AddSingleton(CreateRateHttpClient(options));
            services.AddSingleton<IRateProvider>(provider => new TgnRateProvider(
                provider.GetRequiredService<HttpClient>(),
                options,
                provider.GetRequiredService<IClock>(),
                provider.GetRequiredService<ILogger<TgnRateProvider>>()));
        }

        services.AddHostedService<RateFetchBackgroundService>();

        return services;
    }

    /// <summary>
    /// ثبت کاتالوگ محصول و خطِ لولهٔ تصویر (فاز ۴).
    /// </summary>
    /// <remarks>
    /// تنظیمات از دو بخش <c>StorageOptions</c> و <c>ProductOptions</c> خوانده می‌شوند.
    /// مسیر ذخیرهٔ فایل‌ها هرگز در کد ثابت نیست و در استقرار باید به مسیری
    /// بیرون از پوشهٔ برنامه اشاره کند (راهنما: docs/DEPLOYMENT.md).
    /// </remarks>
    public static IServiceCollection AddSadGalleryCatalog(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // بخشِ تنظیمات همان StorageOptions است که از فاز صفر در appsettings بود؛
        // ساختنِ بخشِ دومِ هم‌پوشان (MediaOptions) باعث سردرگمی در استقرار می‌شد.
        var mediaOptions = new MediaOptions();
        configuration.GetSection("StorageOptions").Bind(mediaOptions);

        var productOptions = new ProductOptions();
        configuration.GetSection("ProductOptions").Bind(productOptions);

        services.AddSingleton(mediaOptions);
        services.AddSingleton(productOptions);

        // پردازشگر تصویر بی‌حالت (Stateless) و سنگین است ⇒ Singleton.
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();

        // مخزن فایل دو بار ثبت می‌شود: یک‌بار با نوعِ مشخص (برای پیکربندی مسیر ایستا در لایه وب)
        // و یک‌بار با واسط (برای استفاده در سرویس‌ها).
        services.AddSingleton<LocalMediaStore>();
        services.AddSingleton<IMediaStore>(provider => provider.GetRequiredService<LocalMediaStore>());

        services.AddSingleton<MediaUploadService>();
        services.AddScoped<IProductStore, ProductStore>();
        services.AddScoped<IProductPriceCalculator, ProductPriceCalculator>();
        services.AddScoped<ProductService>();

        return services;
    }

    /// <summary>
    /// ساخت HttpClient اختصاصی منبع نرخ.
    /// نکته امنیتی: از IHttpClientFactory استفاده نمی‌شود تا هیچ لاگ خودکاری از «آدرس درخواست»
    /// (که اعتبارنامه در مسیر آن است) تولید نشود؛ مهلت هم با CancellationToken در Provider اعمال می‌شود.
    /// </summary>
    private static HttpClient CreateRateHttpClient(RateOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(Math.Min(options.HttpTimeoutSeconds, 10)),
        };

        var client = new HttpClient(handler)
        {
            // مهلت واقعی در Provider با CTS اعمال می‌شود؛ اینجا بی‌نهایت تا تداخل نداشته باشد.
            Timeout = Timeout.InfiniteTimeSpan,
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("SadGallery/1.0 (+internal-rate-fetch)");

        return client;
    }
}
