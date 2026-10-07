using SadGallery.Domain.Enums;

namespace SadGallery.Domain.Market;

/// <summary>
/// تعریف یک دارایی قابل نمایش: نگاشت «کلید منبع نرخ» به «کد پایدار داخلی» + قواعد واحد.
/// نکته حیاتی (ADR-0009 و ADR-0012): واحد هر دارایی صریح است؛ هیچ‌جای سامانه
/// «ریال» و «تومان» یکی گرفته نمی‌شوند و مقیاسِ منبع (<see cref="ProviderScale"/>)
/// فقط یک بار و در همین‌جا اعمال می‌شود.
/// </summary>
public sealed record AssetDefinition
{
    /// <summary>کد پایدار داخلی (لاتین، تغییرناپذیر پس از انتشار) — کلید رکوردها در دیتابیس.</summary>
    public required string Code { get; init; }

    /// <summary>کلید دقیق فیلد در پاسخ منبع نرخ (مثلاً <c>YekGram18</c>).</summary>
    public required string ProviderKey { get; init; }

    /// <summary>عنوان فارسی نمایش.</summary>
    public required string Title { get; init; }

    /// <summary>نوع دارایی.</summary>
    public required AssetKind Kind { get; init; }

    /// <summary>واحد مقدار پس از اعمال <see cref="ProviderScale"/>.</summary>
    public required CurrencyUnit QuoteUnit { get; init; }

    /// <summary>
    /// ضریب تبدیل مقدار خام منبع به مقدار کاملِ <see cref="QuoteUnit"/>.
    /// نمونه‌های واقعی منبع فعلی (docs/API.md): قیمت سکه‌ها با حذف سه صفر اعلام می‌شود ⇒ ۱۰۰۰؛
    /// انس نقره در هزار ضرب شده است ⇒ ۰.۰۰۱؛ سایر مقادیر ⇒ ۱.
    /// </summary>
    public decimal ProviderScale { get; init; } = 1m;

    /// <summary>
    /// کف بازه معقول (روی مقدار «پس از اعمال مقیاس»). کاربرد: تشخیص خطای واحد
    /// (مثلاً ارسال ریال به‌جای تومان ⇒ مقدار ۱۰ برابر). این بازه‌ها «داده بازار» نیستند؛
    /// فقط نگهبان (Guardrail) برای رد مقدار بی‌معنا هستند و در کد قابل تنظیم‌اند.
    /// </summary>
    public required decimal MinPlausible { get; init; }

    /// <summary>سقف بازه معقول (روی مقدار پس از اعمال مقیاس).</summary>
    public required decimal MaxPlausible { get; init; }
}
