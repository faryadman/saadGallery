using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Identity;

namespace SadGallery.Infrastructure.Identity;

/// <summary>
/// مقدارگذاری ایدِمپوتنت نقش‌های پایه و کاربران اولیه.
/// </summary>
/// <remarks>
/// <para>
/// تصمیم امنیتی (ADR-0004 اصلاح‌شده با ADR-0011): این Seeder **هیچ رمز عبور پیش‌فرضی در خود ندارد**
/// و هیچ کاربری را بدون تعریف صریح + رمز عبور از Secret/ENV نمی‌سازد
/// (ساخت حساب مدیر با رمز پیش‌فرض، یک آسیب‌پذیری کلاسیک است).
/// </para>
/// <para>
/// رفتار در اجرای مکرر: کاربر موجود **بازنویسی نمی‌شود** و رمزش تغییر نمی‌کند؛ فقط اگر نقش
/// تعریف‌شده را نداشته باشد، نقش اضافه می‌شود.
/// </para>
/// </remarks>
public sealed class IdentitySeeder : IIdentitySeeder
{
    private readonly RoleManager<IdentityRole<int>> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClock _clock;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        RoleManager<IdentityRole<int>> roleManager,
        UserManager<ApplicationUser> userManager,
        IClock clock,
        ILogger<IdentitySeeder> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _clock = clock;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in RoleNames.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await _roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
            {
                _logger.LogDebug("Role {RoleName} already exists; skipping.", roleName);
                continue;
            }

            var result = await _roleManager.CreateAsync(new IdentityRole<int>(roleName)).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}'. Errors: {Describe(result)}");
            }

            _logger.LogInformation("Role {RoleName} created.", roleName);
        }
    }

    public async Task<IReadOnlyList<SeedUserOutcome>> SeedUsersAsync(
        IReadOnlyList<SeedUserDefinition> users,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(users);

        if (users.Count == 0)
        {
            return [];
        }

        // پیش‌شرط fail-closed: پیش از هر تغییری بررسی می‌شود تا «نیمه‌ساخته» نشویم.
        // پیام خطا فقط نام کاربری را می‌گوید، نه رمز عبور.
        var withoutPassword = users
            .Where(user => string.IsNullOrEmpty(user.Password))
            .Select(user => user.UserName?.Trim() ?? "(بدون نام کاربری)")
            .ToList();

        if (withoutPassword.Count > 0)
        {
            throw new InvalidOperationException(
                "Seed کاربران متوقف شد: این تعریف‌ها رمز عبور ندارند ⇒ " +
                string.Join("، ", withoutPassword) +
                $". مقدار متغیر محیطی {SeedUsersConfiguration.SharedPasswordEnvironmentVariable} را تنظیم کنید.");
        }

        var outcomes = new List<SeedUserOutcome>(users.Count);

        foreach (var definition in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userName = definition.UserName.Trim();
            var roleName = definition.Role.Trim();

            var user = await _userManager.FindByNameAsync(userName).ConfigureAwait(false);
            var userCreated = false;

            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = userName,
                    Email = Normalize(definition.Email),
                    PhoneNumber = Normalize(definition.PhoneNumber),
                    DisplayName = Normalize(definition.DisplayName),
                    CreatedAtUtc = _clock.UtcNow,
                    IsActive = definition.IsActive,
                };

                // رمز عبور فقط در حافظه است: نه لاگ می‌شود، نه در پیام خطا تکرار می‌شود.
                var createResult = await _userManager.CreateAsync(user, definition.Password!).ConfigureAwait(false);

                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"ایجاد کاربر اولیه «{userName}» ناموفق بود: {Describe(createResult)}");
                }

                userCreated = true;
                _logger.LogInformation(
                    "Seed user {UserName} created with role {RoleName}.",
                    userName,
                    roleName);
            }
            else
            {
                _logger.LogInformation(
                    "Seed user {UserName} already exists; password and profile left unchanged.",
                    userName);
            }

            var roleAssigned = false;

            if (!await _userManager.IsInRoleAsync(user, roleName).ConfigureAwait(false))
            {
                var roleResult = await _userManager.AddToRoleAsync(user, roleName).ConfigureAwait(false);

                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"افزودن نقش «{roleName}» به کاربر «{userName}» ناموفق بود: {Describe(roleResult)}");
                }

                roleAssigned = true;
                _logger.LogInformation("Role {RoleName} assigned to seed user {UserName}.", roleName, userName);
            }

            outcomes.Add(new SeedUserOutcome(userName, roleName, userCreated, roleAssigned));
        }

        return outcomes;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>خلاصهٔ خطاهای Identity بدون هیچ داده حساس (Identity رمز را بازنمی‌گرداند).</summary>
    private static string Describe(IdentityResult result)
        => string.Join("؛ ", result.Errors.Select(error => $"{error.Code}:{error.Description}"));
}
