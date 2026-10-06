using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SadGallery.Application.Abstractions;

namespace SadGallery.Infrastructure.Identity;

/// <summary>
/// مقدارگذاری ایدِمپوتنت نقش‌های پایه.
/// تصمیم امنیتی (ADR-0004): این Seeder **هیچ کاربری نمی‌سازد**؛
/// ساخت حساب مدیر با رمز پیش‌فرض، یک آسیب‌پذیری کلاسیک است.
/// حساب مدیر در فاز ۶ از طریق یک مسیر مستند و با ورود ایمن ساخته می‌شود.
/// </summary>
public sealed class IdentitySeeder : IIdentitySeeder
{
    private readonly RoleManager<IdentityRole<int>> _roleManager;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(RoleManager<IdentityRole<int>> roleManager, ILogger<IdentitySeeder> logger)
    {
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in RoleNames.All)
        {
            if (await _roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
            {
                _logger.LogDebug("Role {RoleName} already exists; skipping.", roleName);
                continue;
            }

            var result = await _roleManager.CreateAsync(new IdentityRole<int>(roleName)).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}"));
                throw new InvalidOperationException($"Failed to create role '{roleName}'. Errors: {errors}");
            }

            _logger.LogInformation("Role {RoleName} created.", roleName);
        }
    }
}
