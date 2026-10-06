using Microsoft.AspNetCore.Identity;

namespace SadGallery.Infrastructure.Identity;

/// <summary>
/// کاربر سامانه. کلید <c>int</c> انتخاب شده (Index کاراتر و ساده‌تر برای نگهداری — ADR-0004).
/// توجه: <see cref="IsActive"/> روش حذف حساب نیست؛ برای «تعلیق» استفاده می‌شود و
/// اعمال آن روی دسترسی در <c>AppUserClaimsPrincipalFactory</c> (لایه Web) انجام می‌گیرد.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    /// <summary>نام نمایشی برای پنل و پیام‌ها.</summary>
    public string? DisplayName { get; set; }

    /// <summary>زمان ثبت‌نام (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>زمان آخرین ورود موفق (UTC).</summary>
    public DateTimeOffset? LastLoginAtUtc { get; set; }

    /// <summary>تعلیق حساب بدون حذف داده.</summary>
    public bool IsActive { get; set; } = true;
}
