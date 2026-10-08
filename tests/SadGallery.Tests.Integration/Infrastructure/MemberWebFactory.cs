using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace SadGallery.Tests.Integration.Infrastructure;

/// <summary>
/// کارخانه وب تست با طرح احراز هویت «عضو آزمایشی» (فقط برای آزمون Policy سرور).
/// کاربر واقعی Identity در تست ساخته نمی‌شود (نیازمند دیتابیس)؛
/// اما کنترل دسترسی در همان Middleware واقعی ASP.NET Core اجرا می‌شود.
/// </summary>
public sealed class MemberWebFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        WebFactorySettings.Apply(builder);

        builder.ConfigureTestServices(services =>
        {
            // طرح تست به‌عنوان پیش‌فرض احراز هویت جایگزین کوکی Identity می‌شود (فقط در این کارخانه).
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
