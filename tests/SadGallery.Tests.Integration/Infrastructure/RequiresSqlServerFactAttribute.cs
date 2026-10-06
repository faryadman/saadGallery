using System.Runtime.CompilerServices;
using Xunit;

namespace SadGallery.Tests.Integration.Infrastructure;

/// <summary>
/// تست‌های نیازمند SQL Server.
/// اگر متغیر محیطی <c>SADGALLERY_TEST_SQL</c> تنظیم نباشد، تست به‌صورت صریح Skip می‌شود.
/// چرا؟ چون محیط ایجنت/CI ممکن است SQL Server نداشته باشد و «سبز شدن کاذب» بدتر از Skip است
/// (docs/TESTING.md §۱۱ — گزاره صداقت).
/// </summary>
/// <remarks>
/// xunit v3 برای Attributeهای مشتق از Fact، سازنده‌ای با «اطلاعات مبدأ» می‌خواهد
/// (قاعده xUnit3003) تا گزارش تست دقیق‌تر باشد؛ به همین دلیل پارامترهای Caller* اینجا هستند.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresSqlServerFactAttribute : FactAttribute
{
    public const string EnvironmentVariableName = "SADGALLERY_TEST_SQL";

    public RequiresSqlServerFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariableName)))
        {
            Skip = $"SQL Server integration test skipped: environment variable '{EnvironmentVariableName}' is not set. " +
                   "See docs/TESTING.md.";
        }
    }

    public static string? ConnectionString => Environment.GetEnvironmentVariable(EnvironmentVariableName);
}
