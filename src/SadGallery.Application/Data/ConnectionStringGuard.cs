namespace SadGallery.Application.Data;

/// <summary>
/// نگهبان «شکل» رشته اتصال — پیش از آنکه مقدار به SqlClient برسد.
/// </summary>
/// <remarks>
/// چرا وجود دارد؟ پیام خطای سطح پایین SqlClient برای رشته نامعتبر این است:
/// <c>Format of the initialization string does not conform to specification starting at index 0.</c>
/// که برای اپراتور هیچ راهنمایی ندارد (BUG-009). این نگهبان علت را به فارسی و با راه‌حل می‌گوید.
/// <para>
/// قاعده امنیتی: **مقدار رشته اتصال هرگز روی خروجی نمی‌آید** (ممکن است رمز دیتابیس داشته باشد)؛
/// فقط «شکل» مشکل توصیف می‌شود.
/// </para>
/// </remarks>
public static class ConnectionStringGuard
{
    /// <summary>نام کلید تنظیمات (برای پیام‌ها).</summary>
    public const string ConfigurationKey = "ConnectionStrings:SadGallery";

    /// <summary>متغیر محیطی معادل کلید بالا (نگارش .NET با جداکنندهٔ دو زیرخط).</summary>
    public const string EnvironmentVariableName = "ConnectionStrings__SadGallery";

    private static readonly string[] ServerKeys = ["server", "data source", "addr", "address", "network address"];

    private static readonly string[] DatabaseKeys = ["database", "initial catalog", "attachdbfilename"];

    /// <summary>
    /// اگر رشته اتصال قابل استفاده باشد <c>null</c>، وگرنه پیام فارسیِ آمادهٔ نمایش برمی‌گرداند.
    /// </summary>
    public static string? Validate(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return BuildProblem(
                "تنظیم نشده است",
                "مقدار آن را در همان پنجرهٔ شل تعیین کنید (و در هیچ فایلی ذخیره نکنید)؛ نمونهٔ PowerShell:",
                $"$env:{EnvironmentVariableName} = \"Server=(localdb)\\MSSQLLocalDB;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True\"");
        }

        var value = connectionString.Trim();

        if (HasWrappingQuotes(value))
        {
            return BuildProblem(
                "با کوتیشن («\"» یا «'») آغاز/پایان می‌یابد",
                "این حالت وقتی رخ می‌دهد که نمونهٔ داخل فایل JSON/متن را با کوتیشن‌های خودش کپی کرده باشید. " +
                "کوتیشن‌ها را حذف کنید و مقدار را فقط داخل \"...\" خودِ شل بگذارید:",
                $"$env:{EnvironmentVariableName} = \"Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True\"");
        }

        if (LooksLikePlaceholder(value))
        {
            return BuildProblem(
                "به‌نظر می‌رسد متن جای‌نگهدار (placeholder) است، نه مقدار واقعی",
                "مقداری مثل <connection-string> یا HOST_FROM_ENV را با رشته اتصال واقعی خود عوض کنید:",
                $"$env:{EnvironmentVariableName} = \"Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True\"");
        }

        if (!value.Contains('=', StringComparison.Ordinal))
        {
            return BuildProblem(
                "هیچ جفت «کلید=مقدار» در آن نیست",
                "رشته اتصال باید از جفت‌های کلید=مقدار تشکیل شود که با «;» جدا می‌شوند:",
                $"$env:{EnvironmentVariableName} = \"Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True\"");
        }

        // «index 0» در خطای SqlClient یعنی نویسهٔ اول کلید معتبر نیست؛ رایج‌ترین علت، «=» ابتدایی است.
        if (value.StartsWith('=') || !char.IsLetter(value[0]))
        {
            return BuildProblem(
                "با نویسهٔ نامعتبر آغاز می‌شود (کلید باید با حروف شروع شود)",
                "مقدار را بررسی کنید؛ نمونهٔ درست:",
                $"$env:{EnvironmentVariableName} = \"Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True\"");
        }

        if (!ContainsAnyKey(value, ServerKeys))
        {
            return BuildProblem(
                "کلید سرور (Server یا Data Source) ندارد",
                "با این کلید مشخص می‌کنید به کدام نمونهٔ SQL Server وصل شوید (LocalDB، SQLEXPRESS یا سرور شبکه):",
                $"$env:{EnvironmentVariableName} = \"Server=(localdb)\\MSSQLLocalDB;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True\"");
        }

        if (!ContainsAnyKey(value, DatabaseKeys))
        {
            return BuildProblem(
                "کلید دیتابیس (Database یا Initial Catalog) ندارد",
                "نام دیتابیس هدف را مشخص کنید (اگر وجود نداشته باشد، دستور migration خودش آن را می‌سازد):",
                $"$env:{EnvironmentVariableName} = \"Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True\"");
        }

        return null;
    }

    private static string BuildProblem(string reason, string fixHint, string exampleCommand)
        => $"رشته اتصال «{ConfigurationKey}» نامعتبر است: {reason}. {fixHint}{Environment.NewLine}  {exampleCommand}{Environment.NewLine}"
           + "راهنمای کامل: docs/DEPLOYMENT.md §۴.۵ — عیب‌یابی §۴.۷. (مقدار رشته اتصال به‌عمد چاپ نمی‌شود)";

    private static bool HasWrappingQuotes(string value)
    {
        if (value.Length < 2)
        {
            return false;
        }

        var first = value[0];
        var last = value[^1];

        return (first == '"' && last == '"') || (first == '\'' && last == '\'');
    }

    private static bool LooksLikePlaceholder(string value)
    {
        // نمونه‌های رایج جای‌نگهدار در پیام‌ها و مستندات
        return value.Contains('<', StringComparison.Ordinal)
               || value.Contains('>', StringComparison.Ordinal)
               || value.Contains("HOST_FROM_ENV", StringComparison.OrdinalIgnoreCase)
               || value.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("***", StringComparison.Ordinal)
               || value.StartsWith("your-", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAnyKey(string connectionString, string[] keys)
    {
        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=', StringComparison.Ordinal);

            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();

            if (keys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
