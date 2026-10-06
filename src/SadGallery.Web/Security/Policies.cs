namespace SadGallery.Web.Security;

/// <summary>
/// نام Policyهای کنترل دسترسی (docs/SECURITY.md §5).
/// قاعده پروژه: هر Endpoint حساس باید Policy صریح داشته باشد؛
/// «پنهان کردن دکمه در UI» کنترل امنیتی نیست.
/// </summary>
public static class Policies
{
    /// <summary>فقط نقش Admin و حساب فعال.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>نقش Operator یا Admin و حساب فعال.</summary>
    public const string OperatorArea = "OperatorArea";

    /// <summary>هر کاربر واردشده و فعال (امکانات ویژه اعضا).</summary>
    public const string MemberFeatures = "MemberFeatures";
}
