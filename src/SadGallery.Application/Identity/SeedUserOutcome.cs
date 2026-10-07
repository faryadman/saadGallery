namespace SadGallery.Application.Identity;

/// <summary>
/// نتیجهٔ مقدارگذاری یک کاربر اولیه (برای گزارش شفاف به اپراتور، بدون هیچ داده حساس).
/// </summary>
/// <param name="UserName">نام کاربری.</param>
/// <param name="Role">نقشی که تضمین شده است.</param>
/// <param name="UserCreated">آیا کاربر در این اجرا ساخته شد؟ (false = از قبل وجود داشت و رمزش تغییر نکرد)</param>
/// <param name="RoleAssigned">آیا نقش در این اجرا اضافه شد؟</param>
public sealed record SeedUserOutcome(string UserName, string Role, bool UserCreated, bool RoleAssigned);
