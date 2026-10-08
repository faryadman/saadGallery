using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Market;

namespace SadGallery.Web.Models.Market;

/// <summary>گزینه سکه استاندارد در فرم حباب‌سنج (وزن/عیار از CoinStandards — ورودی صریح).</summary>
public sealed record BubbleCoinOption(
    string AssetCode,
    string Title,
    decimal WeightGrams,
    decimal Purity,
    decimal? MarketPriceIrt);

/// <summary>ورودی فرم حباب‌سنج (رشته‌ها با پارسر فارسی خوانده می‌شوند).</summary>
public sealed class BubbleForm
{
    /// <summary>کد سکه استاندارد انتخاب‌شده (اختیاری).</summary>
    public string? Coin { get; set; }

    /// <summary>قیمت بازار قطعه (تومان).</summary>
    public string? MarketPrice { get; set; }

    /// <summary>وزن (گرم).</summary>
    public string? Weight { get; set; }

    /// <summary>خلوص (مثال ۰٫۹ برای عیار ۹۰۰).</summary>
    public string? Purity { get; set; }

    /// <summary>نرخ مرجع: گرم طلای ۱۸ عیار (تومان).</summary>
    public string? ReferenceRate { get; set; }

    /// <summary>هزینه ضرب (تومان، اختیاری).</summary>
    public string? MintingCost { get; set; }
}

/// <summary>مدل صفحه حباب‌سنج.</summary>
public sealed class BubblePageModel
{
    /// <summary>سکه‌های استاندارد قابل انتخاب.</summary>
    public required IReadOnlyList<BubbleCoinOption> Coins { get; init; }

    /// <summary>ورودی‌های فعلی فرم (برای بازنمایش).</summary>
    public required BubbleForm Form { get; init; }

    /// <summary>نتیجه محاسبه (اگر فرم ارسال شده و ورودی‌ها معتبر بوده).</summary>
    public BubbleResult? Result { get; init; }

    /// <summary>پیام وضعیت منبع نرخ (نمونه/قطع/در دسترس نبودن).</summary>
    public string? RateNotice { get; init; }

    /// <summary>نرخ مرجع کهنه است ⇒ هشدار اعتماد.</summary>
    public bool ReferenceIsStale { get; init; }

    /// <summary>نمونهٔ آزمایشی است.</summary>
    public bool IsSampleData { get; init; }
}

/// <summary>ورودی فرم ماشین‌حساب طلا.</summary>
public sealed class GoldForm
{
    /// <summary>وزن.</summary>
    public string? Amount { get; set; }

    /// <summary>واحد وزن: Gram | Mesghal | Sot.</summary>
    public string? Unit { get; set; }

    /// <summary>عیار (۰ تا ۲۴).</summary>
    public string? Karat { get; set; }

    /// <summary>نرخ گرم ۱۸ عیار (تومان).</summary>
    public string? ReferenceRate { get; set; }
}

/// <summary>مدل صفحه ماشین‌حساب طلا.</summary>
public sealed class GoldPageModel
{
    /// <summary>ورودی‌ها.</summary>
    public required GoldForm Form { get; init; }

    /// <summary>نتیجه.</summary>
    public GoldCalculationResult? Result { get; init; }

    /// <summary>واحد وزن انتخابی (برای بازنمایش).</summary>
    public WeightUnit Unit { get; init; } = WeightUnit.Gram;

    /// <summary>نرخ مرجع پیشنهادی از آخرین نرخ‌ها.</summary>
    public decimal? SuggestedRate { get; init; }

    /// <summary>پیام وضعیت منبع.</summary>
    public string? RateNotice { get; init; }

    /// <summary>نمونهٔ آزمایشی.</summary>
    public bool IsSampleData { get; init; }
}

/// <summary>مدل صفحه جزئیات یک نرخ.</summary>
public sealed record RateDetailsModel(
    RateDisplayItem Item,
    bool IsSampleData,
    DateTimeOffset? LastFetchedUtc);

/// <summary>مدل سرصفحه صفحه تاریخچه اعضا.</summary>
public sealed class HistoryPageModel
{
    /// <summary>تاریخچه دارایی انتخاب‌شده.</summary>
    public required RateHistoryModel History { get; init; }

    /// <summary>فهرست دارایی‌های قابل انتخاب.</summary>
    public required IReadOnlyList<AssetDefinition> Assets { get; init; }

    /// <summary>نمودار SVG (خالی اگر نقطه‌ای نباشد).</summary>
    public required string Chart { get; init; }

    /// <summary>واحد دارایی (برای پانوشت نمودار).</summary>
    public CurrencyUnit Unit { get; init; } = CurrencyUnit.Irt;
}

/// <summary>یک ردیف مقایسه حباب در صفحه پیشرفته (ویژه اعضا).</summary>
public sealed record BubbleComparisonRow(
    string Title,
    decimal WeightGrams,
    decimal Purity,
    decimal? MarketPriceIrt,
    BubbleResult? Result);

/// <summary>مدل صفحه حباب‌سنج پیشرفته (ویژه اعضا).</summary>
public sealed class AdvancedBubblePageModel
{
    /// <summary>ردیف‌های مقایسه.</summary>
    public required IReadOnlyList<BubbleComparisonRow> Rows { get; init; }

    /// <summary>نرخ مرجع به‌کاررفته.</summary>
    public decimal? ReferenceRateIrt { get; init; }

    /// <summary>وضعیت منبع.</summary>
    public string? RateNotice { get; init; }

    /// <summary>نرخ مرجع کهنه.</summary>
    public bool ReferenceIsStale { get; init; }

    /// <summary>نمونهٔ آزمایشی.</summary>
    public bool IsSampleData { get; init; }
}
