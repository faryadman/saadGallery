namespace SadGallery.Application.Catalog;

/// <summary>
/// تنظیمات کاتالوگ محصول (فاز ۴).
/// </summary>
public sealed class ProductOptions
{
    /// <summary>تعداد محصول‌های نمایش‌داده‌شده در «آخرین محصولات» صفحهٔ اصلی.</summary>
    public int HomeLatestCount { get; set; } = 8;

    /// <summary>تعداد محصول در هر صفحهٔ فهرست عمومی.</summary>
    public int CatalogPageSize { get; set; } = 24;

    /// <summary>
    /// پس از این مدت، قیمتِ محاسبه‌شده «کهنه» تلقی و همراه با هشدار نمایش داده می‌شود.
    /// </summary>
    /// <remarks>
    /// پیش‌فرضِ ۶۰ دقیقه: طلا در طول روز نوسان دارد و نمایش قیمتی که با نرخِ چند ساعت پیش
    /// محاسبه شده، بدون هشدار، خلاف معیار پذیرش فاز ۴ است. مقدار قابل تنظیم است چون
    /// سیاستِ هر صنف می‌تواند متفاوت باشد.
    /// </remarks>
    public int PriceStaleAfterMinutes { get; set; } = 60;

    /// <summary>حداکثر طول عنوان محصول.</summary>
    public int MaxTitleLength { get; set; } = 200;

    /// <summary>حداکثر طول خلاصه.</summary>
    public int MaxSummaryLength { get; set; } = 400;

    /// <summary>حداکثر طول توضیحات.</summary>
    public int MaxDescriptionLength { get; set; } = 8000;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (HomeLatestCount is < 1 or > 50)
        {
            errors.Add("ProductOptions:HomeLatestCount باید بین ۱ و ۵۰ باشد.");
        }

        if (CatalogPageSize is < 1 or > 100)
        {
            errors.Add("ProductOptions:CatalogPageSize باید بین ۱ و ۱۰۰ باشد.");
        }

        if (PriceStaleAfterMinutes is < 1 or > 10080)
        {
            errors.Add("ProductOptions:PriceStaleAfterMinutes باید بین ۱ دقیقه و یک هفته باشد.");
        }

        return errors;
    }
}
