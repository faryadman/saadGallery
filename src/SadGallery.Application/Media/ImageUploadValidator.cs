using SadGallery.Application.Abstractions;

namespace SadGallery.Application.Media;

/// <summary>فایل دریافت‌شده از کاربر، پیش از هر تصمیمی.</summary>
public sealed record UploadCandidate(
    string FileName,
    string? ContentType,
    long Length,
    ReadOnlyMemory<byte> Content);

/// <summary>نتیجه اعتبارسنجی آپلود.</summary>
public sealed record ImageValidationResult
{
    public bool IsAccepted { get; init; }

    /// <summary>دلیل رد، به فارسی و قابل نمایش مستقیم به کاربر.</summary>
    public string? Reason { get; init; }

    /// <summary>اطلاعات تصویر پس از رمزگشایی واقعی (فقط در صورت پذیرش).</summary>
    public ImageInspection? Inspection { get; init; }

    public static ImageValidationResult Reject(string reason) => new() { IsAccepted = false, Reason = reason };

    public static ImageValidationResult Accept(ImageInspection inspection) =>
        new() { IsAccepted = true, Inspection = inspection };
}

/// <summary>
/// اعتبارسنج آپلود تصویر.
/// </summary>
/// <remarks>
/// <para>
/// ترتیب بررسی‌ها عمداً از «ارزان و راهنما» به «واقعی و قطعی» است:
/// حجم ← پسوند (راهنما) ← <b>رمزگشایی واقعی</b> ← ابعاد.
/// </para>
/// <para>
/// نکتهٔ امنیتیِ مهم: <b>Content-Type تصمیم‌گیرنده نیست.</b> این مقدار توسط کلاینت
/// فرستاده می‌شود و به‌راحتی جعل می‌شود؛ فقط برای گزارش ثبت می‌گردد. تصمیم نهایی را
/// <see cref="IImageProcessor.Inspect"/> می‌گیرد که فایل را واقعاً رمزگشایی می‌کند
/// (معیار پذیرش فاز ۴: رد فایلی که پسوند/نوع آن جعل شده).
/// </para>
/// </remarks>
public sealed class ImageUploadValidator
{
    /// <summary>پسوندهایی که به‌عنوان «راهنما» پذیرفته می‌شوند.</summary>
    private static readonly string[] HintExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly IImageProcessor _processor;
    private readonly MediaOptions _options;

    public ImageUploadValidator(IImageProcessor processor, MediaOptions options)
    {
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(options);

        _processor = processor;
        _options = options;
    }

    public ImageValidationResult Validate(UploadCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.Length <= 0 || candidate.Content.Length == 0)
        {
            return ImageValidationResult.Reject("فایل انتخاب‌شده خالی است.");
        }

        if (candidate.Length > _options.MaxUploadBytes)
        {
            var limitMegaBytes = _options.MaxUploadBytes / 1024d / 1024d;
            return ImageValidationResult.Reject(
                $"حجم فایل بیش از مجاز است (بیشینهٔ مجاز: {limitMegaBytes:0.#} مگابایت).");
        }

        // بررسیِ پسوند فقط برای راهنماییِ بهتر کاربر است؛ پذیرش بر عهدهٔ رمزگشایی واقعی است.
        var extension = Path.GetExtension(candidate.FileName);

        if (!string.IsNullOrEmpty(extension) &&
            !HintExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return ImageValidationResult.Reject(
                "قالب فایل پشتیبانی نمی‌شود. فقط تصویرهای JPEG، PNG و WebP قابل بارگذاری هستند.");
        }

        // تصمیم قطعی: ساختار واقعی فایل
        var inspection = _processor.Inspect(candidate.Content.Span);

        if (inspection is null)
        {
            return ImageValidationResult.Reject(
                "فایل ارسالی یک تصویر معتبر نیست. (نام یا نوع فایل تغییر داده شده است؛ " +
                "ساختار واقعی آن تصویر نبود.)");
        }

        if (inspection.Width <= 0 || inspection.Height <= 0)
        {
            return ImageValidationResult.Reject("ابعاد تصویر نامعتبر است.");
        }

        if (inspection.Width > _options.MaxWidth || inspection.Height > _options.MaxHeight)
        {
            return ImageValidationResult.Reject(
                $"ابعاد تصویر ({inspection.Width}×{inspection.Height}) بیش از حد مجاز است " +
                $"(بیشینه: {_options.MaxWidth}×{_options.MaxHeight} پیکسل).");
        }

        return ImageValidationResult.Accept(inspection);
    }
}
