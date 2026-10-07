using SadGallery.Application.Identity;
using Xunit;

namespace SadGallery.Tests.Unit.Identity;

/// <summary>
/// تست‌های اعتبارسنجی تعریف کاربران اولیه (ADR-0011).
/// این تست‌ها بدون دیتابیس اجرا می‌شوند؛ نقش آن‌ها تضمین «fail-closed» بودن است:
/// تعریف ناقص یا ناایمن، پیش از هر تغییر در دیتابیس باید رد شود.
/// </summary>
public sealed class SeedUserDefinitionTests
{
    private static readonly IReadOnlyCollection<string> AllowedRoles = ["Customer", "Operator", "Admin"];

    private const string StrongPassword = "SadGallery1404";

    private static SeedUserDefinition ValidDefinition() => new()
    {
        UserName = "admin@example.invalid",
        Email = "admin@example.invalid",
        Role = "Admin",
        Password = StrongPassword,
    };

    [Fact]
    public void ValidDefinition_HasNoErrors()
    {
        var errors = ValidDefinition().Validate(AllowedRoles);

        Assert.Empty(errors);
    }

    [Fact]
    public void EmptyUserName_IsRejected()
    {
        var definition = ValidDefinition() with { UserName = "   " };

        var errors = definition.Validate(AllowedRoles);

        Assert.Contains(errors, error => error.Contains("نام کاربری", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownRole_IsRejected_AndListsAllowedRoles()
    {
        var definition = ValidDefinition() with { Role = "Supervisor" };

        var errors = definition.Validate(AllowedRoles);

        var error = Assert.Single(errors, e => e.Contains("نقش", StringComparison.Ordinal));
        Assert.Contains("Customer", error, StringComparison.Ordinal);
        Assert.Contains("Admin", error, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingPassword_IsRejected_WithGuidance()
    {
        var definition = ValidDefinition() with { Password = null };

        var errors = definition.Validate(AllowedRoles);

        var error = Assert.Single(errors);
        Assert.Contains(SeedUsersConfiguration.SharedPasswordEnvironmentVariable, error, StringComparison.Ordinal);
    }

    [Fact]
    public void WeakPassword_IsRejected_WithoutEchoingThePassword()
    {
        const string weakPassword = "abcdefgh"; // بدون رقم و کوتاه‌تر نیست، اما رقم ندارد

        var definition = ValidDefinition() with { Password = weakPassword };

        var errors = definition.Validate(AllowedRoles);

        Assert.NotEmpty(errors);
        // اصل امنیتی: متن رمز هرگز در پیام خطا تکرار نمی‌شود.
        Assert.DoesNotContain(errors, error => error.Contains(weakPassword, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("admin-without-at-sign")]
    [InlineData("@no-local-part")]
    [InlineData("no-domain@")]
    public void InvalidEmail_IsRejected(string email)
    {
        var definition = ValidDefinition() with { Email = email };

        Assert.Contains(definition.Validate(AllowedRoles), error => error.Contains("ایمیل", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("0912-345-6789")]
    [InlineData("+0912345678901234567")]
    public void InvalidPhoneNumber_IsRejected(string phoneNumber)
    {
        var definition = ValidDefinition() with { PhoneNumber = phoneNumber };

        Assert.Contains(definition.Validate(AllowedRoles), error => error.Contains("موبایل", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("09123456789")]
    [InlineData("+989123456789")]
    [InlineData("0912 345 6789")]
    public void AcceptablePhoneNumbers_PassValidation(string phoneNumber)
    {
        var definition = ValidDefinition() with { PhoneNumber = phoneNumber };

        Assert.Empty(definition.Validate(AllowedRoles));
    }
}
