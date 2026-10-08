using System.Security.Claims;
using System.Text.Encodings.Web;
using SadGallery.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SadGallery.Tests.Integration.Infrastructure;

/// <summary>
/// طرح احراز هویت فقط-تست: هدر <c>X-Test-Member: true</c> کاربر «عضو» می‌سازد.
/// چرا؟ آزمون مسیرهای Policy-دار بدون دیتابیس واقعی کاربران.
/// این طرح فقط در <see cref="MemberWebFactory"/> و صرفاً در تست‌ها ثبت می‌شود؛
/// در برنامه واقعی هیچ اثری ندارد (امنیت واقعی دست‌نخورده است).
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>نام طرح.</summary>
    public const string SchemeName = "TestMember";

    /// <summary>هدر فعال‌ساز.</summary>
    public const string HeaderName = "X-Test-Member";

    /// <summary>ساخت هندلر.</summary>
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var value) || value != "true")
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Policy «MemberFeatures» علاوه بر ورود، ادعای «حساب فعال» را الزام می‌کند (همان قاعده برنامه واقعی).
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-member-1"),
            new Claim(ClaimTypes.Name, "member@test.invalid"),
            new Claim(AppClaims.IsActive, "true"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
