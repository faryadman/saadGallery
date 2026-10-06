using SadGallery.Domain.Constants;

namespace SadGallery.Domain.ValueObjects;

/// <summary>
/// وزن، همیشه با واحد پایه «گرم».
/// مثقال فقط واحد ورودی/نمایش است و در لبه تبدیل می‌شود تا خطا انبار نشود (ADR-0009).
/// </summary>
public readonly record struct Weight
{
    public decimal Grams { get; }

    public Weight(decimal grams)
    {
        if (grams < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(grams), grams, "Weight cannot be negative.");
        }

        Grams = grams;
    }

    public static Weight Zero => new(0m);

    public static Weight FromGrams(decimal grams) => new(grams);

    /// <summary>ساخت وزن از مثقال با ضریب رسمی ۴.۶۰۸۳ گرم.</summary>
    public static Weight FromMesghal(decimal mesghal) => new(mesghal * Measurements.MesghalInGrams);

    public decimal ToMesghal() => Grams / Measurements.MesghalInGrams;

    public override string ToString() => $"{Grams} g";
}
