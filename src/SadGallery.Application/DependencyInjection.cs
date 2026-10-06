using Microsoft.Extensions.DependencyInjection;

namespace SadGallery.Application;

public static class DependencyInjection
{
    /// <summary>
    /// ثبت سرویس‌های لایه Application.
    /// در فاز ۱ سرویس تجاری‌ای اینجا ثبت نمی‌شود؛ سرویس‌های نرخ (فاز ۲)،
    /// حباب/ماشین‌حساب (فاز ۳) و تیکت (فاز ۵) به همین کلاس افزوده می‌شوند.
    /// </summary>
    public static IServiceCollection AddSadGalleryApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
