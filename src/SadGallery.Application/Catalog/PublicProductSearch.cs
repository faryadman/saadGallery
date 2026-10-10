namespace SadGallery.Application.Catalog;

/// <summary>معیارهای جست‌وجوی ویترینِ عمومی؛ انتشار در پرس‌وجوی پایگاه داده اعمال می‌شود.</summary>
public sealed record PublicProductSearchQuery(
    int? CategoryId,
    bool InStockOnly,
    decimal? MaximumBudgetToman,
    DateTimeOffset? FreshComputedPriceSinceUtc,
    int Skip,
    int Take);

/// <summary>صفحه‌ای از رکوردهای کاتالوگِ عمومی.</summary>
public sealed record PublicProductRecordPage(IReadOnlyList<ProductRecord> Items, int TotalCount);

/// <summary>صفحهٔ نهاییِ محصولاتِ عمومی پس از محاسبهٔ قیمت و افزودن تصویرها.</summary>
public sealed record PublicProductPage(
    IReadOnlyList<PublicProduct> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>ورودی ایجاد/ویرایش دسته‌بندی.</summary>
public sealed record ProductCategoryDraft(string Name, string? Description, int DisplayOrder);

/// <summary>نتیجهٔ ایجاد/ویرایش دسته‌بندی.</summary>
public sealed record ProductCategoryWriteOutcome(bool Succeeded, int? CategoryId, IReadOnlyList<string> Errors)
{
    public static ProductCategoryWriteOutcome Ok(int id) => new(true, id, []);

    public static ProductCategoryWriteOutcome Fail(params string[] errors) => new(false, null, errors);
}
