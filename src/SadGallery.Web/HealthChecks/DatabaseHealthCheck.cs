using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SadGallery.Infrastructure.Persistence;

namespace SadGallery.Web.HealthChecks;

/// <summary>
/// بررسی سلامت دیتابیس برای نقطه پایانی آمادگی (<c>/health/ready</c>).
/// نکته امنیتی: جزئیات خطا هرگز در پاسخ HTTP بازگردانده نمی‌شود
/// (پیام استثنا می‌تواند نام سرور/کاربر دیتابیس را افشا کند)؛ فقط وضعیت سالم/ناسالم اعلام می‌شود.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly SadGalleryDbContext _dbContext;

    public DatabaseHealthCheck(SadGalleryDbContext dbContext) => _dbContext = dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);

            return canConnect
                ? HealthCheckResult.Healthy("Database is reachable.")
                : HealthCheckResult.Unhealthy("Database is not reachable.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", exception);
        }
    }
}
