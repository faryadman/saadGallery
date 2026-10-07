namespace SadGallery.Application.Market;

/// <summary>
/// تنظیمات زنجیره نرخ بازار. همه مقادیر از بخش <c>RateOptions</c> در appsettings خوانده می‌شوند
/// و اعتبارنامه‌ها **فقط** از Secret Store / متغیر محیطی می‌آیند (هرگز در فایل تنظیمات مخزن).
/// مقدار پیش‌فرض <see cref="Provider"/> عمداً <c>Disabled</c> است: تا وقتی مالک منبع را فعال نکند،
/// هیچ درخواست خروجی و هیچ نرخ ساختگی وجود ندارد (اصل «fail-closed»).
/// </summary>
public sealed class RateOptions
{
    public const string ProviderDisabled = "Disabled";
    public const string ProviderTgn = "Tgn";
    public const string ProviderFixture = "Fixture";

    public const string AnomalyPolicyManualReview = "ManualReview";
    public const string AnomalyPolicyAutoAccept = "AutoAccept";

    /// <summary>قالب آدرس سرویس نرخ؛ جای‌نگهدارها با اعتبارنامه پر می‌شوند و آدرس هرگز لاگ نمی‌شود.</summary>
    public const string DefaultEndpointTemplate = "https://webservice.tgnsrv.ir/Pr/Get/{username}/{password}";

    /// <summary>منبع فعال: Disabled | Tgn | Fixture (Fixture فقط برای توسعه/تست است).</summary>
    public string Provider { get; set; } = ProviderDisabled;

    /// <summary>بازه اجرای Job واریز نرخ (ثانیه). هم‌زمان حداقل فاصله بین دو درخواست خروجی است.</summary>
    public int FetchIntervalSeconds { get; set; } = 60;

    /// <summary>پس از این مدت، نرخ «کهنه» تلقی می‌شود و با برچسب «آخرین نرخ ثبت‌شده» نمایش داده می‌شود.</summary>
    public int StaleThresholdMinutes { get; set; } = 15;

    /// <summary>فراتر از این مدت، نرخ دیگر نمایش داده نمی‌شود (وضعیت نامعتبر).</summary>
    public int MaxStaleHours { get; set; } = 24;

    /// <summary>عمر مفید کش نمایش برای «تازه‌سازی از دیتابیس» (ثانیه).</summary>
    public int CacheTtlSeconds { get; set; } = 30;

    /// <summary>سقف حجم پاسخ پذیرفته‌شده (بایت) — محافظت از حافظه در برابر پاسخ غول‌آسا.</summary>
    public int MaxResponseBytes { get; set; } = 2_097_152;

    /// <summary>مهلت درخواست HTTP (ثانیه).</summary>
    public int HttpTimeoutSeconds { get; set; } = 10;

    /// <summary>دامنه‌های مجاز منبع نرخ (محافظت SSRF). فهرست خالی = هیچ دامنه‌ای مجاز نیست.</summary>
    public List<string> AllowedProviderDomains { get; set; } = [];

    /// <summary>آستانه هشدار «جهش غیرعادی» نسبت به آخرین مقدار ثبت‌شده (درصد).</summary>
    public decimal AnomalyChangeThresholdPercent { get; set; } = 50m;

    /// <summary>سیاست جهش: ManualReview (پیش‌فرض — منتشر نشود) | AutoAccept.</summary>
    public string AnomalyPolicy { get; set; } = AnomalyPolicyManualReview;

    /// <summary>سقف تأخیر عقب‌نشینی نمایی پس از خطا (دقیقه).</summary>
    public int MaxBackoffMinutes { get; set; } = 30;

    /// <summary>تأخیر نخستین اجرای Job پس از بالا آمدن برنامه (ثانیه).</summary>
    public int StartupDelaySeconds { get; set; } = 5;

    /// <summary>مدت اعتبار قفل دیتابیسی چند-نمونه‌ای (ثانیه). صفر = خودکار (مهلت HTTP + ۳۰ ثانیه).</summary>
    public int LeaseTtlSeconds { get; set; }

    /// <summary>در منبع Fixture، زمان اعلام نرخ به «اکنون» منتقل شود (فقط برای نمایش زنجیره در توسعه).</summary>
    public bool FixtureShiftQuotedTimeToNow { get; set; } = true;

    /// <summary>قالب آدرس سرویس نرخ.</summary>
    public string EndpointTemplate { get; set; } = DefaultEndpointTemplate;

    /// <summary>نام کاربری سرویس نرخ — فقط از <c>RateOptions__Username</c> یا User Secrets.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>رمز سرویس نرخ — فقط از <c>RateOptions__Password</c> یا User Secrets. هرگز چاپ/لاگ نمی‌شود.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>کلید متغیر محیطی نام کاربری (برای پیام‌های راهنمای خطا).</summary>
    public const string UsernameEnvironmentVariable = "RateOptions__Username";

    /// <summary>کلید متغیر محیطی رمز (برای پیام‌های راهنمای خطا).</summary>
    public const string PasswordEnvironmentVariable = "RateOptions__Password";

    /// <summary>بازه اجرای Job.</summary>
    public TimeSpan FetchInterval => TimeSpan.FromSeconds(FetchIntervalSeconds);

    /// <summary>عمر قفل دیتابیسی.</summary>
    public TimeSpan EffectiveLeaseTtl => LeaseTtlSeconds > 0
        ? TimeSpan.FromSeconds(LeaseTtlSeconds)
        : TimeSpan.FromSeconds(HttpTimeoutSeconds + 30);

    /// <summary>منبع Fixture فعال است؟ (فقط محیط توسعه مجاز است — بررسی در لایه Web)</summary>
    public bool IsFixtureProvider => string.Equals(Provider, ProviderFixture, StringComparison.OrdinalIgnoreCase);

    /// <summary>منبع واقعی فعال است؟</summary>
    public bool IsTgnProvider => string.Equals(Provider, ProviderTgn, StringComparison.OrdinalIgnoreCase);

    /// <summary>هیچ منبعی فعال نیست (حالت پیش‌فرض امن).</summary>
    public bool IsDisabled => string.Equals(Provider, ProviderDisabled, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// اعتبارسنجی تنظیمات. فهرست خطاها (فارسی)؛ فهرست خالی = سالم.
    /// هیچ‌گاه مقدار اعتبارنامه در پیام‌ها درج نمی‌شود — فقط نام متغیر محیطی.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!IsDisabled && !IsTgnProvider && !IsFixtureProvider)
        {
            errors.Add(
                $"مقدار RateOptions:Provider نامعتبر است (فقط {ProviderDisabled} یا {ProviderTgn} یا {ProviderFixture} مجاز است).");
        }

        if (FetchIntervalSeconds < 10)
        {
            errors.Add("RateOptions:FetchIntervalSeconds باید دست‌کم ۱۰ ثانیه باشد (احترام به محدودیت منبع).");
        }

        if (StaleThresholdMinutes < 1)
        {
            errors.Add("RateOptions:StaleThresholdMinutes باید دست‌کم ۱ دقیقه باشد.");
        }

        if (MaxStaleHours < 1 || MaxStaleHours * 60 < StaleThresholdMinutes)
        {
            errors.Add("RateOptions:MaxStaleHours باید دست‌کم ۱ و بزرگ‌تر از StaleThresholdMinutes باشد.");
        }

        if (CacheTtlSeconds is < 1 or > 3600)
        {
            errors.Add("RateOptions:CacheTtlSeconds باید بین ۱ تا ۳۶۰۰ ثانیه باشد.");
        }

        if (MaxResponseBytes < 1024)
        {
            errors.Add("RateOptions:MaxResponseBytes باید دست‌کم ۱۰۲۴ بایت باشد.");
        }

        if (HttpTimeoutSeconds is < 1 or > 60)
        {
            errors.Add("RateOptions:HttpTimeoutSeconds باید بین ۱ تا ۶۰ ثانیه باشد.");
        }

        if (AnomalyChangeThresholdPercent is < 1m or > 99m)
        {
            errors.Add("RateOptions:AnomalyChangeThresholdPercent باید بین ۱ تا ۹۹ درصد باشد.");
        }

        if (!string.Equals(AnomalyPolicy, AnomalyPolicyManualReview, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(AnomalyPolicy, AnomalyPolicyAutoAccept, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"RateOptions:AnomalyPolicy نامعتبر است (فقط {AnomalyPolicyManualReview} یا {AnomalyPolicyAutoAccept}).");
        }

        if (MaxBackoffMinutes < 1)
        {
            errors.Add("RateOptions:MaxBackoffMinutes باید دست‌کم ۱ دقیقه باشد.");
        }

        if (StartupDelaySeconds is < 0 or > 300)
        {
            errors.Add("RateOptions:StartupDelaySeconds باید بین ۰ تا ۳۰۰ ثانیه باشد.");
        }

        if (LeaseTtlSeconds < 0)
        {
            errors.Add("RateOptions:LeaseTtlSeconds نمی‌تواند منفی باشد.");
        }

        // بررسی قالب آدرس/دامنه و اعتبارنامه فقط وقتی لازم است که منبع واقعی فعال باشد؛
        // در حالت Disabled/Fixture هیچ درخواست خروجی وجود ندارد که بخواهد محدود شود.
        if (IsTgnProvider)
        {
            foreach (var domain in AllowedProviderDomains)
            {
                if (string.IsNullOrWhiteSpace(domain) ||
                    domain.Contains('/') ||
                    domain.Contains("://", StringComparison.Ordinal) ||
                    domain.Contains(' '))
                {
                    errors.Add("RateOptions:AllowedProviderDomains فقط نام دامنه مجاز است (بدون طرح و مسیر).");
                    break;
                }
            }

            var templateError = ValidateEndpointTemplate();
            if (templateError is not null)
            {
                errors.Add(templateError);
            }

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                errors.Add(
                    "اعتبارنامه‌های منبع نرخ تنظیم نشده است. متغیرهای محیطی " +
                    $"{UsernameEnvironmentVariable} و {PasswordEnvironmentVariable} را تنظیم کنید " +
                    "(User Secrets یا Secret Store در محیط عملیاتی). مقدارها هرگز چاپ نمی‌شوند.");
            }
        }

        return errors;
    }

    /// <summary>طبق ADR-0009 و ADR-0012: قالب آدرس باید HTTPS باشد و میزبان در فهرست مجاز باشد.</summary>
    public string? ValidateEndpointTemplate() => TgnEndpointTemplate.Validate(this);
}
