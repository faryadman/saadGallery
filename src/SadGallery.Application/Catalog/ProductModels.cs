using SadGallery.Domain.Enums;

namespace SadGallery.Application.Catalog;

/// <summary>
/// ورودیِ ایجاد/ویرایش محصول (تنها توسط اپراتور/ادمین).
/// </summary>
public sealed record ProductDraft(
    string Title,
    string? Summary,
    string? Description,
    int? CategoryId,
    PricePolicy PricePolicy,
    decimal? FixedPriceIrt,
    decimal? WeightGrams,
    decimal? Karat,
    decimal MakingChargePercent,
    decimal ProfitPercent,
    decimal TaxPercent,
    bool IsInStock,
    bool IsPublished);

/// <summary>
/// عکسِ فوریِ قیمتِ محاسبه‌شدهٔ یک محصول.
/// </summary>
/// <remarks>
/// این رکورد به‌عمد «کامل» نگه داشته می‌شود: اگر اپراتور فردا وزن یا درصد اجرت را عوض کند،
/// قیمتی که دیروز روی سایت نمایش داده شده نباید عوض‌شود. فاکتور و سابقه باید بازتولیدپذیر باشد.
/// </remarks>
public sealed record PriceSnapshotRecord(
    decimal? TotalIrt,
    decimal? GoldValueIrt,
    decimal? MakingIrt,
    decimal? ProfitIrt,
    decimal? TaxIrt,
    decimal? RateAmountIrt,
    DateTimeOffset? RateQuotedAtUtc,
    decimal? WeightGrams,
    decimal? Karat,
    decimal? MakingPercent,
    decimal? ProfitPercent,
    decimal? TaxPercent,
    DateTimeOffset? ComputedAtUtc,
    string? FormulaVersion,
    string? Reason);

/// <summary>رکورد کامل یک محصول (بازنماییِ ردیف دیتابیس در لایه Application).</summary>
public sealed record ProductRecord(
    int Id,
    string Title,
    string? Summary,
    string? Description,
    int? CategoryId,
    string? CategoryName,
    PricePolicy PricePolicy,
    decimal? FixedPriceIrt,
    decimal? WeightGrams,
    decimal? Karat,
    decimal MakingChargePercent,
    decimal ProfitPercent,
    decimal TaxPercent,
    bool IsInStock,
    bool IsPublished,
    bool IsDeleted,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    int CreatedByUserId,
    PriceSnapshotRecord? Price);

/// <summary>یک تصویر محصول همراه با ممیزی (چه کسی و کِی).</summary>
public sealed record ProductImageRecord(
    int Id,
    int ProductId,
    string StoredFileName,
    string ThumbnailFileName,
    int DisplayOrder,
    long SizeBytes,
    int Width,
    int Height,
    string DetectedFormat,
    string? OriginalFileName,
    string? ClaimedContentType,
    string? UploadedByName,
    DateTimeOffset UploadedAtUtc)
{
    /// <summary>حجمِ نسخهٔ ذخیره‌شده بر حسب کیلوبایت (برای نمایش در مدیریت).</summary>
    public string SizeText => (SizeBytes / 1024d).ToString("0.#");
}

/// <summary>
/// نمایش قیمت برای عموم. قاعده: اگر عدد قابل اعتماد نیست، عددی نشان داده نمی‌شود
/// (نه صفر، نه «—» بی‌توضیح).
/// </summary>
public sealed record PricePresentation(
    PricePolicy Policy,
    bool HasAmount,
    decimal? AmountIrt,
    string? AmountText,
    string? BasisText,
    string? ComputedAtText,
    string? FormulaVersion,
    bool IsStale,
    string? WarningText);

/// <summary>تصویر در نمای عمومی.</summary>
public sealed record PublicProductImage(string Url, string ThumbnailUrl, int DisplayOrder);

/// <summary>محصول در نمای عمومی (فقط محصول‌های منتشرشده).</summary>
public sealed record PublicProduct(
    int Id,
    string Title,
    string? Summary,
    string? Description,
    string? CategoryName,
    PricePresentation Price,
    bool IsInStock,
    IReadOnlyList<PublicProductImage> Images)
{
    /// <summary>نشانی بندانگشتیِ نخستین تصویر (اگر تصویری باشد).</summary>
    public string? ThumbnailUrl => Images.Count > 0 ? Images[0].ThumbnailUrl : null;
}

/// <summary>ردیف محصول در فهرست مدیریت (اپراتور/ادمین).</summary>
public sealed record ProductListRow(
    int Id,
    string Title,
    string? CategoryName,
    PricePolicy PricePolicy,
    bool IsPublished,
    bool IsInStock,
    bool IsDeleted,
    int ImageCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? PriceComputedAtUtc,
    decimal? PriceTotalIrt);

/// <summary>دسته‌بندی محصول.</summary>
public sealed record CategoryRecord(int Id, string Name, string? Description, int ProductCount, int DisplayOrder = 0);
