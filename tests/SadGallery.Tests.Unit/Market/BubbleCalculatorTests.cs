using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>
/// تست‌های مرزی حباب‌سنج (معیار پذیرش فاز ۳ ROADMAP):
/// حباب صفر، حباب منفی، نرخ صفر ⇒ نامعتبر، نرخ کهنه ⇒ IsTrusted=false، دقت اعشاری، سکه با عیار ≠ ۱۸.
/// </summary>
public sealed class BubbleCalculatorTests
{
    private readonly BubbleCalculator _calculator = new();

    private static BubbleInputs Inputs(
        decimal marketPrice,
        decimal referenceRate = 3_000_000m,
        decimal weight = 8.133m,
        decimal purity = 0.900m,
        decimal mintingCost = 0m,
        bool stale = false,
        CurrencyUnit unit = CurrencyUnit.Irt) => new()
        {
            MarketPriceIrt = marketPrice,
            WeightGrams = weight,
            Purity = purity,
            ReferenceGramRateIrt = referenceRate,
            MintingCostIrt = mintingCost,
            ReferenceIsStale = stale,
            ReferenceUnit = unit,
        };

    [Fact]
    public void ZeroBubble_WhenMarketEqualsIntrinsic_IsExactZero()
    {
        // ارزش ذاتی = ۸٫۱۳۳ × (۰٫۹ ÷ ۰٫۷۵) × ۳٬۰۰۰٬۰۰۰ = ۲۹٬۲۷۸٬۸۰۰
        var result = _calculator.Calculate(Inputs(marketPrice: 29_278_800m));

        Assert.True(result.IsValid);
        Assert.Equal(29_278_800m, result.IntrinsicValueIrt);
        Assert.Equal(0m, result.BubbleIrt);
        Assert.Equal(0m, result.BubblePercent);
        Assert.True(result.IsTrusted);
    }

    [Fact]
    public void NegativeBubble_IsReported_NotClampedToZero()
    {
        var result = _calculator.Calculate(Inputs(marketPrice: 25_000_000m));

        Assert.True(result.IsValid);
        Assert.True(result.BubbleIrt < 0m);
        Assert.True(result.BubblePercent < 0m);
        Assert.Equal(25_000_000m - result.IntrinsicValueIrt!.Value, result.BubbleIrt);
    }

    [Fact]
    public void ZeroReferenceRate_IsInvalid_WithPersianReason()
    {
        var result = _calculator.Calculate(Inputs(marketPrice: 30_000_000m, referenceRate: 0m));

        Assert.False(result.IsValid);
        Assert.Null(result.IntrinsicValueIrt);
        Assert.Null(result.BubbleIrt);
        Assert.Contains("نرخ مرجع", result.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void NonPositiveWeight_IsInvalid(int weight)
    {
        var result = _calculator.Calculate(Inputs(30_000_000m, weight: weight));

        Assert.False(result.IsValid);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void StaleReference_MakesResultUntrusted_ButStillCalculated()
    {
        var result = _calculator.Calculate(Inputs(30_000_000m, stale: true));

        Assert.True(result.IsValid);
        Assert.False(result.IsTrusted);
        Assert.NotNull(result.IntrinsicValueIrt);
    }

    [Fact]
    public void NonTomanReference_IsRejected_WithoutConversion()
    {
        var result = _calculator.Calculate(Inputs(30_000_000m, unit: CurrencyUnit.Irr));

        Assert.False(result.IsValid);
        Assert.Contains("تومان", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void DecimalPrecision_IsKept_WithoutIntermediateRounding()
    {
        // ۲٫۰۳۳۲۵ گرم ربع سکه با عیار ۰٫۹ و نرخ ۳٬۲۵۰٬۰۰۰
        var result = _calculator.Calculate(Inputs(
            marketPrice: 9_000_000m,
            referenceRate: 3_250_000m,
            weight: 2.03325m,
            purity: 0.900m));

        // ۲٫۰۳۳۲۵ × ۰٫۹ × ۳٬۲۵۰٬۰۰۰ ÷ ۰٫۷۵ = ۷٬۹۲۹٬۶۷۵ (دقیق، بدون خطای گردکردن)
        Assert.Equal(7_929_675m, result.IntrinsicValueIrt);
        Assert.Equal(1_070_325m, result.BubbleIrt);
    }

    [Fact]
    public void RepeatingDivision_DoesNotLosePrecision()
    {
        // خلوص ۰٫۸۳۳۳ ÷ ۰٫۷۵ ضریب تکرارشونده می‌دهد؛ ترتیب «ضرب قبل از تقسیم» نتیجه را دقیق نگه می‌دارد.
        var result = _calculator.Calculate(Inputs(
            marketPrice: 7_000_000m,
            referenceRate: 1_000_000m,
            weight: 6m,
            purity: 0.8333m));

        // ۶ × ۰٫۸۳۳۳ × ۱٬۰۰۰٬۰۰۰ ÷ ۰٫۷۵ = ۶٬۶۶۶٬۴۰۰ (دقیق)
        Assert.Equal(6_666_400m, result.IntrinsicValueIrt);
        Assert.Equal(333_600m, result.BubbleIrt);
    }

    [Fact]
    public void CoinWithKaratDifferentFrom18_ScalesByPurityRatio()
    {
        // سکه پارسیان: طلای ۱۸ عیار (خلوص ۰٫۷۵) ⇒ با نرخ مرجع ۱۸ عیار، ضریب ۱ می‌شود.
        var result = _calculator.Calculate(Inputs(
            marketPrice: 5_000_000m,
            referenceRate: 4_000_000m,
            weight: 1.0m,
            purity: 0.750m));

        Assert.Equal(4_000_000m, result.IntrinsicValueIrt);
        Assert.Equal(1_000_000m, result.BubbleIrt);
        Assert.Equal(25m, result.BubblePercent);
    }

    [Fact]
    public void MintingCost_IsAddedToIntrinsic()
    {
        var result = _calculator.Calculate(Inputs(marketPrice: 30_000_000m, mintingCost: 500_000m));

        Assert.Equal(29_778_800m, result.IntrinsicValueIrt);
    }

    [Fact]
    public void InvalidPurity_IsRejected()
    {
        Assert.False(_calculator.Calculate(Inputs(30_000_000m, purity: 0m)).IsValid);
        Assert.False(_calculator.Calculate(Inputs(30_000_000m, purity: 1.5m)).IsValid);
    }

    [Fact]
    public void FormulaVersion_IsReported_OnEveryResult()
    {
        Assert.Equal(BubbleFormula.Version, _calculator.Calculate(Inputs(30_000_000m)).FormulaVersion);
        Assert.Equal(BubbleFormula.Version, _calculator.Calculate(Inputs(0m)).FormulaVersion);
    }

    [Fact]
    public void NegativeMarketPriceOrCost_IsRejected()
    {
        Assert.False(_calculator.Calculate(Inputs(-1m)).IsValid);
        Assert.False(_calculator.Calculate(Inputs(30_000_000m, mintingCost: -5m)).IsValid);
    }
}
