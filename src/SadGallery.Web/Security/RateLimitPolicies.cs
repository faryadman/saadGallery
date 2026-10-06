namespace SadGallery.Web.Security;

/// <summary>
/// نام سیاست‌های محدودیت نرخ درخواست.
/// محدودیت‌ها بر اساس IP و مسیر اعمال می‌شوند: ورود/ثبت‌نام، OTP (فاز ۶)، تیکت (فاز ۵).
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>ورود، ثبت‌نام و بازیابی رمز.</summary>
    public const string Authentication = "sg-auth";
}
