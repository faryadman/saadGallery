using SadGallery.Domain.Enums;

namespace SadGallery.Application.Market;

/// <summary>
/// سیاست تازگی/اعتبار نرخ. «تازگی» بر مبنای **زمان اعلام منبع** سنجیده می‌شود، نه زمان دریافت ما؛
/// چون منبع ممکن است ساعت‌ها پیش نرخ را اعلام کرده باشد و ما تازه دریافت کرده باشیم.
/// <para>پنجره‌ها: Live تا <see cref="LiveWindow"/> · Delayed تا <see cref="StaleAfter"/> ·
/// Stale تا <see cref="MaxAge"/> · فراتر از آن یا زمان آینده ⇒ Invalid (نمایش داده نمی‌شود).</para>
/// </summary>
public sealed class RateFreshnessPolicy
{
    /// <summary>پیش‌فرض حداکثر انحراف زمانی آینده (ساعت) پیش از Invalid شدن.</summary>
    public const int DefaultMaxFutureSkewHours = 6;

    /// <summary>ساخت سیاست از تنظیمات.</summary>
    public RateFreshnessPolicy(TimeSpan liveWindow, TimeSpan staleAfter, TimeSpan maxAge, TimeSpan maxFutureSkew)
    {
        if (liveWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(liveWindow));
        }

        if (staleAfter < liveWindow)
        {
            throw new ArgumentOutOfRangeException(nameof(staleAfter), "بازه کهنه‌شدن باید بزرگ‌تر از پنجره زنده باشد.");
        }

        if (maxAge < staleAfter)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAge), "حداکثر عمر مجاز باید بزرگ‌تر از بازه کهنه‌شدن باشد.");
        }

        LiveWindow = liveWindow;
        StaleAfter = staleAfter;
        MaxAge = maxAge;
        MaxFutureSkew = maxFutureSkew;
    }

    /// <summary>ساخت از تنظیمات پروژه: پنجره زنده = دو برابر بازه واریز (حداقل ۲ دقیقه).</summary>
    public static RateFreshnessPolicy FromOptions(RateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var doubled = TimeSpan.FromSeconds(options.FetchIntervalSeconds * 2.0);
        var liveWindow = doubled > TimeSpan.FromMinutes(2) ? doubled : TimeSpan.FromMinutes(2);

        return new RateFreshnessPolicy(
            liveWindow,
            TimeSpan.FromMinutes(options.StaleThresholdMinutes),
            TimeSpan.FromHours(options.MaxStaleHours),
            TimeSpan.FromHours(DefaultMaxFutureSkewHours));
    }

    /// <summary>نرخ تا این مدت پس از اعلام، «لحظه‌ای» تلقی می‌شود.</summary>
    public TimeSpan LiveWindow { get; }

    /// <summary>نرخ تا این مدت پس از اعلام، «تأخیری» تلقی می‌شود.</summary>
    public TimeSpan StaleAfter { get; }

    /// <summary>فراتر از این مدت، نرخ نامعتبر است و نمایش داده نمی‌شود.</summary>
    public TimeSpan MaxAge { get; }

    /// <summary>تحمل انحراف ساعت منبع به سمت آینده.</summary>
    public TimeSpan MaxFutureSkew { get; }

    /// <summary>ارزیابی وضعیت نرخ در لحظه <paramref name="nowUtc"/>.</summary>
    public RateQuality Evaluate(DateTimeOffset quotedAtUtc, DateTimeOffset nowUtc)
    {
        if (quotedAtUtc > nowUtc + MaxFutureSkew)
        {
            return RateQuality.Invalid;
        }

        var age = nowUtc - quotedAtUtc;

        if (age <= LiveWindow)
        {
            return RateQuality.Live;
        }

        if (age <= StaleAfter)
        {
            return RateQuality.Delayed;
        }

        return age <= MaxAge ? RateQuality.Stale : RateQuality.Invalid;
    }

    /// <summary>ارزیابی همراه با توضیح فارسی وضعیت (برای نمایش/لاگ).</summary>
    public (RateQuality Quality, string Description) EvaluateWithReason(DateTimeOffset quotedAtUtc, DateTimeOffset nowUtc)
    {
        var quality = Evaluate(quotedAtUtc, nowUtc);

        var description = quality switch
        {
            RateQuality.Live => "نرخ در بازه موردانتظار دریافت شده است.",
            RateQuality.Delayed => "نرخ با تأخیر دریافت شده است.",
            RateQuality.Stale => "نرخ کهنه است؛ باید با برچسب «آخرین نرخ ثبت‌شده» نمایش داده شود.",
            _ => "نرخ نامعتبر است (زمان اعلام بسیار قدیمی یا در آینده).",
        };

        return (quality, description);
    }
}
