using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using SadGallery.Domain.Catalog;
using SadGallery.Domain.Enums;

namespace SadGallery.Application.Catalog;

/// <summary>
/// محاسبه‌گرِ قیمتِ محاسبه‌شده (Computed) بر پایهٔ نرخِ جاریِ طلای ۱۸ عیار.
/// </summary>
public interface IProductPriceCalculator
{
    /// <summary>
    /// محاسبه قیمت و ساخت عکسِ فوریِ آن (شامل مبنا، زمان و نسخه فرمول).
    /// اگر نرخ در دسترس نباشد، نتیجه نامعتبر است و <b>هیچ عددی</b> تولید نمی‌شود.
    /// </summary>
    PriceSnapshotRecord Compute(ProductRecord product, DateTimeOffset nowUtc);
}

/// <summary>
/// پیاده‌سازی محاسبه‌گر قیمت.
/// </summary>
/// <remarks>
/// قاعده: نرخ فقط اگر با واحد «تومان» (Irt) باشد استفاده می‌شود. اگر واحد متفاوت بود،
/// به‌جای تبدیلِ خودکار، محاسبه نامعتبر می‌شود — چون اشتباهِ ریال/تومان شایع‌ترین
/// خطای ده‌برابری است و تبدیلِ پنهان خطرناک‌تر از نمایش ندادن عدد است (ADR-0009).
/// </remarks>
public sealed class ProductPriceCalculator : IProductPriceCalculator
{
    /// <summary>کد داراییِ نرخ مرجع: هر گرم طلای ۱۸ عیار (همان کدِ فاز ۲/۳).</summary>
    public const string ReferenceAssetCode = "GOLD_GRAM_18";

    private readonly RateSnapshotCache _cache;

    public ProductPriceCalculator(RateSnapshotCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    public PriceSnapshotRecord Compute(ProductRecord product, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(product);

        var snapshot = _cache.Get();

        if (snapshot is null)
        {
            return Invalid("نرخ روز در دسترس نیست؛ قیمت قابل محاسبه نیست.");
        }

        var rate = snapshot.Publishable.FirstOrDefault(
            item => string.Equals(item.AssetCode, ReferenceAssetCode, StringComparison.Ordinal));

        if (rate is null)
        {
            return Invalid("نرخ هر گرم طلای ۱۸ عیار در این مجموعه نیست؛ قیمت قابل محاسبه نیست.");
        }

        if (rate.QuoteUnit != CurrencyUnit.Irt)
        {
            return Invalid(
                $"واحد نرخ مرجع ({rate.QuoteUnit}) تومان نیست؛ برای جلوگیری از اشتباهِ ریال/تومان " +
                "محاسبه انجام نشد.");
        }

        var weight = product.WeightGrams;
        var karat = product.Karat;

        if (weight is null || karat is null)
        {
            return Invalid("برای محاسبهٔ خودکار، وزن و عیار کالا باید ثبت شده باشد.");
        }

        var result = ProductPriceMath.Compute(new PriceInputs(
            WeightGrams: weight.Value,
            Karat: karat.Value,
            Rate18KaratIrt: rate.Amount,
            MakingChargePercent: product.MakingChargePercent,
            ProfitPercent: product.ProfitPercent,
            TaxPercent: product.TaxPercent));

        if (!result.IsValid || result.Breakdown is null)
        {
            return Invalid(result.Reason ?? "محاسبه قیمت امکان‌پذیر نیست.");
        }

        var breakdown = result.Breakdown;

        return new PriceSnapshotRecord(
            TotalIrt: breakdown.TotalIrt,
            GoldValueIrt: breakdown.GoldValueIrt,
            MakingIrt: breakdown.MakingChargeIrt,
            ProfitIrt: breakdown.ProfitIrt,
            TaxIrt: breakdown.TaxIrt,
            RateAmountIrt: rate.Amount,
            RateQuotedAtUtc: rate.QuotedAtUtc,
            WeightGrams: weight.Value,
            Karat: karat.Value,
            MakingPercent: product.MakingChargePercent,
            ProfitPercent: product.ProfitPercent,
            TaxPercent: product.TaxPercent,
            ComputedAtUtc: nowUtc,
            FormulaVersion: ProductPriceFormula.Version,
            Reason: null);
    }

    private static PriceSnapshotRecord Invalid(string reason) => new(
        TotalIrt: null,
        GoldValueIrt: null,
        MakingIrt: null,
        ProfitIrt: null,
        TaxIrt: null,
        RateAmountIrt: null,
        RateQuotedAtUtc: null,
        WeightGrams: null,
        Karat: null,
        MakingPercent: null,
        ProfitPercent: null,
        TaxPercent: null,
        ComputedAtUtc: null,
        FormulaVersion: ProductPriceFormula.Version,
        Reason: reason);
}
