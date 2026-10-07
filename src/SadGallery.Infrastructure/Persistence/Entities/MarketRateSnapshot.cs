using SadGallery.Domain.Enums;

namespace SadGallery.Infrastructure.Persistence.Entities;

/// <summary>
/// رکورد تاریخی یک نرخ. جدول فقط «افزودنی» است؛ بازنویسی تاریخچه ممنوع.
/// همه ستون‌های هویتی/زمانی اجباری‌اند تا معیار پذیرش فاز ۲ در سطح دیتابیس هم تضمین شود:
/// هیچ نرخی بدون واحد، زمان اعلام، زمان دریافت، شناسه منبع و وضعیت اعتبار ذخیره نمی‌شود.
/// </summary>
public class MarketRateSnapshot
{
    /// <summary>شناسه (افزایشی؛ بزرگ‌ترین شناسه هر دارایی = آخرین نرخ آن).</summary>
    public long Id { get; set; }

    /// <summary>کد پایدار دارایی (AssetCatalog).</summary>
    public string AssetCode { get; set; } = string.Empty;

    /// <summary>مقدار بر حسب <see cref="QuoteUnit"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>واحد مقدار (Irr/Irt/Usd/Usdt).</summary>
    public CurrencyUnit QuoteUnit { get; set; }

    /// <summary>شناسه منبع نرخ.</summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>زمان اعلام نرخ توسط منبع (UTC).</summary>
    public DateTimeOffset QuotedAtUtc { get; set; }

    /// <summary>زمان دریافت ما از منبع (UTC).</summary>
    public DateTimeOffset FetchedAtUtc { get; set; }

    /// <summary>وضعیت اعتبار در لحظه ثبت.</summary>
    public RateQuality Quality { get; set; }

    /// <summary>مقدار خام منبع (پیش از اعمال مقیاس) — برای ردیابی.</summary>
    public decimal? ProviderRawValue { get; set; }

    /// <summary>مقیاس اعمال‌شده.</summary>
    public decimal ScaleApplied { get; set; } = 1m;

    /// <summary>جهش غیرعادی مشکوک (منتشر نمی‌شود؛ فقط برای بررسی اپراتور).</summary>
    public bool IsAnomalySuspected { get; set; }

    /// <summary>شناسه اجرای مربوطه (PriceFetchRun).</summary>
    public long? FetchRunId { get; set; }

    /// <summary>زمان ایجاد رکورد (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}
