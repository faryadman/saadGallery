using Microsoft.Extensions.Configuration;
using SadGallery.Application.Identity;
using Xunit;

namespace SadGallery.Tests.Unit.Identity;

/// <summary>
/// تست‌های خواندن تنظیمات کاربران اولیه: شکل آرایه، رمز مشترک از ENV، و اعتبارسنجی فهرست.
/// </summary>
public sealed class SeedUsersConfigurationTests
{
    private static readonly IReadOnlyCollection<string> AllowedRoles = ["Customer", "Operator", "Admin"];

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void EmptySection_ReturnsNoUsers()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        Assert.Empty(SeedUsersConfiguration.Read(configuration));
    }

    [Fact]
    public void ReadsArrayEntries_WithDefaults()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["SeedUsers:0:UserName"] = "admin@example.invalid",
            ["SeedUsers:0:Email"] = "admin@example.invalid",
            ["SeedUsers:0:Role"] = "Admin",
            ["SeedUsers:0:DisplayName"] = "مدیر",
            ["SeedUsers:1:UserName"] = "customer@example.invalid",
            ["SeedUsers:1:Role"] = "Customer",
            ["SeedUsers:1:IsActive"] = "false",
        });

        var users = SeedUsersConfiguration.Read(configuration);

        Assert.Equal(2, users.Count);

        Assert.Equal("admin@example.invalid", users[0].UserName);
        Assert.Equal("Admin", users[0].Role);
        Assert.Equal("مدیر", users[0].DisplayName);
        Assert.True(users[0].IsActive); // پیش‌فرض
        Assert.Null(users[0].Password); // بدون رمز ⇒ بعداً fail-closed رد می‌شود

        Assert.False(users[1].IsActive);
    }

    [Fact]
    public void SharedPasswordFromEnvironment_IsAppliedToUsersWithoutOwnPassword()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [SeedUsersConfiguration.SharedPasswordEnvironmentVariable] = "SharedEnvPass1",
            ["SeedUsers:0:UserName"] = "admin@example.invalid",
            ["SeedUsers:0:Role"] = "Admin",
            ["SeedUsers:1:UserName"] = "operator@example.invalid",
            ["SeedUsers:1:Role"] = "Operator",
            ["SeedUsers:1:Password"] = "OwnSecretPass1",
        });

        var users = SeedUsersConfiguration.Read(configuration);

        Assert.Equal("SharedEnvPass1", users[0].Password); // از ENV
        Assert.Equal("OwnSecretPass1", users[1].Password); // رمز اختصاصی مقدم است
    }

    [Fact]
    public void Validate_ReportsMissingPassword_PerEntry()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["SeedUsers:0:UserName"] = "admin@example.invalid",
            ["SeedUsers:0:Role"] = "Admin",
        });

        var errors = SeedUsersConfiguration.Validate(SeedUsersConfiguration.Read(configuration), AllowedRoles);

        Assert.Contains(errors, error => error.Contains("SeedUsers[0]", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsDuplicateUserNames_CaseInsensitively()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [SeedUsersConfiguration.SharedPasswordEnvironmentVariable] = "SharedEnvPass1",
            ["SeedUsers:0:UserName"] = "admin@example.invalid",
            ["SeedUsers:0:Role"] = "Admin",
            ["SeedUsers:1:UserName"] = "ADMIN@example.invalid",
            ["SeedUsers:1:Role"] = "Customer",
        });

        var errors = SeedUsersConfiguration.Validate(SeedUsersConfiguration.Read(configuration), AllowedRoles);

        Assert.Contains(errors, error => error.Contains("تکراری", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AllErrorsNeverContainPasswordValue()
    {
        const string password = "SadGallery1404";

        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [SeedUsersConfiguration.SharedPasswordEnvironmentVariable] = password,
            ["SeedUsers:0:UserName"] = "admin@example.invalid",
            ["SeedUsers:0:Role"] = "UnknownRole",
            ["SeedUsers:1:UserName"] = "admin@example.invalid", // تکراری + ایمیل نامعتبر
            ["SeedUsers:1:Email"] = "bad-email",
        });

        var errors = SeedUsersConfiguration.Validate(SeedUsersConfiguration.Read(configuration), AllowedRoles);

        Assert.NotEmpty(errors);
        Assert.DoesNotContain(errors, error => error.Contains(password, StringComparison.Ordinal));
    }
}
