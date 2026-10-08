using SadGallery.Domain.Constants;
using SadGallery.Domain.Enums;

namespace SadGallery.Application.Market;

/// <summary>واحدهای وزن طلا (تبدیل‌پذیر با ضرایب مستند).</summary>
public enum WeightUnit
{
    /// <summary>گرم.</summary>
    Gram = 1,

    /// <summary>مثقال (۴٫۶۰۸۳ گرم — Measurements.MesghalInGrams).</summary>
    Mesghal = 2,

    /// <summary>سوت (میلی‌گرم = ۰٫۰۰۱ گرم — Measurements.SotInGrams).</summary>
    Sot = 3,
}

/// <summary>نسخه‌بندی فرمول ماشین‌حساب طلا.</summary>
public static class GoldFormula
{
    /// <summary>نسخه فرمول تبدیل وزن/عیار و ارزش‌گذاری طلا.</summary>
    public const string Version = "gold-v1";
}

/// <summary>ورودی‌های صریح ماشین‌حساب طلا.</summary>
public sealed record GoldCalculationInputs
{
    /// <summary>وزن به واحد <see cref="Unit"/>.</summary>
    public required decimal Weight { get; init; }

    /// <summary>واحد وزن ورودی.</summary>
    public WeightUnit Unit { get; init; } = WeightUnit.Gram;

    /// <summary>عیار (۰ تا ۲۴)؛ مثال: طلای ۱۸ عیار = 18، سکه با خلوص ۹۰۰ در هزار = 21.6.</summary>
    public required decimal Karat { get; init; }

    /// <summary>نرخ گرم طلای ۱۸ عیار (تومان).</summary>
    public required decimal Gram18KRateIrt { get; init; }

    /// <summary>واحد نرخ — فقط تومان؛ ریال به‌تومان تبدیل نمی‌شود (ADR-0009).</summary>
    public CurrencyUnit RateUnit { get; init; } = CurrencyUnit.Irt;

    /// <summary>نرخ کهنه است ⇒ نتیجه قابل اعتماد نیست.</summary>
    public bool RateIsStale { get; init; }
}

/// <summary>نتیجه ماشین‌حساب طلا (همه وزن‌ها «گرم» مگر خلافش اعلام شود).</summary>
public sealed record GoldCalculationResult
{
    /// <summary>نسخه فرمول.</summary>
    public required string FormulaVersion { get; init; }

    /// <summary>ورودی‌ها معتبر بودند؟</summary>
    public required bool IsValid { get; init; }

    /// <summary>دلیل نامعتبری.</summary>
    public string? Reason { get; init; }

    /// <summary>وزن کل بر حسب گرم.</summary>
    public decimal? WeightGrams { get; init; }

    /// <summary>وزن کل بر حسب مثقال.</summary>
    public decimal? WeightMesghal { get; init; }

    /// <summary>وزن طلای خالص (گرم، معادل ۲۴ عیار).</summary>
    public decimal? PureGoldGrams { get; init; }

    /// <summary>ارزش قطعه (تومان) با نرخ گرم ۱۸ عیار.</summary>
    public decimal? ValueIrt { get; init; }

    /// <summary>نرخ تازه ⇒ true.</summary>
    public bool IsTrusted { get; init; }
}

/// <summary>قرارداد ماشین‌حساب طلا.</summary>
public interface IGoldCalculator
{
    /// <summary>محاسبه تبدیل وزن/عیار و ارزش.</summary>
    GoldCalculationResult Calculate(GoldCalculationInputs inputs);
}

/// <summary>
/// ماشین‌حساب طلا (نسخه <see cref="GoldFormula.Version"/>).
///
/// فرمول‌ها (بدون گردکردن میانی؛ گردکردن فقط در نمایش):
///   گرم از مثقال = مثقال × ۴٫۶۰۸۳ | گرم از سوت = سوت × ۰٫۰۰۱
///   طلای خالص (گرم) = وزن گرم × (عیار ÷ ۲۴)
///   ارزش (تومان) = وزن گرم × (عیار ÷ ۱۸) × نرخ گرم ۱۸ عیار
/// </summary>
public sealed class GoldCalculator : IGoldCalculator
{
    /// <inheritdoc />
    public GoldCalculationResult Calculate(GoldCalculationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.RateUnit != CurrencyUnit.Irt)
        {
            return Invalid("واحد نرخ باید تومان باشد؛ تبدیل خودکار واحدهای پول انجام نمی‌شود.");
        }

        if (inputs.Weight <= 0m)
        {
            return Invalid("وزن باید بزرگ‌تر از صفر باشد.");
        }

        if (inputs.Karat <= 0m || inputs.Karat > Measurements.PureKarat)
        {
            return Invalid("عیار باید بزرگ‌تر از صفر و حداکثر ۲۴ باشد.");
        }

        if (inputs.Gram18KRateIrt <= 0m)
        {
            return Invalid("نرخ گرم طلای ۱۸ عیار باید بزرگ‌تر از صفر باشد.");
        }

        var grams = GoldMath.ToGrams(inputs.Weight, inputs.Unit);
        var pureGold = GoldMath.PureGoldGrams(grams, inputs.Karat);
        var value = GoldMath.ValueIrt(grams, inputs.Karat, inputs.Gram18KRateIrt);

        return new GoldCalculationResult
        {
            FormulaVersion = GoldFormula.Version,
            IsValid = true,
            Reason = null,
            WeightGrams = grams,
            WeightMesghal = GoldMath.FromGrams(grams, WeightUnit.Mesghal),
            PureGoldGrams = pureGold,
            ValueIrt = value,
            IsTrusted = !inputs.RateIsStale,
        };
    }

    private static GoldCalculationResult Invalid(string reason) => new()
    {
        FormulaVersion = GoldFormula.Version,
        IsValid = false,
        Reason = reason,
        WeightGrams = null,
        WeightMesghal = null,
        PureGoldGrams = null,
        ValueIrt = null,
        IsTrusted = false,
    };
}

/// <summary>تبدیل‌های پایه وزن/عیار/ارزش — قابل استفاده مستقل (حباب‌سنج، ماشین‌حساب، تست).</summary>
public static class GoldMath
{
    /// <summary>وزن به گرم.</summary>
    public static decimal ToGrams(decimal weight, WeightUnit unit) => unit switch
    {
        WeightUnit.Gram => weight,
        WeightUnit.Mesghal => weight * Measurements.MesghalInGrams,
        WeightUnit.Sot => weight * Measurements.SotInGrams,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "واحد وزن ناشناخته"),
    };

    /// <summary>گرم به واحد دیگر.</summary>
    public static decimal FromGrams(decimal grams, WeightUnit unit) => unit switch
    {
        WeightUnit.Gram => grams,
        WeightUnit.Mesghal => grams / Measurements.MesghalInGrams,
        WeightUnit.Sot => grams / Measurements.SotInGrams,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "واحد وزن ناشناخته"),
    };

    /// <summary>وزن طلای خالص (معادل ۲۴ عیار). اول ضرب، آخر تقسیم (دقت اعشاری کامل).</summary>
    public static decimal PureGoldGrams(decimal grams, decimal karat) => grams * karat / Measurements.PureKarat;

    /// <summary>معادل وزن در عیار دیگر (تبدیل برگشت‌پذیر با تلورانس گردکردن).</summary>
    public static decimal EquivalentWeightAtKarat(decimal grams, decimal fromKarat, decimal toKarat) =>
        grams * fromKarat / toKarat;

    /// <summary>ارزش به تومان بر پایه نرخ گرم طلای ۱۸ عیار. ترتیب عمدی: ضرب‌ها قبل از تقسیم.</summary>
    public static decimal ValueIrt(decimal grams, decimal karat, decimal gram18KRateIrt) =>
        grams * karat * gram18KRateIrt / 18m;

    /// <summary>نرخ گرم معادل برای عیار دلخواه (برای نمایش «ارزش هر گرم»).</summary>
    public static decimal RateForKarat(decimal gram18KRateIrt, decimal karat) => gram18KRateIrt * karat / 18m;
}
