using SadGallery.Domain.Market;

namespace SadGallery.Application.Market;

/// <summary>دلیل رد یک قلم از پاسخ منبع نرخ.</summary>
public enum RateProblemReason
{
    /// <summary>مقدار <c>null</c> بود.</summary>
    NullValue = 2,

    /// <summary>مقدار عدد قابل‌تجزیه نبود.</summary>
    NotANumber = 3,

    /// <summary>نوع مقدار غیرمنتظره بود (شیء/آرایه/بولین).</summary>
    UnexpectedType = 4,

    /// <summary>زمان اعلام نرخ قابل‌تجزیه نبود.</summary>
    InvalidTime = 5,
}

/// <summary>یک قلم خامِ پذیرفته‌شده از پاسخ منبع (پیش از اعمال مقیاس و اعتبارسنجی).</summary>
public sealed record ParsedRate(string ProviderKey, string AssetCode, decimal RawValue);

/// <summary>مشکل یک قلم در پاسخ منبع.</summary>
public sealed record RateParseProblem(string ProviderKey, RateProblemReason Reason, string? Detail = null);

/// <summary>
/// نتیجه تجزیه پاسخ منبع نرخ. تجزیه هرگز استثنا پرتاب نمی‌کند؛ همه شکست‌ها به‌صورت
/// ساختارمند گزارش می‌شوند تا فراخوان بتواند سیاست (ثبت اجرا/عقب‌نشینی) را اعمال کند.
/// </summary>
public sealed record RateParseResult
{
    /// <summary>شکست بنیادی: پاسخ خالی/نامعتبر یا نبود زمان اعلام.</summary>
    public bool IsFatal { get; init; }

    /// <summary>کد شکست بنیادی: EmptyResponse | InvalidJson | UnexpectedRoot | MissingTime | InvalidTime.</summary>
    public string? FatalCode { get; init; }

    /// <summary>توضیح فنی شکست (بدون داده حساس).</summary>
    public string? FatalDetail { get; init; }

    /// <summary>خطای اعلام‌شده توسط خود منبع (مثلاً <c>Unauthorized</c>) — بند ۷ مستندات.</summary>
    public string? ProviderError { get; init; }

    /// <summary>زمان اعلام نرخ به UTC (از فیلد <c>TimeRead</c> با فرض وقت ایران).</summary>
    public DateTimeOffset? QuotedAtUtc { get; init; }

    /// <summary>مقدار خام فیلد زمان (برای گزارش/ردیابی).</summary>
    public string? TimeRaw { get; init; }

    /// <summary>اقلام خام پذیرفته‌شده.</summary>
    public IReadOnlyList<ParsedRate> Rates { get; init; } = [];

    /// <summary>مشکلات اقلام.</summary>
    public IReadOnlyList<RateParseProblem> Problems { get; init; } = [];

    /// <summary>کلیدهای ناشناخته پاسخ (قرارداد منبع می‌تواند تغییر کند) — برای آگاهی اپراتور.</summary>
    public IReadOnlyList<string> UnmappedKeys { get; init; } = [];

    /// <summary>
    /// دارایی‌هایی که کلیدشان در این پاسخ نبود. **خطا نیست**: بند ۶ مستندات سرویس صریحاً
    /// می‌گوید تعداد و ترتیب اقلام تضمین‌شده نیست؛ فقط برای پایش ثبت می‌شود.
    /// دارایی غایب، نرخ قبلی خود را نگه می‌دارد و با گذر زمان «کهنه» برچسب می‌خورد.
    /// </summary>
    public IReadOnlyList<string> MissingKeys { get; init; } = [];

    /// <summary>ساخت نتیجه شکست بنیادی.</summary>
    public static RateParseResult Fatal(string code, string? detail = null) =>
        new() { IsFatal = true, FatalCode = code, FatalDetail = detail };

    /// <summary>هیچ قلمی در پاسخ نبود (بدون شکست بنیادی).</summary>
    public bool IsEmpty => !IsFatal && Rates.Count == 0 && UnmappedKeys.Count == 0;
}

/// <summary>متن فارسی دلایل مشکل (برای گزارش CLI و لاگ بدون داده حساس).</summary>
public static class RateProblemText
{
    /// <summary>توضیح فارسی دلیل.</summary>
    public static string ToPersian(RateProblemReason reason) => reason switch
    {
        RateProblemReason.NullValue => "مقدار null بود",
        RateProblemReason.NotANumber => "مقدار عدد معتبر نبود",
        RateProblemReason.UnexpectedType => "نوع مقدار غیرمنتظره بود",
        RateProblemReason.InvalidTime => "زمان اعلام نرخ قابل‌تجزیه نبود",
        _ => "دلیل نامشخص",
    };
}
