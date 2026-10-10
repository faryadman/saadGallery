namespace SadGallery.Application.Media;

/// <summary>
/// تنظیمات بارگذاری و نگهداری رسانه (فاز ۴).
/// از بخش <c>MediaOptions</c> در appsettings خوانده می‌شود.
/// </summary>
public sealed class MediaOptions
{
    /// <summary>
    /// ریشهٔ نگهداری فایل‌ها. مقدار نسبی نسبت به ContentRoot تعبیر می‌شود.
    /// </summary>
    /// <remarks>
    /// پیش‌فرض <c>App_Data/media</c> است: بیرون از پوشهٔ ارائه‌شده (wwwroot) و در پوشه‌ای که
    /// ASP.NET Core به‌طور پیش‌فرض هرگز سرو نمی‌کند. برای محیط عملیاتی، مسیر مطلقی
    /// خارج از پوشهٔ برنامه تنظیم کنید (راهنما: docs/DEPLOYMENT.md).
    /// نام این تنظیم با کلیدِ ازپیش‌موجودِ <c>StorageOptions:UploadsRoot</c> یکی است
    /// تا دو بخشِ تنظیماتِ هم‌پوشان ساخته نشود.
    /// </remarks>
    public string UploadsRoot { get; set; } = "App_Data/media";

    /// <summary>نام زیرپوشه برای تصاویر عمومی.</summary>
    public string PublicSubPath { get; set; } = "public";

    /// <summary>نام زیرپوشه برای فایل‌های خصوصی.</summary>
    public string PrivateSubPath { get; set; } = "private";

    /// <summary>بیشینهٔ حجم هر فایل (بایت) — پیش‌فرض ۵ مگابایت.</summary>
    public int MaxUploadBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>بیشینهٔ تعداد تصویر برای هر محصول.</summary>
    public int MaxImagesPerProduct { get; set; } = 8;

    /// <summary>بیشینهٔ اندازهٔ ضلعِ نسخهٔ ذخیره‌شده (پیکسل).</summary>
    public int StoredMaxSide { get; set; } = 1600;

    /// <summary>
    /// بیشینهٔ پهنای تصویر ورودی (پیکسل). سدی در برابر «بمبِ فشرده‌سازی»:
    /// یک فایل کوچک می‌تواند به تصویری با میلیون‌ها پیکسل رمزگشایی شود و حافظه را ببلعد.
    /// ابعاد از روی سرآیند و پیش از تخصیص حافظه خوانده می‌شوند.
    /// </summary>
    public int MaxWidth { get; set; } = 8000;

    /// <summary>بیشینهٔ بلندای تصویر ورودی (پیکسل).</summary>
    public int MaxHeight { get; set; } = 8000;

    /// <summary>اندازهٔ ضلع بندانگشتی (پیکسل).</summary>
    public int ThumbnailSide { get; set; } = 480;

    /// <summary>کیفیت بازرمزگذاری (۰ تا ۱۰۰).</summary>
    public int Quality { get; set; } = 82;

    /// <summary>قالب خروجیِ بازرمزگذاری‌شده.</summary>
    public Abstractions.ImageFormat OutputFormat { get; set; } = Abstractions.ImageFormat.WebP;

    /// <summary>نشانی عمومی برای تصاویر محصول.</summary>
    public string PublicRequestPath { get; set; } = "/media/products";

    /// <summary>
    /// قالب‌های مجاز بر اساس «ساختار واقعی» (تعیین‌شده توسط پردازشگر تصویر).
    /// این فهرست برای نمایش راهنما به کاربر است؛ تصمیم نهایی را رمزگشایی واقعی می‌گیرد.
    /// </summary>
    public IReadOnlyList<string> AcceptedLabels { get; set; } = ["JPEG", "PNG", "WebP"];

    /// <summary>
    /// اعتبارسنجی تنظیمات. مانند <c>RateOptions</c> خطاها را برمی‌گرداند تا لایه وب
    /// بتواند آن‌ها را لاگ کند (بدون متوقف کردن کل سایت).
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(UploadsRoot))
        {
            errors.Add("StorageOptions:UploadsRoot خالی است.");
        }

        if (MaxUploadBytes <= 0)
        {
            errors.Add("StorageOptions:MaxUploadBytes باید بزرگ‌تر از صفر باشد.");
        }

        if (MaxImagesPerProduct is < 1 or > 50)
        {
            errors.Add("StorageOptions:MaxImagesPerProduct باید بین ۱ و ۵۰ باشد.");
        }

        if (StoredMaxSide < 64 || StoredMaxSide > 4096)
        {
            errors.Add("StorageOptions:StoredMaxSide باید بین ۶۴ و ۴۰۹۶ باشد.");
        }

        if (ThumbnailSide < 32 || ThumbnailSide > 1024)
        {
            errors.Add("StorageOptions:ThumbnailSide باید بین ۳۲ و ۱۰۲۴ باشد.");
        }

        if (MaxWidth < 256 || MaxHeight < 256)
        {
            errors.Add("StorageOptions:MaxWidth و MaxHeight باید دست‌کم ۲۵۶ پیکسل باشند.");
        }

        if (Quality is < 40 or > 100)
        {
            errors.Add("StorageOptions:Quality باید بین ۴۰ و ۱۰۰ باشد.");
        }

        if (string.IsNullOrWhiteSpace(PublicSubPath) || string.IsNullOrWhiteSpace(PrivateSubPath))
        {
            errors.Add("StorageOptions:PublicSubPath و PrivateSubPath نمی‌توانند خالی باشند.");
        }
        else if (string.Equals(PublicSubPath.Trim(), PrivateSubPath.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("StorageOptions: مسیر عمومی و خصوصی نمی‌توانند یکسان باشند (تفکیک اجباری است).");
        }

        if (!PublicRequestPath.StartsWith('/'))
        {
            errors.Add("StorageOptions:PublicRequestPath باید با / شروع شود.");
        }

        return errors;
    }
}
