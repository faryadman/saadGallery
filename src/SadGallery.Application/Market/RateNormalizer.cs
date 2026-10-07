using SadGallery.Domain.Enums;
using SadGallery.Domain.Market;

namespace SadGallery.Application.Market;

/// <summary>دلیل رد یک نرخ در مرحله نرمال‌سازی.</summary>
public enum RateRejectionReason
{
    /// <summary>مقدار صفر یا منفی (بی‌معنا برای نرخ).</summary>
    NonPositive = 1,

    /// <summary>مقدار خارج از بازه معقول — احتمالاً واحد یا مقیاس اشتباه است.</summary>
    OutOfPlausibleRange = 2,

    /// <summary>احتمال خطای واحد ریال/تومان (مقدار دقیقاً ۱۰ برابر یا ۱/۱۰ بازه معقول).</summary>
    UnitMismatchSuspected = 3,

    /// <summary>مقدار null بود.</summary>
    NullValue = 5,

    /// <summary>مقدار عدد نبود.</summary>
    NotANumber = 6,

    /// <summary>نوع مقدار غیرمنتظره بود.</summary>
    UnexpectedType = 7,
}

/// <summary>رد شدن یک نرخ همراه دلیل.</summary>
public sealed record RateRejection(string ProviderKey, string? AssetCode, RateRejectionReason Reason, string? Detail = null);

/// <summary>
/// خروجی نرمال‌سازی: <see cref="Accepted"/> قابل انتشار/نمایش ·
/// <see cref="Flagged"/> فقط برای ثبت و بررسی (جهش مشکوک یا وضعیت نامعتبر) ·
/// <see cref="Rejected"/> با دلیل.
/// </summary>
public sealed record RateNormalizationResult(
    IReadOnlyList<NormalizedRate> Accepted,
    IReadOnlyList<NormalizedRate> Flagged,
    IReadOnlyList<RateRejection> Rejected);

/// <summary>
/// نرمال‌سازی اقلام خام: اعمال مقیاس منبع (یک بار)، تعیین واحد، بازه‌سنجی معقول،
/// ارزیابی تازگی و تشخیص جهش غیرعادی. همه قواعد اینجا و به‌صورت خالص تست می‌شوند.
/// </summary>
public sealed class RateNormalizer
{
    /// <summary>تعداد رقم اعشار مورد قبول ذخیره‌سازی (مطابق ستون decimal(18,4)).</summary>
    public const int StoredDecimalPlaces = 4;

    private readonly RateFreshnessPolicy _freshness;
    private readonly RateAnomalyDetector _anomalyDetector;
    private readonly bool _manualReviewOnAnomaly;

    /// <summary>ساخت نرمال‌ساز.</summary>
    public RateNormalizer(RateFreshnessPolicy freshness, RateAnomalyDetector anomalyDetector, RateOptions options)
    {
        ArgumentNullException.ThrowIfNull(freshness);
        ArgumentNullException.ThrowIfNull(anomalyDetector);
        ArgumentNullException.ThrowIfNull(options);

        _freshness = freshness;
        _anomalyDetector = anomalyDetector;
        _manualReviewOnAnomaly = string.Equals(
            options.AnomalyPolicy,
            RateOptions.AnomalyPolicyManualReview,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// نرمال‌سازی نتیجه تجزیه.
    /// </summary>
    /// <param name="parse">نتیجه تجزیه پاسخ منبع.</param>
    /// <param name="providerId">شناسه منبع.</param>
    /// <param name="fetchedAtUtc">زمان دریافت ما.</param>
    /// <param name="nowUtc">زمان جاری برای ارزیابی تازگی.</param>
    /// <param name="previousAmounts">آخرین مقدار ثبت‌شده هر دارایی (پایه تشخیص جهش) — می‌تواند خالی باشد.</param>
    public RateNormalizationResult Normalize(
        RateParseResult parse,
        string providerId,
        DateTimeOffset fetchedAtUtc,
        DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, decimal>? previousAmounts = null)
    {
        ArgumentNullException.ThrowIfNull(parse);

        var accepted = new List<NormalizedRate>();
        var flagged = new List<NormalizedRate>();
        var rejected = new List<RateRejection>();

        foreach (var problem in parse.Problems)
        {
            var definition = AssetCatalog.FindByProviderKey(problem.ProviderKey);

            rejected.Add(new RateRejection(
                problem.ProviderKey,
                definition?.Code,
                problem.Reason switch
                {
                    RateProblemReason.NullValue => RateRejectionReason.NullValue,
                    RateProblemReason.NotANumber => RateRejectionReason.NotANumber,
                    _ => RateRejectionReason.UnexpectedType,
                },
                problem.Detail));
        }

        if (parse.QuotedAtUtc is not { } quotedAt)
        {
            // نبود زمان ⇒ هیچ نرخی نرمال نمی‌شود (شکست بنیادی در تجزیه).
            return new RateNormalizationResult(accepted, flagged, rejected);
        }

        foreach (var parsed in parse.Rates)
        {
            var definition = AssetCatalog.FindByCode(parsed.AssetCode);

            if (definition is null)
            {
                rejected.Add(new RateRejection(
                    parsed.ProviderKey,
                    parsed.AssetCode,
                    RateRejectionReason.OutOfPlausibleRange,
                    "دارایی در فهرست پشتیبانی‌شده نیست."));
                continue;
            }

            if (parsed.RawValue <= 0m)
            {
                rejected.Add(new RateRejection(
                    parsed.ProviderKey,
                    definition.Code,
                    RateRejectionReason.NonPositive,
                    "مقدار صفر یا منفی بود."));
                continue;
            }

            // مقیاس منبع دقیقاً یک بار اعمال می‌شود (مثال: سکه ۱۴۵۰۰ ⇒ ۱۴٫۵۰۰٫۰۰۰ تومان).
            var amount = Math.Round(parsed.RawValue * definition.ProviderScale, StoredDecimalPlaces);

            var rangeProblem = CheckRange(definition, amount);

            if (rangeProblem is not null)
            {
                rejected.Add(new RateRejection(parsed.ProviderKey, definition.Code, rangeProblem.Value.Reason, rangeProblem.Value.Detail));
                continue;
            }

            var quality = _freshness.Evaluate(quotedAt, nowUtc);

            var isAnomalous = false;

            if (previousAmounts is not null &&
                previousAmounts.TryGetValue(definition.Code, out var previousAmount))
            {
                isAnomalous = _anomalyDetector.IsAnomalous(previousAmount, amount);
            }

            var rate = new NormalizedRate
            {
                AssetCode = definition.Code,
                Title = definition.Title,
                Amount = amount,
                QuoteUnit = definition.QuoteUnit,
                ProviderId = providerId,
                QuotedAtUtc = quotedAt,
                FetchedAtUtc = fetchedAtUtc,
                Quality = quality,
                ProviderRawValue = parsed.RawValue,
                ScaleApplied = definition.ProviderScale,
                IsAnomalySuspected = isAnomalous,
            };

            // سیاست انتشار:
            //   • جهش مشکوک + سیاست ManualReview ⇒ فقط ثبت (نمایش/محاسبه ممنوع)
            //   • وضعیت Invalid (زمان بسیار قدیمی/آینده) ⇒ فقط ثبت
            //   • سایر حالت‌ها ⇒ قابل انتشار؛ نرخ کهنه با برچسب صریح نمایش داده می‌شود
            if ((isAnomalous && _manualReviewOnAnomaly) || quality == RateQuality.Invalid)
            {
                flagged.Add(rate);
            }
            else
            {
                accepted.Add(rate);
            }
        }

        return new RateNormalizationResult(accepted, flagged, rejected);
    }

    private static (RateRejectionReason Reason, string? Detail)? CheckRange(AssetDefinition definition, decimal amount)
    {
        if (amount >= definition.MinPlausible && amount <= definition.MaxPlausible)
        {
            return null;
        }

        // تشخیص شایع‌ترین خطای واقعی: ارسال ریال به‌جای تومان (ضریب ۱۰) یا برعکس.
        var tenth = amount / AssetCatalog.UnitMismatchFactor;
        var tenfold = amount * AssetCatalog.UnitMismatchFactor;

        var looksLikeUnitMismatch =
            (tenth >= definition.MinPlausible && tenth <= definition.MaxPlausible) ||
            (tenfold >= definition.MinPlausible && tenfold <= definition.MaxPlausible);

        return looksLikeUnitMismatch
            ? (RateRejectionReason.UnitMismatchSuspected,
                $"مقدار {amount} با واحد انتظاری ({definition.QuoteUnit}) سازگار نیست؛ احتمال واحد ریال/تومان یا مقیاس اشتباه. مقدار منتشر نشد.")
            : (RateRejectionReason.OutOfPlausibleRange,
                $"مقدار {amount} خارج از بازه معقول [{definition.MinPlausible} .. {definition.MaxPlausible}] است. مقدار منتشر نشد.");
    }
}
