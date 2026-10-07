using System.Reflection;
using System.Text.RegularExpressions;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Text;

namespace SadGallery.Application.Market;

/// <summary>
/// منبع «نمونهٔ آزمایشی» برای توسعه و تست بدون اعتبارنامه. پاسخ واقعی ارائه‌شده توسط مالک
/// (۱۴۰۱/۰۳/۱۹) به‌صورت منبع جاسازی‌شده بازخوانی می‌شود.
/// <para>
/// **قواعد ایمنی:** این منبع فقط وقتی فعال می‌شود که <c>RateOptions:Provider = Fixture</c> باشد،
/// و لایه Web فعال‌سازی آن در محیط غیرتوسعه را رد می‌کند. نرخ‌های آن در نمایش با برچسب
/// صریح «نمونهٔ آزمایشی» می‌آیند و هرگز به‌عنوان نرخ روز نشان داده نمی‌شوند.
/// </para>
/// </summary>
public sealed class FixtureRateProvider : IRateProvider
{
    /// <summary>شناسه ثابت این منبع (در دیتابیس/نمایش به‌کار می‌رود).</summary>
    public const string Id = "Fixture";

    private const string ResourceName = "SadGallery.Application.Market.Fixture.tgn-sample.json";

    private readonly IClock _clock;
    private readonly RateOptions _options;
    private readonly Lazy<string> _payload;

    /// <summary>ساخت منبع نمونه.</summary>
    public FixtureRateProvider(IClock clock, RateOptions options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _options = options;
        _payload = new Lazy<string>(ReadFixture, isThreadSafe: true);
    }

    /// <inheritdoc />
    public string ProviderId => Id;

    /// <inheritdoc />
    public bool IsConfigured => true;

    /// <inheritdoc />
    public Task<RateProviderFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = _clock.UtcNow;
        var body = _payload.Value;

        if (_options.FixtureShiftQuotedTimeToNow)
        {
            // زمان نمونه در سال ۱۴۰۱ است؛ برای مشاهده زنجیره کامل در توسعه، زمان اعلام را
            // به «اکنون» منتقل می‌کنیم. برچسب «نمونهٔ آزمایشی» در نمایش حفظ می‌شود.
            var shifted = _clock.UtcNow.ToOffset(TehranTime.Offset);
            body = Regex.Replace(
                body,
                "\"TimeRead\":\"[^\"]*\"",
                $"\"TimeRead\":\"{shifted:yyyy/MM/dd HH:mm:ss}\"");
        }

        var parse = TgnResponseParser.Parse(body);

        return Task.FromResult(RateProviderFetchResult.Ok(ProviderId, now, parse, durationMs: 0));
    }

    private static string ReadFixture()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"منبع جاسازی‌شده «{ResourceName}» یافت نشد؛ تنظیم EmbeddedResource در csproj بررسی شود.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
