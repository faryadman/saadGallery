using System.Globalization;

namespace SadGallery.Application.Text;

/// <summary>
/// زمان سرویس نرخ بدون Timezone را به زمان مطلق تبدیل می‌کند.
/// <para>
/// **فرض ثبت‌شده (ADR-0012):** فیلد <c>TimeRead</c> بدون Timezone اعلام می‌شود و مربوط به
/// وقت رسمی ایران است. از ۱ فروردین ۱۴۰۱ (۲۰۲۲-۰۳-۲۱) ساعت رسمی ایران «تغییر ساعت تابستانی»
/// ندارد و ثابت <c>+03:30</c> است؛ لذا این کلاس آفست ثابت را اعمال می‌کند و به tzdata
/// سیستم وابسته نیست (سازگار با Windows و Linux).
/// </para>
/// </summary>
public static class TehranTime
{
    /// <summary>آفست ثابت ایران: UTC+03:30 (بدون DST از سال ۲۰۲۲).</summary>
    public static readonly TimeSpan Offset = new(3, 30, 0);

    private static readonly string[] AcceptedFormats =
    [
        "yyyy/MM/dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy/MM/dd HH:mm",
        "yyyy-MM-dd HH:mm",
    ];

    /// <summary>
    /// تجزیه زمان «بدون Timezone». ارقام فارسی/عربی و جداکننده‌های هزارگان نیز پذیرفته می‌شوند.
    /// </summary>
    public static bool TryParse(string? raw, out DateTimeOffset value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = PersianText.ToAsciiDigits(raw)
            .Replace("\u066C", string.Empty, StringComparison.Ordinal) // ٬ جداکننده هزارگان
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (!DateTime.TryParseExact(
                normalized,
                AcceptedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var naive))
        {
            return false;
        }

        if (naive.Year is < 1900 or > 2200)
        {
            return false;
        }

        value = new DateTimeOffset(DateTime.SpecifyKind(naive, DateTimeKind.Unspecified), Offset);
        return true;
    }
}
