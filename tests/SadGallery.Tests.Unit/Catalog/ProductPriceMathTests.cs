using SadGallery.Domain.Catalog;
using Xunit;

namespace SadGallery.Tests.Unit.Catalog;

/// <summary>
/// تست‌های ریاضیاتِ قیمت‌گذاری (فاز ۴).
/// </summary>
/// <remarks>
/// این تست‌ها فقط فرمول را می‌سنجند؛ هیچ وابستگی به دیتابیس یا نرخِ لحظه‌ای ندارند.
/// قاعدهٔ کلیدیِ بازار که اینجا قفل می‌شود: <b>مالیات فقط به «اجرت + سود» می‌خورد،
/// نه به ارزشِ طلا</b> (اصلِ طلا از مالیاتِ بر ارزش افزوده معاف است).
/// </remarks>
public sealed class ProductPriceMathTests
{
    private static PriceInputs Inputs(
        decimal weight = 10m,
        decimal karat = 18m,
        decimal rate = 1_000_000m,
        decimal making = 10m,
        decimal profit = 7m,
        decimal tax = 10m) =>
        new(weight, karat, rate, making, profit, tax);

    [Fact]
    public void GoldValue_UsesKaratRelativeTo18KaratReference()
    {
        // ۱۰ گرمِ ۱۸ عیار با نرخ ۱٬۰۰۰٬۰۰۰ تومان برای هر گرمِ ۱۸ عیار
        Assert.Equal(10_000_000m, ProductPriceMath.GoldValueIrt(10m, 18m, 1_000_000m));

        // ۲۴ عیار گران‌تر است: ۱۰ گرم × (۲۴/۱۸) × ۱٬۰۰۰٬۰۰۰
        Assert.Equal(13_333_333m, ProductPriceMath.GoldValueIrt(10m, 24m, 1_000_000m));

        // ۹ عیار نصفِ ۱۸ عیار
        Assert.Equal(5_000_000m, ProductPriceMath.GoldValueIrt(10m, 9m, 1_000_000m));
    }

    [Fact]
    public void Compute_AppliesTaxOnlyOnMakingAndProfit_NotOnGoldValue()
    {
        // ارزش طلا = ۱۰٬۰۰۰٬۰۰۰ · اجرت ۱۰٪ = ۱٬۰۰۰٬۰۰۰
        // سود ۷٪ از (طلا + اجرت) = ۷۷۰٬۰۰۰
        // مالیات ۱۰٪ از (اجرت + سود) = ۱۷۷٬۰۰۰   ← نه از کل مبلغ
        var result = ProductPriceMath.Compute(Inputs());

        Assert.True(result.IsValid);
        var breakdown = result.Breakdown!;

        Assert.Equal(10_000_000m, breakdown.GoldValueIrt);
        Assert.Equal(1_000_000m, breakdown.MakingChargeIrt);
        Assert.Equal(770_000m, breakdown.ProfitIrt);
        Assert.Equal(177_000m, breakdown.TaxIrt);
        Assert.Equal(11_947_000m, breakdown.TotalIrt);

        // اگر (به‌اشتباه) مالیات به کل مبلغ می‌خورد، عدد کاملاً متفاوت بود:
        Assert.NotEqual(1_177_000m, breakdown.TaxIrt);
    }

    [Fact]
    public void Compute_ComponentsSumExactlyToTotal()
    {
        // چون هر جزء پیش از جمع گرد می‌شود، فاکتورِ چاپی نباید اختیارِ ریالی/تومانی نشان دهد.
        var result = ProductPriceMath.Compute(Inputs(weight: 8.133m, karat: 21.6m, rate: 1_442_800m));

        Assert.True(result.IsValid);
        var breakdown = result.Breakdown!;

        Assert.Equal(
            breakdown.GoldValueIrt + breakdown.MakingChargeIrt + breakdown.ProfitIrt + breakdown.TaxIrt,
            breakdown.TotalIrt);
    }

    [Fact]
    public void Compute_ZeroPercents_ReturnsOnlyGoldValue()
    {
        var result = ProductPriceMath.Compute(Inputs(making: 0m, profit: 0m, tax: 0m));

        Assert.True(result.IsValid);
        var breakdown = result.Breakdown!;

        Assert.Equal(10_000_000m, breakdown.GoldValueIrt);
        Assert.Equal(0m, breakdown.MakingChargeIrt);
        Assert.Equal(0m, breakdown.ProfitIrt);
        Assert.Equal(0m, breakdown.TaxIrt);
        Assert.Equal(10_000_000m, breakdown.TotalIrt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Compute_RejectsNonPositiveWeight(decimal weight)
    {
        var result = ProductPriceMath.Compute(Inputs(weight: weight));

        Assert.False(result.IsValid);
        Assert.NotNull(result.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(25)]
    public void Compute_RejectsInvalidKarat(decimal karat)
    {
        var result = ProductPriceMath.Compute(Inputs(karat: karat));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Compute_RejectsMissingOrZeroRate()
    {
        var missing = ProductPriceMath.Compute(Inputs(rate: 0m));

        Assert.False(missing.IsValid);
        Assert.Contains("نرخ", missing.Reason!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Compute_RejectsOutOfRangePercents(decimal percent)
    {
        Assert.False(ProductPriceMath.Compute(Inputs(making: percent)).IsValid);
        Assert.False(ProductPriceMath.Compute(Inputs(profit: percent)).IsValid);
        Assert.False(ProductPriceMath.Compute(Inputs(tax: percent)).IsValid);
    }

    [Fact]
    public void Compute_UsesVersionedFormula()
    {
        Assert.False(string.IsNullOrWhiteSpace(ProductPriceFormula.Version));
        Assert.Equal(7m, ProductPriceFormula.DefaultProfitPercent);
        Assert.Equal(10m, ProductPriceFormula.DefaultTaxPercent);
    }

    [Fact]
    public void Compute_DoesNotLosePrecisionOnRepeatingDivision()
    {
        // ۲۴ ÷ ۱۸ تقسیمِ تکرارشونده است؛ اگر پیش از ضرب انجام شود دقت از بین می‌رود
        // (همان باگی که در فاز ۳ پیدا و اصلاح شد).
        var result = ProductPriceMath.Compute(Inputs(weight: 3m, karat: 24m, rate: 3_000_000m));

        Assert.True(result.IsValid);
        Assert.Equal(12_000_000m, result.Breakdown!.GoldValueIrt);
    }
}
