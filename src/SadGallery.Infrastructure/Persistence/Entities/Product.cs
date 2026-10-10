using SadGallery.Domain.Enums;

namespace SadGallery.Infrastructure.Persistence.Entities;

/// <summary>
/// کالای ویترین (فاز ۴).
/// </summary>
/// <remarks>
/// <para>
/// <b>حذف فیزیکی انجام نمی‌شود:</b> پاک‌کردنِ ردیف، سوابق فروش و فاکتورهای آینده را بی‌صاحب
/// می‌کند. به‌جای آن <see cref="IsDeleted"/> همراه با زمان و عاملِ حذف ثبت می‌شود (حذفِ نرم).
/// </para>
/// <para>
/// ستون‌های <c>Price*</c> یک «عکسِ فوریِ قیمت» هستند و عمداً تکراری (Denormalized) نگه داشته شده‌اند:
/// اگر اپراتور فردا وزن یا درصد اجرت را تغییر دهد، قیمتی که دیروز به مشتری نشان داده شده
/// نباید تغییر کند. این ویژگی برای بازتولیدِ فاکتور و ممیزی ضروری است.
/// </para>
/// </remarks>
public class Product
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>خلاصهٔ کوتاه برای کارت محصول.</summary>
    public string? Summary { get; set; }

    /// <summary>توضیح کامل (در صفحهٔ جزئیات).</summary>
    public string? Description { get; set; }

    public int? CategoryId { get; set; }

    public ProductCategory? Category { get; set; }

    /// <summary>سیاست قیمت‌گذاری.</summary>
    public PricePolicy PricePolicy { get; set; }

    /// <summary>مبلغ ثابت (تومان) — فقط برای سیاست <c>Fixed</c>.</summary>
    public decimal? FixedPriceIrt { get; set; }

    // ---- ورودی‌های محاسبه خودکار ----

    public decimal? WeightGrams { get; set; }

    public decimal? Karat { get; set; }

    public decimal MakingChargePercent { get; set; }

    public decimal ProfitPercent { get; set; }

    public decimal TaxPercent { get; set; }

    // ---- وضعیت ----

    public bool IsInStock { get; set; } = true;

    /// <summary>منتشرشده؟ فقط این کالاها در ویترین عمومی دیده می‌شوند.</summary>
    public bool IsPublished { get; set; }

    public bool IsDeleted { get; set; }

    // ---- ممیزی ----

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public DateTimeOffset? PublishedAtUtc { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public int CreatedByUserId { get; set; }

    public int? UpdatedByUserId { get; set; }

    public int? DeletedByUserId { get; set; }

    // ---- عکسِ فوریِ قیمتِ محاسبه‌شده ----

    public decimal? PriceTotalIrt { get; set; }

    public decimal? PriceGoldValueIrt { get; set; }

    public decimal? PriceMakingIrt { get; set; }

    public decimal? PriceProfitIrt { get; set; }

    public decimal? PriceTaxIrt { get; set; }

    /// <summary>نرخی که مبنای محاسبه بوده (تومان، هر گرم طلای ۱۸ عیار).</summary>
    public decimal? PriceRateAmountIrt { get; set; }

    /// <summary>زمانِ اعلامِ همان نرخ (برای تشخیص کهنگی).</summary>
    public DateTimeOffset? PriceRateQuotedAtUtc { get; set; }

    public decimal? PriceWeightGrams { get; set; }

    public decimal? PriceKarat { get; set; }

    public decimal? PriceMakingPercent { get; set; }

    public decimal? PriceProfitPercent { get; set; }

    public decimal? PriceTaxPercent { get; set; }

    public DateTimeOffset? PriceComputedAtUtc { get; set; }

    /// <summary>نسخهٔ فرمولی که با آن محاسبه انجام شده (مانند price-v1).</summary>
    public string? PriceFormulaVersion { get; set; }

    /// <summary>دلیلِ نامعتبر بودنِ محاسبه (مثلاً نرخ در دسترس نبود).</summary>
    public string? PriceReason { get; set; }

    public List<ProductImage> Images { get; set; } = [];
}
