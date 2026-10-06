namespace SadGallery.Domain.Enums;

/// <summary>
/// وضعیت تازگی/اعتبار یک نرخ.
/// قاعده پروژه: نرخ کهنه هرگز به‌عنوان نرخ لحظه‌ای نمایش داده نمی‌شود
/// و در حالت <see cref="Stale"/> یا <see cref="Invalid"/> هیچ محاسبه‌ای بدون هشدار مجاز نیست.
/// </summary>
public enum RateQuality
{
    /// <summary>دریافت موفق در بازه موردانتظار.</summary>
    Live = 1,

    /// <summary>دریافت موفق اما دیرتر از بازه موردانتظار (تأخیری).</summary>
    Delayed = 2,

    /// <summary>داده قدیمی؛ باید با برچسب «آخرین نرخ ثبت‌شده» و زمان نمایش داده شود.</summary>
    Stale = 3,

    /// <summary>داده نامعتبر یا غیرقابل اعتماد؛ نباید در محاسبات استفاده شود.</summary>
    Invalid = 4,
}
