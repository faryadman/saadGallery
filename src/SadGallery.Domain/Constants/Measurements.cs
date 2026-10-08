namespace SadGallery.Domain.Constants;

/// <summary>
/// ثابت‌های اندازه‌گیری مورد استفاده در محاسبات طلا و مسکوکات.
/// هشدار: تغییر این مقادیر، محاسبات تاریخی را تغییر می‌دهد؛
/// همراه با نسخه فرمول (FormulaVersion) مستند شود. مرجع: ADR-0009.
/// </summary>
public static class Measurements
{
    /// <summary>
    /// وزن یک مثقال بر حسب گرم. مبنای رسمی در بازار ایران: ۴.۶۰۸۳ گرم.
    /// همه محاسبات داخلی روی «گرم» انجام می‌شود و مثقال فقط واحد ورودی/نمایش است.
    /// </summary>
    public const decimal MesghalInGrams = 4.6083m;

    /// <summary>
    /// یک سوت بر حسب گرم. در بازار ایران هر سوت = یک میلی‌گرم = ۰٫۰۰۱ گرم.
    /// </summary>
    public const decimal SotInGrams = 0.001m;

    /// <summary>عیار طلای خالص.</summary>
    public const decimal PureKarat = 24m;

    /// <summary>ضریب خلوص طلای ۱۸ عیار (۱۸ ÷ ۲۴).</summary>
    public const decimal Fineness18Karat = 0.750m;
}
