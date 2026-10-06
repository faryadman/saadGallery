namespace SadGallery.Infrastructure.Identity;

/// <summary>
/// نام نقش‌های پایه (ADR-0004). مقادیر این ثابت‌ها در Policyها و Seed استفاده می‌شوند.
/// افزودن نقش جدید فقط با تصمیم مستند انجام شود؛ برای مجوزهای ریزتر از Policy و Claim استفاده کنید.
/// </summary>
public static class RoleNames
{
    public const string Customer = "Customer";

    public const string Operator = "Operator";

    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Customer, Operator, Admin];
}
