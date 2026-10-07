using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using SadGallery.Domain.Enums;

namespace SadGallery.Infrastructure.Market;

/// <summary>گزارش یک اجرای دریافت نرخ (برای CLI، لاگ و تست).</summary>
public sealed record RateFetchReport
{
    /// <summary>وضعیت نهایی.</summary>
    public required RateFetchStatus Status { get; init; }

    /// <summary>شناسه منبع.</summary>
    public required string ProviderId { get; init; }

    /// <summary>زمان اعلام نرخ توسط منبع.</summary>
    public DateTimeOffset? QuotedAtUtc { get; init; }

    /// <summary>زمان دریافت ما.</summary>
    public DateTimeOffset? FetchedAtUtc { get; init; }

    /// <summary>تعداد نرخ‌های قابل انتشار.</summary>
    public int AcceptedCount { get; init; }

    /// <summary>تعداد نرخ‌های علامت‌دار (فقط ثبت).</summary>
    public int FlaggedCount { get; init; }

    /// <summary>تعداد اقلام رد‌شده.</summary>
    public int RejectedCount { get; init; }

    /// <summary>تعداد کلیدهای ناشناخته پاسخ.</summary>
    public int UnknownKeyCount { get; init; }

    /// <summary>تعداد دارایی‌هایی که کلیدشان در این پاسخ نبود (رفتار مجاز طبق مستندات سرویس).</summary>
    public int MissingKeyCount { get; init; }

    /// <summary>مدت اجرا (میلی‌ثانیه).</summary>
    public int DurationMs { get; init; }

    /// <summary>آیا در دیتابیس ثبت شد؟</summary>
    public bool Persisted { get; init; }

    /// <summary>کد خطا (در صورت شکست).</summary>
    public string? ErrorCode { get; init; }

    /// <summary>پیام فارسی خلاصه (بدون داده حساس) برای CLI/گزارش.</summary>
    public string? Message { get; init; }
}

/// <summary>
/// هماهنگ‌کننده یک اجرای کامل: قفل‌ها ← دریافت ← نرمال‌سازی ← کش ← ثبت تاریخچه.
/// <para>
/// اصول پیاده‌سازی (معیارهای پذیرش فاز ۲):
/// <list type="bullet">
///   <item>قفل درون‌فرایندی: اجرای هم‌زمان در یک نمونه ممکن نیست.</item>
///   <item>قفل چند-نمونه‌ای: اجاره زمانی در دیتابیس (کرش ⇒ انقضا و آزادسازی خودکار).</item>
///   <item>حداقل فاصله بین درخواست‌های خروجی (به‌جز اجرای دستی) رعایت می‌شود.</item>
///   <item>خرابی منبع: سیستم سالم می‌ماند؛ آخرین نرخ معتبر از کش با برچسب کهنگی نمایش داده می‌شود.</item>
///   <item>خرابی دیتابیس: کش باز هم به‌روز می‌شود تا نمایش مختل نشود (با ثبت هشدار).</item>
///   <item>هر اجرا (موفق/ناموفق/ردشده) در MarketRateFetchRuns ثبت می‌شود.</item>
/// </list>
/// </para>
/// </summary>
public sealed class RateFetchOrchestrator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRateProvider _provider;
    private readonly RateSnapshotCache _cache;
    private readonly RateNormalizer _normalizer;
    private readonly RateOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<RateFetchOrchestrator> _logger;
    private readonly SemaphoreSlim _inProcessGate = new(1, 1);
    private readonly string _ownerId = $"{Environment.MachineName}:{Environment.ProcessId}";

    private DateTimeOffset? _lastFetchStartedAtUtc;

    /// <summary>ساخت هماهنگ‌کننده.</summary>
    public RateFetchOrchestrator(
        IServiceScopeFactory scopeFactory,
        IRateProvider provider,
        RateSnapshotCache cache,
        RateNormalizer normalizer,
        RateOptions options,
        IClock clock,
        ILogger<RateFetchOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _provider = provider;
        _cache = cache;
        _normalizer = normalizer;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// یک اجرای کامل. هرگز استثنای پیش‌بینی‌نشده بیرون نمی‌دهد (جز لغو واقعی).
    /// </summary>
    /// <param name="trigger">انگیزه: Startup | Scheduled | Manual.</param>
    /// <param name="cancellationToken">لغو.</param>
    public async Task<RateFetchReport> RunOnceAsync(string trigger, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);

        if (!_provider.IsConfigured)
        {
            var message = _options.IsDisabled
                ? "منبع نرخ غیرفعال است (RateOptions:Provider = Disabled)."
                : "منبع نرخ تنظیم نشده است؛ " +
                  $"متغیرهای محیطی {RateOptions.UsernameEnvironmentVariable} و {RateOptions.PasswordEnvironmentVariable} و " +
                  "دامنه مجاز (RateOptions:AllowedProviderDomains) را بررسی کنید.";

            _logger.LogWarning("اجرای دریافت نرخ انجام نشد: {Message}", message);

            return new RateFetchReport
            {
                Status = RateFetchStatus.NotConfigured,
                ProviderId = _provider.ProviderId,
                Message = message,
            };
        }

        var isManual = string.Equals(trigger, RunTriggers.Manual, StringComparison.OrdinalIgnoreCase);

        // حداقل فاصله بین درخواست‌های خروجی (احترام به محدودیت منبع) — اجرای دستی مستثناست.
        if (!isManual && _lastFetchStartedAtUtc is { } lastStart)
        {
            var sinceLast = _clock.UtcNow - lastStart;

            if (sinceLast < _options.FetchInterval)
            {
                return new RateFetchReport
                {
                    Status = RateFetchStatus.SkippedTooSoon,
                    ProviderId = _provider.ProviderId,
                    Message = $"کمتر از حداقل فاصله مجاز از درخواست قبلی ({sinceLast.TotalSeconds:F0} از {_options.FetchIntervalSeconds} ثانیه).",
                };
            }
        }

        if (!await _inProcessGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("اجرای دریافت نرخ رد شد: اجرای دیگری در همین نمونه در جریان است.");

            return new RateFetchReport
            {
                Status = RateFetchStatus.SkippedOverlap,
                ProviderId = _provider.ProviderId,
                Message = "اجرای دیگری در همین نمونه در جریان بود.",
            };
        }

        try
        {
            _lastFetchStartedAtUtc = _clock.UtcNow;

            return await RunCoreAsync(trigger, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "خطای پیش‌بینی‌نشده در اجرای دریافت نرخ.");

            return new RateFetchReport
            {
                Status = RateFetchStatus.TransportError,
                ProviderId = _provider.ProviderId,
                ErrorCode = "UnexpectedError",
                Message = "خطای پیش‌بینی‌نشده در اجرا (جزئیات در لاگ سرور).",
            };
        }
        finally
        {
            _inProcessGate.Release();
        }
    }

    private async Task<RateFetchReport> RunCoreAsync(string trigger, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        using var scope = _scopeFactory.CreateScope();
        IRateStore? store = null;
        long runId = 0;
        var persisted = true;

        try
        {
            store = scope.ServiceProvider.GetRequiredService<IRateStore>();
            runId = await store.BeginRunAsync(new RateFetchRunStart(_provider.ProviderId, trigger, _clock.UtcNow), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            persisted = false;
            _logger.LogError(exception, "ثبت شروع اجرای نرخ در دیتابیس ممکن نشد؛ اجرا ادامه می‌یابد.");
        }

        var leaseAcquired = false;

        try
        {
            if (store is not null)
            {
                try
                {
                    leaseAcquired = await store
                        .TryAcquireLeaseAsync(_ownerId, _options.EffectiveLeaseTtl, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    persisted = false;
                    _logger.LogError(exception, "دریافت قفل چند-نمونه‌ای ممکن نشد؛ اجرا ادامه می‌یابد.");
                }

                if (store is not null && !leaseAcquired && persisted)
                {
                    stopwatch.Stop();

                    await CompleteRunAsync(store, runId, RateFetchOutcomeKind.SkippedOverlap, stopwatch, 0, 0, 0, null, "SkippedOverlap", "نمونه دیگری در حال دریافت نرخ است.", cancellationToken)
                        .ConfigureAwait(false);

                    return new RateFetchReport
                    {
                        Status = RateFetchStatus.SkippedOverlap,
                        ProviderId = _provider.ProviderId,
                        Persisted = true,
                        Message = "نمونه دیگری از برنامه در حال دریافت نرخ است؛ این نوبت رد شد.",
                    };
                }
            }

            var fetchResult = await _provider.FetchAsync(cancellationToken).ConfigureAwait(false);

            return await HandleFetchResultAsync(store, runId, fetchResult, stopwatch, persisted, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (leaseAcquired && store is not null)
            {
                try
                {
                    await store.ReleaseLeaseAsync(_ownerId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "آزادسازی قفل اجرای نرخ ناموفق بود؛ با انقضای اجاره خودکار آزاد می‌شود.");
                }
            }
        }
    }

    private async Task<RateFetchReport> HandleFetchResultAsync(
        IRateStore? store,
        long runId,
        RateProviderFetchResult fetchResult,
        Stopwatch stopwatch,
        bool persisted,
        CancellationToken cancellationToken)
    {
        if (fetchResult.Status != RateFetchStatus.Success || fetchResult.Payload is null)
        {
            stopwatch.Stop();

            if (store is not null)
            {
                await CompleteRunAsync(
                    store, runId, RateFetchOutcomeKind.Failed, stopwatch, 0, 0, 0,
                    fetchResult.HttpStatusCode, fetchResult.ErrorCode, fetchResult.TechnicalDetail, cancellationToken).ConfigureAwait(false);
            }

            return new RateFetchReport
            {
                Status = fetchResult.Status,
                ProviderId = fetchResult.ProviderId,
                FetchedAtUtc = fetchResult.FetchedAtUtc,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                Persisted = persisted,
                ErrorCode = fetchResult.ErrorCode,
                Message = fetchResult.TechnicalDetail ?? "دریافت نرخ ناموفق بود.",
            };
        }

        var payload = fetchResult.Payload;
        var now = _clock.UtcNow;

        var previousAmounts = await TryLoadPreviousAmountsAsync(store, cancellationToken).ConfigureAwait(false);

        var normalization = _normalizer.Normalize(payload, fetchResult.ProviderId, fetchResult.FetchedAtUtc, now, previousAmounts);

        // نمایش هرگز نباید به دیتابیس وابسته باشد: کش پیش از تلاش برای ثبت به‌روز می‌شود.
        if (normalization.Accepted.Count > 0)
        {
            _cache.Set(fetchResult.ProviderId, normalization.Accepted, normalization.Flagged, fetchResult.FetchedAtUtc);
        }

        if (store is not null && runId > 0 && normalization.Accepted.Count + normalization.Flagged.Count > 0)
        {
            try
            {
                var all = normalization.Accepted.Concat(normalization.Flagged).ToArray();
                await store.SaveRatesAsync(runId, all, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                persisted = false;
                _logger.LogError(exception, "ثبت نرخ‌ها در دیتابیس ناموفق بود (نمایش از کش ادامه دارد).");
            }
        }

        stopwatch.Stop();

        var notes = BuildNotes(payload);

        var outcome = normalization.Accepted.Count == 0
            ? RateFetchOutcomeKind.Failed
            : normalization.Rejected.Count > 0 || normalization.Flagged.Count > 0 || payload.UnmappedKeys.Count > 0
                ? RateFetchOutcomeKind.SuccessWithProblems
                : RateFetchOutcomeKind.Success;

        if (!persisted)
        {
            outcome = RateFetchOutcomeKind.PersistenceFailed;
        }

        if (store is not null && runId > 0)
        {
            await CompleteRunAsync(
                store,
                runId,
                outcome,
                stopwatch,
                normalization.Accepted.Count,
                normalization.Flagged.Count,
                normalization.Rejected.Count,
                fetchResult.HttpStatusCode,
                fetchResult.ErrorCode,
                notes,
                cancellationToken).ConfigureAwait(false);
        }

        return new RateFetchReport
        {
            Status = fetchResult.Status,
            ProviderId = fetchResult.ProviderId,
            QuotedAtUtc = payload.QuotedAtUtc,
            FetchedAtUtc = fetchResult.FetchedAtUtc,
            AcceptedCount = normalization.Accepted.Count,
            FlaggedCount = normalization.Flagged.Count,
            RejectedCount = normalization.Rejected.Count,
            UnknownKeyCount = payload.UnmappedKeys.Count,
            MissingKeyCount = payload.MissingKeys.Count,
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            Persisted = persisted,
            ErrorCode = fetchResult.ErrorCode,
            Message = normalization.Accepted.Count == 0
                ? "هیچ نرخ معتبری از پاسخ منبع استخراج نشد."
                : $"{normalization.Accepted.Count} نرخ معتبر دریافت شد" +
                  (normalization.Flagged.Count > 0 ? $" · {normalization.Flagged.Count} نرخ علامت‌دار (فقط ثبت)" : string.Empty) +
                  (normalization.Rejected.Count > 0 ? $" · {normalization.Rejected.Count} قلم رد شد" : string.Empty),
        };
    }

    private async Task<IReadOnlyDictionary<string, decimal>?> TryLoadPreviousAmountsAsync(IRateStore? store, CancellationToken cancellationToken)
    {
        if (store is null)
        {
            return null;
        }

        try
        {
            var latest = await store.GetLatestAsync(cancellationToken).ConfigureAwait(false);

            return latest.ToDictionary(rate => rate.AssetCode, rate => rate.Amount, StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "خواندن آخرین نرخ‌ها برای تشخیص جهش ممکن نشد؛ تشخیص جهش این اجرا غیرفعال است.");
            return null;
        }
    }

    private async Task CompleteRunAsync(
        IRateStore store,
        long runId,
        RateFetchOutcomeKind outcome,
        Stopwatch stopwatch,
        int accepted,
        int flagged,
        int rejected,
        int? httpStatusCode,
        string? errorCode,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (runId <= 0)
        {
            return;
        }

        try
        {
            await store.CompleteRunAsync(
                runId,
                new RateFetchRunFinish(outcome, _clock.UtcNow, (int)stopwatch.ElapsedMilliseconds, accepted, flagged, rejected, httpStatusCode, errorCode, notes),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "تکمیل رکورد اجرای نرخ ناموفق بود.");
        }
    }

    private static string? BuildNotes(RateParseResult payload)
    {
        var parts = new List<string>();

        if (payload.UnmappedKeys.Count > 0)
        {
            parts.Add("کلیدهای ناشناخته پاسخ: " + string.Join(", ", payload.UnmappedKeys));
        }

        if (payload.MissingKeys.Count > 0)
        {
            parts.Add("کلیدهای غایب پاسخ (مجاز طبق مستندات سرویس): " + string.Join(", ", payload.MissingKeys));
        }

        var problems = payload.Problems
            .Take(6)
            .Select(problem => $"{problem.ProviderKey}={RateProblemText.ToPersian(problem.Reason)}")
            .ToArray();

        if (problems.Length > 0)
        {
            parts.Add("مشکلات اقلام: " + string.Join("؛ ", problems));
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }
}

/// <summary>انگیزه‌های اجرای دریافت نرخ.</summary>
public static class RunTriggers
{
    /// <summary>نخستین اجرا پس از راه‌اندازی برنامه.</summary>
    public const string Startup = "Startup";

    /// <summary>اجرای دوره‌ای زمان‌بند.</summary>
    public const string Scheduled = "Scheduled";

    /// <summary>اجرای دستی (CLI/اپراتور) — از حداقل فاصله مستثناست.</summary>
    public const string Manual = "Manual";
}
