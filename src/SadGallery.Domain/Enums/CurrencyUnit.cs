namespace SadGallery.Domain.Enums;

/// <summary>
/// واحد پول. قاعده پروژه (ADR-0009): ریال و تومان هرگز بدون تبدیل صریح یکی گرفته نمی‌شوند.
/// </summary>
public enum CurrencyUnit
{
    /// <summary>ریال ایران</summary>
    Irr = 1,

    /// <summary>تومان (۱ تومان = ۱۰ ریال)</summary>
    Irt = 2,

    /// <summary>دلار آمریکا</summary>
    Usd = 3,

    /// <summary>تتر (USDT)</summary>
    Usdt = 4,
}
