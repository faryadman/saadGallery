namespace SadGallery.Application.Identity;

/// <summary>
/// تعریف یک «کاربر اولیه» برای مقدارگذاری (Seed).
/// </summary>
/// <remarks>
/// اصل امنیتی (ADR-0011): رمز عبور هرگز در مخزن، فایل تنظیمات پایه یا کد نوشته نمی‌شود.
/// مقدار آن فقط از Secret Store / متغیر محیطی (<see cref="SeedUsersConfiguration.SharedPasswordEnvironmentVariable"/>)
/// یا کلید `SeedUsers:n:Password` می‌آید. متن رمز هرگز در لاگ/خطا ظاهر نمی‌شود.
/// </remarks>
public sealed record SeedUserDefinition
{
    /// <summary>حداقل طول رمز — هم‌راستا با سیاست Identity در `Program.cs` (در صورت تغییر، هر دو به‌روز شوند).</summary>
    public const int MinimumPasswordLength = 8;

    /// <summary>حداکثر طول ایمیل/نام کاربری طبق پیش‌فرض Identity.</summary>
    public const int MaximumUserNameLength = 256;

    public required string UserName { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? DisplayName { get; init; }

    public required string Role { get; init; }

    /// <summary>رمز عبور — فقط از Secret/ENV. در هیچ حالتی چاپ یا لاگ نمی‌شود.</summary>
    public string? Password { get; init; }

    public bool IsActive { get; init; } = true;

    /// <summary>
    /// اعتبارسنجی تعریف، پیش از هر تغییر در دیتابیس (fail-closed).
    /// پیام‌ها هرگز شامل <see cref="Password"/> نیستند.
    /// </summary>
    public IReadOnlyList<string> Validate(IReadOnlyCollection<string> allowedRoles)
    {
        ArgumentNullException.ThrowIfNull(allowedRoles);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(UserName))
        {
            errors.Add("نام کاربری (UserName) خالی است.");
        }
        else if (UserName.Trim().Length > MaximumUserNameLength)
        {
            errors.Add($"نام کاربری بیش از {MaximumUserNameLength} نویسه است.");
        }

        if (string.IsNullOrWhiteSpace(Role))
        {
            errors.Add("نقش (Role) خالی است.");
        }
        else if (!allowedRoles.Contains(Role.Trim(), StringComparer.Ordinal))
        {
            errors.Add($"نقش «{Role.Trim()}» شناخته‌شده نیست؛ نقش‌های مجاز: {string.Join(", ", allowedRoles)}.");
        }

        if (!string.IsNullOrWhiteSpace(Email) && !IsPlausibleEmail(Email))
        {
            errors.Add("ایمیل معتبر نیست.");
        }

        if (!string.IsNullOrWhiteSpace(PhoneNumber) && !IsPlausiblePhoneNumber(PhoneNumber))
        {
            errors.Add("شماره موبایل باید ۱۰ تا ۱۵ رقم باشد (و در ابتدا می‌تواند «+» داشته باشد).");
        }

        if (string.IsNullOrEmpty(Password))
        {
            errors.Add(
                $"رمز عبور تعیین نشده است؛ متغیر محیطی {SeedUsersConfiguration.SharedPasswordEnvironmentVariable} " +
                "یا کلید SeedUsers:n:Password را تنظیم کنید.");
        }
        else
        {
            // بررسی‌ها هم‌راستا با سیاست Identity؛ متن رمز در پیام خطا تکرار نمی‌شود.
            if (Password.Length < MinimumPasswordLength)
            {
                errors.Add($"رمز عبور باید حداقل {MinimumPasswordLength} نویسه باشد.");
            }

            if (!Password.Any(char.IsAsciiDigit))
            {
                errors.Add("رمز عبور باید حداقل یک رقم داشته باشد (سیاست Identity).");
            }
        }

        return errors;
    }

    private static bool IsPlausibleEmail(string email)
    {
        var value = email.Trim();
        var atIndex = value.IndexOf('@', StringComparison.Ordinal);

        return atIndex > 0
               && atIndex < value.Length - 1
               && !value.Contains(' ', StringComparison.Ordinal);
    }

    private static bool IsPlausiblePhoneNumber(string phoneNumber)
    {
        var value = phoneNumber.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        var digits = value.StartsWith('+') ? value[1..] : value;

        // توجه: اینجا هیچ تبدیل عددی انجام نمی‌شود؛ شمارهٔ ۱۱ رقمی موبایل ایرانی
        // در int جا نمی‌شود و Parse/Convert آن را به‌اشتباه رد می‌کرد (کشف‌شده با تست واحد).
        return digits.Length is >= 10 and <= 15 && digits.All(char.IsAsciiDigit);
    }
}
