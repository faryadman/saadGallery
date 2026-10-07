namespace SadGallery.Application.Market;

/// <summary>
/// نگهبان «جهش غیرعادی»: تغییر درصدی نرخ نسبت به آخرین مقدار ثبت‌شده.
/// کاربرد: خطای انسانی در منبع، تغییر یک‌باره با ضریب ۱۰ (ریال/تومان) یا داده خراب.
/// سیاست پیش‌فرض پروژه <c>ManualReview</c> است: نرخ مشکوک **منتشر نمی‌شود** و فقط برای
/// بررسی اپراتور ثبت می‌گردد (فاز ۵: رابط تأیید).
/// </summary>
public sealed class RateAnomalyDetector
{
    /// <summary>ساخت نگهبان با آستانه درصدی.</summary>
    public RateAnomalyDetector(decimal thresholdPercent)
    {
        if (thresholdPercent is < 1m or > 99m)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholdPercent), "آستانه باید بین ۱ تا ۹۹ درصد باشد.");
        }

        ThresholdPercent = thresholdPercent;
    }

    /// <summary>آستانه (درصد).</summary>
    public decimal ThresholdPercent { get; }

    /// <summary>
    /// آیا تغییر نسبت به مقدار قبلی غیرعادی است؟ مقدار قبلی نامعلوم/نامعتبر ⇒ «غیرعادی» نیست
    /// (نخستین ثبت هر دارایی هرگز مشکوک تلقی نمی‌شود).
    /// </summary>
    public bool IsAnomalous(decimal? previousAmount, decimal currentAmount)
    {
        if (previousAmount is not > 0m)
        {
            return false;
        }

        var changePercent = Math.Abs(currentAmount - previousAmount.Value) / previousAmount.Value * 100m;

        return changePercent >= ThresholdPercent;
    }
}
