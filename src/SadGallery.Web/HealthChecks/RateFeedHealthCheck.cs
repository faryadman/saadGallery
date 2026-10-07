using Microsoft.Extensions.Diagnostics.HealthChecks;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using SadGallery.Application.Text;
using SadGallery.Domain.Enums;

namespace SadGallery.Web.HealthChecks;

/// <summary>
/// پایش سلامت «منبع نرخ» (معیار پذیرش فاز ۲).
/// <list type="bullet">
///   <item>Healthy: آخرین نرخ‌ها در بازه موردانتظار هستند.</item>
///   <item>Degraded: نرخ‌ها تأخیری/کهنه‌اند، یا هنوز هیچ اجرایی دریافت نشده است.</item>
///   <item>Unhealthy: نرخ‌ها فراتر از حد مجاز کهنه‌اند یا هیچ نرخ معتبری وجود ندارد.</item>
/// </list>
/// منبع غیرفعال (Disabled) خطا تلقی نمی‌شود، اما وضعیت آن صریح گزارش می‌شود.
/// </summary>
public sealed class RateFeedHealthCheck : IHealthCheck
{
    private readonly RateOptions _options;
    private readonly IRateProvider _provider;
    private readonly RateSnapshotCache _cache;
    private readonly IClock _clock;
    private readonly RateFreshnessPolicy _freshness;

    /// <summary>ساخت بررسی‌کننده سلامت.</summary>
    public RateFeedHealthCheck(RateOptions options, IRateProvider provider, RateSnapshotCache cache, IClock clock, RateFreshnessPolicy freshness)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(freshness);

        _options = options;
        _provider = provider;
        _cache = cache;
        _clock = clock;
        _freshness = freshness;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["provider"] = _options.Provider,
            ["configured"] = _provider.IsConfigured,
        };

        if (_options.IsDisabled || !_provider.IsConfigured)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                _options.IsDisabled
                    ? "منبع نرخ غیرفعال است (نمایش نرخ انجام نمی‌شود)."
                    : "منبع نرخ فعال است اما تنظیم نشده (اعتبارنامه/دامنه). هیچ درخواست خروجی ارسال نمی‌شود.",
                data: data));
        }

        var snapshot = _cache.Get();

        if (snapshot is null || snapshot.Publishable.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "هنوز نرخ معتبری دریافت و ثبت نشده است.",
                data: data));
        }

        var now = _clock.UtcNow;
        var worst = snapshot.Publishable.Max(rate => _freshness.Evaluate(rate.QuotedAtUtc, now));

        data["items"] = snapshot.Publishable.Count;
        data["lastFetchedUtc"] = snapshot.FetchedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        data["lastQuotedPersian"] = PersianDate.ToJalaliDateTimeText(snapshot.Publishable.Max(rate => rate.QuotedAtUtc));

        return Task.FromResult(worst switch
        {
            RateQuality.Live => HealthCheckResult.Healthy(
                $"منبع نرخ سالم است؛ {snapshot.Publishable.Count} نرخ در بازه موردانتظار.", data: data),

            RateQuality.Delayed => HealthCheckResult.Degraded(
                "نرخ‌ها با تأخیر دریافت شده‌اند (نمایش با برچسب تأخیری).", data: data),

            RateQuality.Stale => HealthCheckResult.Degraded(
                "ارتباط با منبع نرخ قطع است؛ نمایش «آخرین نرخ ثبت‌شده».", data: data),

            _ => HealthCheckResult.Unhealthy(
                "نرخ‌های موجود فراتر از حد مجاز کهنه‌اند و نمایش داده نمی‌شوند.", data: data),
        });
    }
}
