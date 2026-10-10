using SadGallery.Domain.Enums;

namespace SadGallery.Application.Abstractions;

/// <summary>
/// محل نگهداری فایل‌های بارگذاری‌شده.
/// </summary>
/// <remarks>
/// قاعده امنیتی پروژه: فایل‌های عمومی و خصوصی در <b>دو ریشهٔ کاملاً جدا</b> نگهداری می‌شوند و
/// تنها ریشهٔ عمومی به یک مسیر ایستا (Static) نقشه می‌شود. فایل خصوصی هرگز مسیر ایستا ندارد.
/// </remarks>
public interface IMediaStore
{
    /// <summary>
    /// ذخیره فایل با نامی که <b>سامانه</b> تولید کرده است (نام کاربر هرگز استفاده نمی‌شود).
    /// </summary>
    /// <param name="storedFileName">
    /// نامی که باید از پیش با <c>MediaFileNaming.NewStoredName</c> ساخته شده باشد.
    /// پیاده‌سازی موظف است شکلِ آن را دوباره بررسی و در صورت تردید، عملیات را رد کند
    /// (سدِ دوم در برابر عبور از مسیر — هرگز به تمایزگذاریِ تنها یک لایه اکتفا نمی‌شود).
    /// </param>
    Task SaveAsync(
        MediaKind kind,
        string storedFileName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    /// <summary>حذف فایل. نام باید از پیش با <c>MediaFileNaming.IsWellFormed</c> تأیید شده باشد.</summary>
    Task DeleteAsync(MediaKind kind, string storedFileName, CancellationToken cancellationToken);

    /// <summary>مسیر فیزیکی فایل (برای ارائه با کنترلر دارای مجوز).</summary>
    string GetPhysicalPath(MediaKind kind, string storedFileName);

    /// <summary>نشانی عمومیِ نسبیِ یک تصویر عمومی (مانند <c>/media/products/ab12….webp</c>).</summary>
    string GetPublicUrl(string storedFileName);

    bool Exists(MediaKind kind, string storedFileName);
}
