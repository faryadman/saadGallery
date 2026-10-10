using System.Security.Claims;

namespace SadGallery.Web.Security;

/// <summary>
/// خواندنِ اطلاعاتِ کاربرِ جاری از Claims.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>شناسهٔ کاربرِ واردشده؛ اگر معتبر نباشد <c>null</c>.</summary>
    public static int? CurrentUserId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var id) ? id : null;
    }

    /// <summary>نام نمایشی برای ثبت در گزارش‌ها (ممیزی).</summary>
    public static string CurrentDisplayName(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.FindFirstValue(AppClaims.DisplayName)
               ?? user.FindFirstValue(ClaimTypes.Name)
               ?? "نامشخص";
    }
}
