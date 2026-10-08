using SadGallery.Domain.Enums;

namespace SadGallery.Application.Market;

/// <summary>نسخه‌بندی فرمول‌ها — تغییر هر فرمول باید نسخه را افزایش دهد (قاعده پروژه: محاسبات قابل ردیابی).</summary>
public static class BubbleFormula
{
    /// <summary>نسخه فرمول حباب (فرمول ارزش ذاتی + حباب ریالی/درصدی).</summary>
    public const string Version = "bubble-v1";

    /// <summary>خلوص مرجع (عیار ۱۸ = ۰٫۷۵۰) — ارجاع پیش‌فرض نرخ گرم.</summary>
    public const decimal ReferencePurity18K = 0.750m;
}

/// <summary>
/// ورودی‌های صریح حباب‌سنج. هیچ مقداری «پیش‌فرض پنهان» ندارد؛
/// سرویس وب مقادیر را از استاندارد مسکوکات (CoinStandards) و آخرین نرخ‌ها پر می‌کند و کاربر می‌تواند تغییر دهد.
/// </summary>
public sealed record BubbleInputs
{
    /// <summary>قیمت بازار قطعه (تومان).</summary>
    public required decimal MarketPriceIrt { get; init; }

    /// <summary>وزن کل استاندارد قطعه (گرم).</summary>
    public required decimal WeightGrams { get; init; }

    /// <summary>خلوص قطعه به‌صورت نسبت (عیار ۹۰۰ ⇒ 0.900).</summary>
    public required decimal Purity { get; init; }

    /// <summary>نرخ مرجع: قیمت هر گرم طلا با خلوص مرجع (تومان).</summary>
    public required decimal ReferenceGramRateIrt { get; init; }

    /// <summary>خلوص نرخ مرجع (پیش‌فرض ۰٫۷۵۰ = عیار ۱۸).</summary>
    public decimal ReferencePurity { get; init; } = BubbleFormula.ReferencePurity18K;

    /// <summary>هزینه ضرب/ساخت هر قطعه (تومان، پیش‌فرض صفر).</summary>
    public decimal MintingCostIrt { get; init; }

    /// <summary>واحد نرخ مرجع — فقط تومان پذیرفته می‌شود؛ ریال به‌تومان تبدیل نمی‌شود (ADR-0009).</summary>
    public CurrencyUnit ReferenceUnit { get; init; } = CurrencyUnit.Irt;

    /// <summary>نرخ مرجع کهنه است (پیرتر از آستانه تازگی) ⇒ نتیجه قابل اعتماد نیست.</summary>
    public bool ReferenceIsStale { get; init; }
}

/// <summary>نتیجه حباب‌سنج. مقادیر پولی «تومان» و بدون تبدیل واحد هستند.</summary>
public sealed record BubbleResult
{
    /// <summary>نسخه فرمول به‌کاررفته (در نمایش و لاگ ثبت می‌شود).</summary>
    public required string FormulaVersion { get; init; }

    /// <summary>نتیجه معتبر است؟ (ورودی نامعتبر ⇒ همه مقادیر null).</summary>
    public required bool IsValid { get; init; }

    /// <summary>دلیل نامعتبری (فارسی، برای کاربر).</summary>
    public string? Reason { get; init; }

    /// <summary>ارزش ذاتی قطعه (تومان).</summary>
    public decimal? IntrinsicValueIrt { get; init; }

    /// <summary>حباب ریالی = قیمت بازار − ارزش ذاتی (می‌تواند منفی باشد).</summary>
    public decimal? BubbleIrt { get; init; }

    /// <summary>حباب درصدی نسبت به ارزش ذاتی.</summary>
    public decimal? BubblePercent { get; init; }

    /// <summary>معتبر و مبتنی بر نرخ تازه ⇒ true؛ نرخ کهنه ⇒ false (برچسب هشدار در UI).</summary>
    public bool IsTrusted { get; init; }
}

/// <summary>قرارداد حباب‌سنج (مستقل از منبع نرخ؛ فقط ورودی صریح ⇒ خروجی محاسبه‌شده).</summary>
public interface IBubbleCalculator
{
    /// <summary>محاسبه حباب بر پایه ورودی‌های صریح.</summary>
    BubbleResult Calculate(BubbleInputs inputs);
}

/// <summary>
/// حباب‌سنج (نسخه <see cref="BubbleFormula.Version"/>).
///
/// فرمول (عیناً در UI و مستندات نمایش داده می‌شود):
///   ارزش ذاتی = وزن × (خلوص قطعه ÷ خلوص مرجع) × نرخ گرم مرجع + هزینه ضرب
///   حباب ریالی = قیمت بازار − ارزش ذاتی
///   حباب درصدی = (حباب ریالی ÷ ارزش ذاتی) × ۱۰۰
///
/// قاعده واحد: اگر واحد نرخ مرجع تومان نباشد، نتیجه «نامعتبر» می‌شود؛
/// تبدیل خودکار ریال↔تومان ممنوع است (ADR-0009).
/// </summary>
public sealed class BubbleCalculator : IBubbleCalculator
{
    /// <inheritdoc />
    public BubbleResult Calculate(BubbleInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.ReferenceUnit != CurrencyUnit.Irt)
        {
            return Invalid("واحد نرخ مرجع باید تومان باشد؛ تبدیل خودکار واحدها انجام نمی‌شود.");
        }

        if (inputs.ReferenceGramRateIrt <= 0m)
        {
            return Invalid("نرخ مرجع گرم طلا باید بزرگ‌تر از صفر باشد.");
        }

        if (inputs.WeightGrams <= 0m)
        {
            return Invalid("وزن قطعه باید بزرگ‌تر از صفر باشد.");
        }

        if (inputs.Purity <= 0m || inputs.Purity > 1m)
        {
            return Invalid("خلوص قطعه باید بین صفر و یک باشد (مثال: عیار ۹۰۰ ⇒ ۰٫۹).");
        }

        if (inputs.ReferencePurity <= 0m || inputs.ReferencePurity > 1m)
        {
            return Invalid("خلوص نرخ مرجع باید بین صفر و یک باشد.");
        }

        if (inputs.MarketPriceIrt < 0m || inputs.MintingCostIrt < 0m)
        {
            return Invalid("قیمت بازار و هزینه ضرب نمی‌توانند منفی باشند.");
        }

        // ارزش ذاتی با دقت اعشاری کامل محاسبه می‌شود؛ گردکردن فقط در نمایش انجام می‌گیرد.
        // ترتیب عملیات عمدی است: «اول ضرب، آخر تقسیم» تا تقسیم‌های تکرارشونده (مثل ۲۴÷۱۸)
        // دقت را از بین نبرند — تست GoldCalculatorTests.Value_UsesKaratRelativeTo18KaratReference
        // همین اشتباه را در نسخه اول گرفت.
        var intrinsic = (inputs.WeightGrams * inputs.Purity * inputs.ReferenceGramRateIrt / inputs.ReferencePurity)
            + inputs.MintingCostIrt;
        var bubble = inputs.MarketPriceIrt - intrinsic;
        var bubblePercent = bubble / intrinsic * 100m;

        return new BubbleResult
        {
            FormulaVersion = BubbleFormula.Version,
            IsValid = true,
            Reason = null,
            IntrinsicValueIrt = intrinsic,
            BubbleIrt = bubble,
            BubblePercent = bubblePercent,
            IsTrusted = !inputs.ReferenceIsStale,
        };
    }

    private static BubbleResult Invalid(string reason) => new()
    {
        FormulaVersion = BubbleFormula.Version,
        IsValid = false,
        Reason = reason,
        IntrinsicValueIrt = null,
        BubbleIrt = null,
        BubblePercent = null,
        IsTrusted = false,
    };
}
