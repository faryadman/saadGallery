using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SadGallery.Infrastructure.Identity;

namespace SadGallery.Web.Security;

/// <summary>
/// سازنده Claims کاربر هنگام ورود.
/// چرا لازم است؟ Policyهای ما به Claim «فعال بودن حساب» نیاز دارند تا کاربر تعلیق‌شده
/// (IsActive=false) حتی با کوکی معتبر، به امکانات اعضا و پنل‌ها دسترسی نداشته باشد.
/// محدودیت مستند: تعلیق فوری اعمال نمی‌شود؛ تا زمان بازاعتبارسنجی کوکی
/// (SecurityStampValidationInterval، پیش‌فرض ۳۰ دقیقه) کاربر فعلی می‌تواند ادامه دهد.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<int>>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user).ConfigureAwait(false);

        identity.AddClaim(new Claim(AppClaims.IsActive, user.IsActive ? "true" : "false"));

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            identity.AddClaim(new Claim(AppClaims.DisplayName, user.DisplayName));
        }

        return identity;
    }
}
