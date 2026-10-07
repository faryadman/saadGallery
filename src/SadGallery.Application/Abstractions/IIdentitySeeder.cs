using SadGallery.Application.Identity;

namespace SadGallery.Application.Abstractions;

/// <summary>
/// مقدارگذاری داده‌های پایه هویت (نقش‌ها و کاربران اولیه). پیاده‌سازی در Infrastructure
/// (<c>IdentitySeeder</c>) و باید «ایدِمپوتنت» باشد: اجرای مکرر آن نباید خطا، داده تکراری
/// یا تغییر رمز عبور کاربران موجود را به همراه داشته باشد.
/// </summary>
public interface IIdentitySeeder
{
    /// <summary>نقش‌های پایه (<c>RoleNames.All</c>) را در صورت نبود می‌سازد.</summary>
    Task SeedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// کاربران اولیه را می‌سازد و نقش تعریف‌شده را تضمین می‌کند.
    /// پیش‌شرط: همهٔ تعریف‌ها رمز عبور داشته باشند (از Secret/ENV)؛ در غیر این صورت
    /// **پیش از هر تغییر** استثنا پرتاب می‌شود. رمز عبور لاگ نمی‌شود.
    /// </summary>
    Task<IReadOnlyList<SeedUserOutcome>> SeedUsersAsync(
        IReadOnlyList<SeedUserDefinition> users,
        CancellationToken cancellationToken = default);
}
