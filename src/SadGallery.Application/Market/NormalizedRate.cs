using SadGallery.Domain.Enums;

namespace SadGallery.Application.Market;

/// <summary>
/// نرخ نرمال‌شده و آماده ذخیره/نمایش.
/// <para>
/// **معیار پذیرش فاز ۲:** هیچ نرخی بدون <see cref="QuoteUnit"/>، <see cref="QuotedAtUtc"/>،
/// <see cref="FetchedAtUtc"/>، <see cref="ProviderId"/> و <see cref="Quality"/> منتشر نمی‌شود.
/// این ثابت‌ها با <see cref="FindInvariantViolations"/> قابل بازرسی خودکار هستند (تست واحد + تست دیتابیس).
/// </para>
/// </summary>
public sealed record NormalizedRate
{
    /// <summary>کد پایدار دارایی (مطابق <see cref="SadGallery.Domain.Market.AssetCatalog"/>).</summary>
    public required string AssetCode { get; init; }

    /// <summary>عنوان فارسی دارایی (برای نمایش/گزارش).</summary>
    public required string Title { get; init; }

    /// <summary>مقدار نهایی بر حسب واحد <see cref="QuoteUnit"/> (مقیاس منبع اعمال شده).</summary>
    public required decimal Amount { get; init; }

    /// <summary>واحد مقدار — هرگز بدون واحد ذخیره/نمایش نمی‌شود (ADR-0009).</summary>
    public required CurrencyUnit QuoteUnit { get; init; }

    /// <summary>شناسه منبع نرخ (مثلاً Tgn).</summary>
    public required string ProviderId { get; init; }

    /// <summary>زمان اعلام نرخ توسط منبع (UTC، از فیلد TimeRead با فرض وقت ایران).</summary>
    public required DateTimeOffset QuotedAtUtc { get; init; }

    /// <summary>زمان دریافت ما از منبع (UTC) — جدا از زمان اعلام نرخ.</summary>
    public required DateTimeOffset FetchedAtUtc { get; init; }

    /// <summary>وضعیت اعتبار/تازگی در زمان ارزیابی.</summary>
    public required RateQuality Quality { get; init; }

    /// <summary>مقدار خام منبع (پیش از اعمال مقیاس) — برای ردیابی و بازتولید.</summary>
    public decimal? ProviderRawValue { get; init; }

    /// <summary>مقیاس اعمال‌شده (برای ردیابی: ۱۰۰۰ سکه، ۰٫۰۰۱ انس نقره، ۱ سایر).</summary>
    public decimal ScaleApplied { get; init; } = 1m;

    /// <summary>جهش غیرعادی نسبت به مقدار قبلی مشکوک است (سیاست ManualReview ⇒ منتشر نمی‌شود).</summary>
    public bool IsAnomalySuspected { get; init; }

    /// <summary>
    /// بازرسی ثابت‌های نرخ. فهرست خالی = سالم. هر نقض ⇒ نباید منتشر/ذخیره شود.
    /// </summary>
    public static IReadOnlyList<string> FindInvariantViolations(NormalizedRate rate)
    {
        ArgumentNullException.ThrowIfNull(rate);

        var violations = new List<string>();

        if (string.IsNullOrWhiteSpace(rate.AssetCode))
        {
            violations.Add("AssetCode خالی است.");
        }

        if (!Enum.IsDefined(rate.QuoteUnit))
        {
            violations.Add("واحد پول (QuoteUnit) تعیین نشده است.");
        }

        if (!Enum.IsDefined(rate.Quality))
        {
            violations.Add("وضعیت اعتبار (Quality) تعیین نشده است.");
        }

        if (string.IsNullOrWhiteSpace(rate.ProviderId))
        {
            violations.Add("ProviderId خالی است.");
        }

        if (rate.QuotedAtUtc == default)
        {
            violations.Add("QuotedAtUtc تعیین نشده است.");
        }

        if (rate.FetchedAtUtc == default)
        {
            violations.Add("FetchedAtUtc تعیین نشده است.");
        }

        if (rate.Amount <= 0m)
        {
            violations.Add("مقدار نرخ باید مثبت باشد.");
        }

        return violations;
    }
}
