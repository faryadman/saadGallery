namespace SadGallery.Web.Security;

/// <summary>
/// نام Claimهای اختصاصی سامانه. مقادیر در <c>AppUserClaimsPrincipalFactory</c> پر می‌شوند
/// و Policyها بر اساس آن‌ها تصمیم می‌گیرند.
/// </summary>
public static class AppClaims
{
    /// <summary>وضعیت فعال بودن حساب؛ مقدار "true"/"false". کاربر تعلیق‌شده به امکانات اعضا دسترسی ندارد.</summary>
    public const string IsActive = "sg:is_active";

    /// <summary>نام نمایشی، برای نمایش در UI بدون کوئری اضافه.</summary>
    public const string DisplayName = "sg:display_name";
}
