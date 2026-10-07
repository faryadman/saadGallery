namespace SadGallery.Application.Market;

/// <summary>
/// ساخت و «پوشاندن» آدرس سرویس نرخ. اعتبارنامه در **مسیر آدرس** قرار می‌گیرد (قرارداد سرویس)،
/// بنابراین:
/// <list type="bullet">
///   <item>آدرس واقعی هرگز لاگ/چاپ نمی‌شود؛ فقط <see cref="Describe"/> (با ***) مجاز است.</item>
///   <item>آدرس باید https و میزبانش در <c>RateOptions:AllowedProviderDomains</c> باشد (محافظت SSRF).</item>
/// </list>
/// </summary>
public static class TgnEndpointTemplate
{
    private const string UsernamePlaceholder = "{username}";
    private const string PasswordPlaceholder = "{password}";

    /// <summary>اعتبارسنجی قالب/دامنه. <c>null</c> = بی‌ایراد.</summary>
    public static string? Validate(RateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.EndpointTemplate))
        {
            return "RateOptions:EndpointTemplate تنظیم نشده است.";
        }

        if (!options.EndpointTemplate.Contains(UsernamePlaceholder, StringComparison.OrdinalIgnoreCase) ||
            !options.EndpointTemplate.Contains(PasswordPlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            return "RateOptions:EndpointTemplate باید شامل جای‌نگهدارهای {username} و {password} باشد.";
        }

        if (!Uri.TryCreate(options.EndpointTemplate, UriKind.Absolute, out var uri))
        {
            return "RateOptions:EndpointTemplate یک آدرس مطلق معتبر نیست.";
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return "RateOptions:EndpointTemplate باید با https باشد (اعتبارنامه در مسیر آدرس قرار می‌گیرد).";
        }

        if (options.AllowedProviderDomains.Count == 0)
        {
            return "RateOptions:AllowedProviderDomains خالی است؛ برای جلوگیری از SSRF باید دست‌کم یک دامنه مجاز تعریف شود.";
        }

        var allowed = options.AllowedProviderDomains.Any(domain =>
            string.Equals(domain.Trim(), uri.Host, StringComparison.OrdinalIgnoreCase));

        return allowed
            ? null
            : $"دامنه منبع نرخ («{uri.Host}») در RateOptions:AllowedProviderDomains مجاز نشده است.";
    }

    /// <summary>ساخت آدرس نهایی با اعتبارنامه (مقدارها URL-encode می‌شوند).</summary>
    public static Uri Build(RateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var error = Validate(options);

        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        var url = options.EndpointTemplate
            .Replace(UsernamePlaceholder, Uri.EscapeDataString(options.Username), StringComparison.OrdinalIgnoreCase)
            .Replace(PasswordPlaceholder, Uri.EscapeDataString(options.Password), StringComparison.OrdinalIgnoreCase);

        return new Uri(url, UriKind.Absolute);
    }

    /// <summary>
    /// توصیف امن آدرس برای لاگ/گزارش: جای‌نگهدارها با *** پوشانده می‌شوند.
    /// هرگز از آدرس ساخته‌شده استفاده نکنید — این متد فقط از قالب می‌خواند.
    /// </summary>
    public static string Describe(RateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.EndpointTemplate
            .Replace(UsernamePlaceholder, "***", StringComparison.OrdinalIgnoreCase)
            .Replace(PasswordPlaceholder, "***", StringComparison.OrdinalIgnoreCase);
    }
}
