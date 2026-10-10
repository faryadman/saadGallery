using SadGallery.Application.Catalog;

namespace SadGallery.Application.Abstractions;

/// <summary>
/// دسترسیِ دادهٔ کاتالوگ. پیاده‌سازی در لایه Infrastructure با EF Core انجام می‌شود.
/// </summary>
/// <remarks>
/// این یک «مخزن عمومی» (Generic Repository) نیست؛ قراردادی محدود به نیازهای همین ماژول است
/// (مطابق ADR-0003 که ساخت لایه Repository عمومی را رد کرده، و هم‌راستا با <c>IRateStore</c>).
/// </remarks>
public interface IProductStore
{
    // ---- نمای عمومی ----

    /// <summary>آخرین محصول‌های منتشرشده (برای صفحهٔ اصلی و ویترین).</summary>
    Task<IReadOnlyList<ProductRecord>> GetPublishedAsync(int take, CancellationToken cancellationToken);

    /// <summary>
    /// یک محصول برای نمایش عمومی. تنها در صورتی ردیف برمی‌گردد که منتشرشده و حذف‌نشده باشد؛
    /// در غیر این صورت <c>null</c> (یعنی ۴۰۴ — معیار پذیرش فاز ۴).
    /// </summary>
    Task<ProductRecord?> GetPublishedByIdAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductImageRecord>> GetImagesAsync(int productId, CancellationToken cancellationToken);

    /// <summary>
    /// تصاویرِ چند محصول در یک کوئری (جلوگیری از الگوی N+1 در فهرست‌ها و صفحهٔ اصلی).
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<ProductImageRecord>>> GetImagesForProductsAsync(
        IReadOnlyList<int> productIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryRecord>> GetCategoriesAsync(CancellationToken cancellationToken);

    // ---- مدیریت ----

    /// <summary>فهرست مدیریت (شامل منتشرنشده‌ها).</summary>
    Task<IReadOnlyList<ProductListRow>> GetListAsync(bool includeDeleted, CancellationToken cancellationToken);

    /// <summary>دریافت برای ویرایش — بدون فیلترِ انتشار (اپراتور باید بتواند پیش‌نویس را ببیند).</summary>
    Task<ProductRecord?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<int> CreateAsync(ProductDraft draft, int actorUserId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task UpdateAsync(int id, ProductDraft draft, int actorUserId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>ذخیره عکسِ فوریِ قیمتِ محاسبه‌شده (همراه مبنا و زمان).</summary>
    Task SavePriceSnapshotAsync(int id, PriceSnapshotRecord price, CancellationToken cancellationToken);

    Task SetPublishedAsync(int id, bool published, int actorUserId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>
    /// حذفِ نرم: ردیف از دیتابیس پاک نمی‌شود (سوابق فروش و فاکتورها نباید بی‌صاحب شوند).
    /// </summary>
    Task SoftDeleteAsync(int id, int actorUserId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    // ---- تصاویر ----

    Task<int> CountImagesAsync(int productId, CancellationToken cancellationToken);

    Task AddImageAsync(ProductImageInput image, CancellationToken cancellationToken);

    Task<ProductImageRecord?> GetImageAsync(int imageId, CancellationToken cancellationToken);

    Task RemoveImageAsync(int imageId, CancellationToken cancellationToken);
}

/// <summary>ورودی ثبت یک تصویر محصول (پس از اعتبارسنجی و پردازش).</summary>
public sealed record ProductImageInput(
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
    int UploadedByUserId,
    DateTimeOffset UploadedAtUtc);
