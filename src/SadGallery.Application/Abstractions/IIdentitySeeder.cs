namespace SadGallery.Application.Abstractions;

/// <summary>
/// مقدارگذاری داده‌های پایه هویت (نقش‌ها). پیاده‌سازی در Infrastructure
/// (<c>IdentitySeeder</c>) و باید «ایدِمپوتنت» باشد: اجرای مکرر آن نباید خطا یا داده تکراری بسازد.
/// </summary>
public interface IIdentitySeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
