using SadGallery.Application.Market;
using SadGallery.Domain.Constants;
using SadGallery.Domain.Enums;
using SadGallery.Domain.ValueObjects;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>
/// تست‌های ماشین‌حساب طلا (معیار پذیرش فاز ۳): تبدیل وزن/عیار/واحد با مقادیر شناخته‌شده
/// و برگشت‌پذیری «۱۸ عیار → خالص → ۱۸ عیار» با تلورانس مشخص.
/// </summary>
public sealed class GoldCalculatorTests
{
    private readonly GoldCalculator _calculator = new();

    private static GoldCalculationInputs Inputs(
        decimal weight,
        decimal karat = 18m,
        decimal rate = 3_000_000m,
        WeightUnit unit = WeightUnit.Gram,
        CurrencyUnit rateUnit = CurrencyUnit.Irt,
        bool stale = false) => new()
        {
            Weight = weight,
            Karat = karat,
            Gram18KRateIrt = rate,
            Unit = unit,
            RateUnit = rateUnit,
            RateIsStale = stale,
        };

    [Fact]
    public void OneMesghal18Karat_Value_Equals_GramRate_Times_MesghalInGrams()
    {
        var result = _calculator.Calculate(Inputs(1m, unit: WeightUnit.Mesghal));

        Assert.True(result.IsValid);
        Assert.Equal(Measurements.MesghalInGrams, result.WeightGrams);
        Assert.Equal(Measurements.MesghalInGrams * 3_000_000m, result.ValueIrt);
        Assert.Equal(1m, result.WeightMesghal);
    }

    [Fact]
    public void Sot_IsOneMilligram()
    {
        var result = _calculator.Calculate(Inputs(1000m, unit: WeightUnit.Sot));

        Assert.Equal(1m, result.WeightGrams);
    }

    [Fact]
    public void PureGold_Of18Karat_IsThreeQuarters()
    {
        var result = _calculator.Calculate(Inputs(100m));

        Assert.Equal(75m, result.PureGoldGrams);
    }

    [Fact]
    public void Convert18KaratToPureAndBack_IsWithinTolerance()
    {
        // ۱۰ گرم ۱۸ عیار ⇒ ۷٫۵ گرم خالص ⇒ برگشت با ضریب ۲۴/۱۸ (باید دقیقاً ۱۰ برگردد)
        var pure = GoldMath.PureGoldGrams(10m, 18m);
        var back = GoldMath.EquivalentWeightAtKarat(pure, 24m, 18m);

        Assert.Equal(7.5m, pure);
        Assert.InRange(back, 9.999m, 10.001m);
        Assert.Equal(10m, back);
    }

    [Fact]
    public void Karat21Point6_MatchesCoinPurity900()
    {
        var coin = GoldMath.PureGoldGrams(8.133m, 21.6m);

        Assert.Equal(8.133m * 0.9m, coin);
        Assert.Equal(0.9m, Purity.FromKarat(21.6m).Fineness);
    }

    [Fact]
    public void Value_UsesKaratRelativeTo18KaratReference_WithoutPrecisionLoss()
    {
        // ۱ گرم ۲۴ عیار = ۲۴/۱۸ برابر نرخ گرم ۱۸ عیار ⇒ باید دقیقاً ۴٬۰۰۰٬۰۰۰ باشد
        // (نسخه اول با تقسیم قبل از ضرب، ۳٬۹۹۹٬۹۹۹٫۹۹۹۹… می‌داد — این تست همان رگرسیون را می‌گیرد.)
        var result = _calculator.Calculate(Inputs(1m, karat: 24m, rate: 3_000_000m));

        Assert.Equal(4_000_000m, result.ValueIrt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveWeight_IsInvalid(int weight)
    {
        var result = _calculator.Calculate(Inputs(weight));

        Assert.False(result.IsValid);
        Assert.Null(result.ValueIrt);
        Assert.NotNull(result.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void KaratOutOfRange_IsInvalid(int karat)
    {
        Assert.False(_calculator.Calculate(Inputs(1m, karat: karat)).IsValid);
    }

    [Fact]
    public void RialRate_IsRejected_WithPersianReason()
    {
        var result = _calculator.Calculate(Inputs(1m, rateUnit: CurrencyUnit.Irr));

        Assert.False(result.IsValid);
        Assert.Contains("تومان", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void StaleRate_MakesResultUntrusted()
    {
        var result = _calculator.Calculate(Inputs(1m, stale: true));

        Assert.True(result.IsValid);
        Assert.False(result.IsTrusted);
    }

    [Fact]
    public void RateForKarat_ScalesLinearly()
    {
        Assert.Equal(1_500_000m, GoldMath.RateForKarat(3_000_000m, 9m));
    }

    [Fact]
    public void FormulaVersion_IsReported()
    {
        Assert.Equal(GoldFormula.Version, _calculator.Calculate(Inputs(1m)).FormulaVersion);
    }

    [Fact]
    public void WeightValueObject_AgreesWithCalculator()
    {
        // یکپارچگی با ValueObject وزن از فاز ۱ (مثقال = ۴٫۶۰۸۳ گرم)
        var weight = Weight.FromMesghal(2m);
        var result = _calculator.Calculate(Inputs(2m, unit: WeightUnit.Mesghal));

        Assert.Equal(weight.Grams, result.WeightGrams);
    }
}
