using SadGallery.Application.Data;
using Xunit;

namespace SadGallery.Tests.Unit.Data;

/// <summary>
/// تست‌های نگهبان رشته اتصال (BUG-009).
/// هدف: جایگزینی خطای مبهم <c>Format of the initialization string ... index 0</c>
/// با پیام فارسیِ راهنما، و تضمین اینکه **مقدار رشته اتصال در هیچ پیامی چاپ نمی‌شود**.
/// </summary>
public sealed class ConnectionStringGuardTests
{
    private const string Valid = "Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True";

    [Theory]
    [InlineData("Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True")]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True")]
    [InlineData("Data Source=localhost\\SQLEXPRESS;Initial Catalog=SadGallery;Integrated Security=True;TrustServerCertificate=True")]
    [InlineData("Server=127.0.0.1,1433;Database=SadGallery;User Id=sa;Password=SomePass123;TrustServerCertificate=True")]
    [InlineData("server=localhost;database=SadGallery;trusted_connection=true")] // حساسیت به بزرگی/کوچکی حروف ندارد
    public void ValidConnectionStrings_AreAccepted(string connectionString)
    {
        Assert.Null(ConnectionStringGuard.Validate(connectionString));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingValue_IsRejected_WithEnvironmentVariableGuidance(string? connectionString)
    {
        var error = ConnectionStringGuard.Validate(connectionString);

        Assert.NotNull(error);
        Assert.Contains(ConnectionStringGuard.EnvironmentVariableName, error, StringComparison.Ordinal);
        Assert.Contains("تنظیم نشده", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Placeholder_IsRejected_WithClearReason()
    {
        var error = ConnectionStringGuard.Validate("<connection-string>");

        Assert.NotNull(error);
        Assert.Contains("جای‌نگهدار", error, StringComparison.Ordinal);
    }

    [Fact]
    public void WrappingQuotes_AreRejected_BecauseTheyAreACopyPasteTrap()
    {
        // الگوی رایج: کپی نمونهٔ JSON با کوتیشن‌های خودش
        var error = ConnectionStringGuard.Validate($"\"{Valid}\"");

        Assert.NotNull(error);
        Assert.Contains("کوتیشن", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValueWithoutKeyValuePair_IsRejected()
    {
        // این همان ورودی‌ای است که پیام مبهم «starting at index 0» را می‌داد
        var error = ConnectionStringGuard.Validate("ThisIsNotAConnectionString");

        Assert.NotNull(error);
        Assert.Contains("کلید=مقدار", error, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingServerKey_IsRejected()
    {
        var error = ConnectionStringGuard.Validate("Database=SadGallery;Trusted_Connection=True");

        Assert.NotNull(error);
        Assert.Contains("Server", error, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingDatabaseKey_IsRejected()
    {
        var error = ConnectionStringGuard.Validate("Server=localhost;Trusted_Connection=True");

        Assert.NotNull(error);
        Assert.Contains("دیتابیس", error, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryErrorMessage_ContainsFixHintAndDocPointer()
    {
        string[] badValues =
        [
            "",
            "<connection-string>",
            $"\"{Valid}\"",
            "NotAConnectionString",
            "Database=SadGallery;",
            "Server=localhost;",
        ];

        foreach (var badValue in badValues)
        {
            var error = ConnectionStringGuard.Validate(badValue);

            Assert.NotNull(error);
            Assert.Contains("docs/DEPLOYMENT.md", error, StringComparison.Ordinal);
            Assert.Contains(ConnectionStringGuard.EnvironmentVariableName, error, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PasswordValue_IsNeverEchoed_InAnyErrorMessage()
    {
        // مقدار حساس در انتهای رشته به شکل نامعتبر قرار می‌گیرد تا خطا تولید شود
        const string secret = "Sup3rSecret!Pass";
        var error = ConnectionStringGuard.Validate($"Server=localhost;Database=SadGallery;Password={secret}<");

        Assert.NotNull(error);
        Assert.DoesNotContain(secret, error, StringComparison.Ordinal);
    }
}
