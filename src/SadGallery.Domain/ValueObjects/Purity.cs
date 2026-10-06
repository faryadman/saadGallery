using SadGallery.Domain.Constants;

namespace SadGallery.Domain.ValueObjects;

/// <summary>
/// عیار طلا (نظام ۲۴). مثال: ۱۸ عیار = ضریب خلوص ۰.۷۵۰.
/// قاعده پروژه: طلای خالص هرگز با طلای ۱۸ عیار اشتباه گرفته نمی‌شود؛
/// هر تبدیل عیار باید از این نوع عبور کند (ADR-0009).
/// </summary>
public readonly record struct Purity
{
    public static readonly Purity Eighteen = FromKarat(18m);

    public static readonly Purity TwentyTwo = FromKarat(22m);

    public static readonly Purity TwentyFour = FromKarat(Measurements.PureKarat);

    public Purity(decimal karat)
    {
        if (karat <= 0m || karat > Measurements.PureKarat)
        {
            throw new ArgumentOutOfRangeException(
                nameof(karat), karat, $"Karat must be greater than 0 and at most {Measurements.PureKarat}.");
        }

        Karat = karat;
    }

    /// <summary>عیار (۰ تا ۲۴).</summary>
    public decimal Karat { get; }

    /// <summary>ضریب خلوص = عیار ÷ ۲۴ (مثال: ۱۸ ⇒ ۰.۷۵۰).</summary>
    public decimal Fineness => Karat / Measurements.PureKarat;

    public static Purity FromKarat(decimal karat) => new(karat);

    /// <summary>ساخت عیار از ضریب خلوص (مثال: ورودی ۰.۹۰۰ ⇒ عیار ۲۱.۶).</summary>
    public static Purity FromFineness(decimal fineness)
    {
        if (fineness <= 0m || fineness > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fineness), fineness, "Fineness must be greater than 0 and at most 1.");
        }

        return new Purity(fineness * Measurements.PureKarat);
    }

    public override string ToString() => $"{Karat}K ({Fineness:0.000})";
}
