using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;

namespace SadGallery.Infrastructure.Market;

/// <summary>
/// Job دوره‌ای دریافت نرخ. بازه از تنظیمات می‌آید، با <c>CancellationToken</c> خاتمه می‌یابد،
/// اجرای هم‌زمان را در دو سطح (درون‌فرایندی + چند-نمونه‌ای) می‌بندد، پس از خطا عقب‌نشینی
/// نمایی می‌کند و در حالت غیرفعال‌بودن منبع، بی‌صدا و بدون هیچ درخواست خروجی می‌ماند.
/// </summary>
public sealed class RateFetchBackgroundService : BackgroundService
{
    private readonly RateFetchOrchestrator _orchestrator;
    private readonly IRateProvider _provider;
    private readonly RateOptions _options;
    private readonly ILogger<RateFetchBackgroundService> _logger;

    /// <summary>ساخت Job.</summary>
    public RateFetchBackgroundService(
        RateFetchOrchestrator orchestrator,
        IRateProvider provider,
        RateOptions options,
        ILogger<RateFetchBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _orchestrator = orchestrator;
        _provider = provider;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_provider.IsConfigured)
        {
            _logger.LogInformation(
                "Job نرخ راه‌اندازی شد: منبع {ProviderId}، بازه {IntervalSeconds} ثانیه، کهنگی پس از {StaleMinutes} دقیقه.",
                _provider.ProviderId,
                _options.FetchIntervalSeconds,
                _options.StaleThresholdMinutes);
        }
        else
        {
            _logger.LogInformation(
                "Job نرخ غیرفعال است: منبع «{Provider}» تنظیم/فعال نشده است. هیچ درخواست خروجی ارسال نمی‌شود.",
                _options.Provider);

            return;
        }

        var baseInterval = _options.FetchInterval;
        var maxBackoff = TimeSpan.FromMinutes(_options.MaxBackoffMinutes);
        // نخستین اجرا کمی پس از بالا آمدن برنامه (تا صفحه‌ها سریع نرخ داشته باشند)، سپس بازه عادی.
        var nextDelay = TimeSpan.FromSeconds(_options.StartupDelaySeconds);
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(nextDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var report = await _orchestrator.RunOnceAsync(RunTriggers.Scheduled, stoppingToken).ConfigureAwait(false);

                consecutiveFailures = RateBackoffPolicy.IsFailure(report.Status) ? consecutiveFailures + 1 : 0;

                if (consecutiveFailures > 0)
                {
                    _logger.LogWarning(
                        "اجرای نرخ با وضعیت {Status} پایان یافت (خطای پیاپی: {Failures}). تلاش بعدی پس از عقب‌نشینی.",
                        report.Status,
                        consecutiveFailures);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                consecutiveFailures++;
                _logger.LogError(exception, "خطای پیش‌بینی‌نشده در Job نرخ.");
            }

            nextDelay = RateBackoffPolicy.NextDelay(consecutiveFailures, baseInterval, maxBackoff);
        }

        _logger.LogInformation("Job نرخ متوقف شد.");
    }
}
