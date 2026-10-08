namespace SadGallery.Domain.Market;

/// <summary>
/// مشخصات فیزیکی استاندارد مسکوکات رسمی بانک مرکزی (وزن کل و عیار).
///
/// منبع مقادیر (تأیید شده در ۲۰۲۶-۱۰-۰۸ از منابع عمومی متعدد که به استاندارد بانک مرکزی ارجاع می‌دهند):
///   • سکه تمام (امامی و بهار آزادی طرح قدیم): وزن ۸٫۱۳۳ گرم، عیار ۹۰۰ در هزار (مصطلحاً «۲۲ عیار»)، طلای خالص ≈ ۷٫۳۲ گرم.
///   • نیم سکه: ۴٫۰۶۶۵ گرم (نصف تمام)، عیار ۹۰۰.
///   • ربع سکه: ۲٫۰۳۳۲۵ گرم (نصف نیم)، عیار ۹۰۰. (برخی منابع ۲٫۰۳۲۲۵ گزارش می‌کنند؛ اختلاف ±۰٫۰۰۱ گرم.)
///   • سکه گرمی (بانک مرکزی، عرضه از ۱۳۸۹): ۱٫۰۱ گرم، عیار ۹۰۰، طلای خالص ≈ ۰٫۹۰۹ گرم.
///
/// قاعده پروژه: این اعداد «ورودی صریح» حباب‌سنج‌اند و در فرمول حدس زده نمی‌شوند.
/// تغییر هر مقدار باید همراه با نسخه فرمول (BubbleFormula.Version) مستند شود؛
/// تأیید نهایی مالک در OPEN_QUESTIONS §Q-COIN-1 درخواست شده است.
/// </summary>
public sealed record CoinStandard
{
    /// <summary>کد دارایی متناظر در AssetCatalog (مثلاً COIN_EMAMI).</summary>
    public required string AssetCode { get; init; }

    /// <summary>عنوان نمایشی.</summary>
    public required string Title { get; init; }

    /// <summary>وزن کل استاندارد (گرم) — شامل آلیاژ.</summary>
    public required decimal WeightGrams { get; init; }

    /// <summary>عیار به‌صورت نسبت خلوص (۹۰۰ در هزار = 0.900).</summary>
    public required decimal Purity { get; init; }
}

/// <summary>فهرست استاندارد مسکوکات رسمی.</summary>
public static class CoinStandards
{
    /// <summary>سکه تمام امامی (طرح جدید).</summary>
    public const string EmamiCode = "COIN_EMAMI";

    /// <summary>سکه تمام بهار آزادی (طرح قدیم).</summary>
    public const string FullOldCode = "COIN_FULL_OLD";

    /// <summary>نیم سکه.</summary>
    public const string HalfCode = "COIN_HALF";

    /// <summary>ربع سکه.</summary>
    public const string QuarterCode = "COIN_QUARTER";

    /// <summary>سکه گرمی (یک‌گرمی بانک مرکزی).</summary>
    public const string GeramiCode = "COIN_GERAMI";

    /// <summary>عیار مسکوکات رسمی (۹۰۰ در هزار).</summary>
    public const decimal OfficialPurity = 0.900m;

    private static readonly CoinStandard[] All =
    [
        new CoinStandard
        {
            AssetCode = EmamiCode,
            Title = "سکه تمام (امامی)",
            WeightGrams = 8.133m,
            Purity = OfficialPurity,
        },
        new CoinStandard
        {
            AssetCode = FullOldCode,
            Title = "سکه تمام (بهار آزادی طرح قدیم)",
            WeightGrams = 8.133m,
            Purity = OfficialPurity,
        },
        new CoinStandard
        {
            AssetCode = HalfCode,
            Title = "نیم سکه",
            WeightGrams = 4.0665m,
            Purity = OfficialPurity,
        },
        new CoinStandard
        {
            AssetCode = QuarterCode,
            Title = "ربع سکه",
            WeightGrams = 2.03325m,
            Purity = OfficialPurity,
        },
        new CoinStandard
        {
            AssetCode = GeramiCode,
            Title = "سکه گرمی",
            WeightGrams = 1.01m,
            Purity = OfficialPurity,
        },
    ];

    /// <summary>همه استانداردهای شناخته‌شده (ترتیب نمایش ثابت).</summary>
    public static IReadOnlyList<CoinStandard> Known => All;

    /// <summary>یافتن استاندارد بر اساس کد دارایی؛ ناموجود ⇒ null (بدون حدس).</summary>
    public static CoinStandard? TryGet(string assetCode) =>
        All.FirstOrDefault(coin => string.Equals(coin.AssetCode, assetCode, StringComparison.Ordinal));
}
