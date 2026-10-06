namespace SadGallery.Web.Middleware;

/// <summary>
/// سرآیندهای امنیتی پایه روی همه پاسخ‌ها.
/// نکته: CSP کامل در فاز ۸ افزوده می‌شود (به هم‌راستایی با فونت/استایل‌های self-host نیاز دارد)
/// و در چک‌لیست امنیتی فاز ۸ به‌صورت «اجرا نشده تا آن زمان» ثبت شده است.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-Frame-Options"] = "DENY";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), payment=()";

        return _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
