using Microsoft.Extensions.Configuration;

namespace SadGallery.Application.Identity;

/// <summary>
/// خواندن و اعتبارسنجی فهرست کاربران اولیه از تنظیمات.
/// </summary>
/// <remarks>
/// شکل تنظیمات (آرایه):
/// <code>
/// "SeedUsers": [
///   { "UserName": "admin@example.invalid", "Email": "admin@example.invalid", "Role": "Admin", "DisplayName": "مدیر" }
/// ]
/// </code>
/// <para>
/// رمز عبور را در فایل تنظیمات نگذارید. ترتیب خواندن رمز:
/// (۱) کلید `SeedUsers:n:Password` (که می‌تواند از ENV بیاید: <c>SeedUsers__0__Password</c>)،
/// (۲) متغیر محیطی <see cref="SharedPasswordEnvironmentVariable"/> برای همه کاربرانی که رمز اختصاصی ندارند.
/// </para>
/// </remarks>
public static class SeedUsersConfiguration
{
    public const string SectionName = "SeedUsers";

    /// <summary>متغیر محیطی رمز مشترک کاربران اولیه (هرگز در مخزن قرار نمی‌گیرد).</summary>
    public const string SharedPasswordEnvironmentVariable = "SADGALLERY_SEED_PASSWORD";

    /// <summary>کلید رمز اختصاصی هر کاربر در تنظیمات.</summary>
    public const string PasswordKey = "Password";

    public static IReadOnlyList<SeedUserDefinition> Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var sharedPassword = configuration[SharedPasswordEnvironmentVariable];
        var definitions = new List<SeedUserDefinition>();

        foreach (var child in configuration.GetSection(SectionName).GetChildren())
        {
            definitions.Add(new SeedUserDefinition
            {
                UserName = child["UserName"] ?? string.Empty,
                Email = child["Email"],
                PhoneNumber = child["PhoneNumber"],
                DisplayName = child["DisplayName"],
                Role = child["Role"] ?? string.Empty,
                Password = child[PasswordKey] ?? sharedPassword,
                IsActive = !bool.TryParse(child["IsActive"], out var isActive) || isActive,
            });
        }

        return definitions;
    }

    /// <summary>
    /// اعتبارسنجی کل فهرست: خطاهای هر تعریف + نام کاربری تکراری.
    /// خروجی، متن آمادهٔ نمایش به اپراتور است و هرگز شامل رمز عبور نیست.
    /// </summary>
    public static IReadOnlyList<string> Validate(
        IReadOnlyList<SeedUserDefinition> definitions,
        IReadOnlyCollection<string> allowedRoles)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(allowedRoles);

        var errors = new List<string>();
        var seenUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];

            foreach (var error in definition.Validate(allowedRoles))
            {
                errors.Add($"{SectionName}[{index}]: {error}");
            }

            if (!string.IsNullOrWhiteSpace(definition.UserName) && !seenUserNames.Add(definition.UserName.Trim()))
            {
                errors.Add($"{SectionName}[{index}]: نام کاربری «{definition.UserName.Trim()}» تکراری است.");
            }
        }

        return errors;
    }
}
