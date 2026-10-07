using System.Globalization;

namespace SadGallery.Application.Text;

/// <summary>
/// نمایش تاریخ/ساعت به تقویم هجری شمسی و وقت ایران (لایه نمایش — منطق تجاری نیست).
/// همه ورودی‌ها UTC هستند و پیش از نمایش به وقت ایران (<see cref="TehranTime.Offset"/>) تبدیل می‌شوند.
/// </summary>
public static class PersianDate
{
    /// <summary>تاریخ شمسی مانند <c>۱۴۰۱/۰۳/۱۹</c>.</summary>
    public static string ToJalaliDateText(DateTimeOffset moment)
    {
        var tehran = moment.ToOffset(TehranTime.Offset);

        try
        {
            var calendar = new PersianCalendar();
            var text = $"{calendar.GetYear(tehran.DateTime):0000}/{calendar.GetMonth(tehran.DateTime):00}/{calendar.GetDayOfMonth(tehran.DateTime):00}";
            return PersianText.ToPersianDigits(text);
        }
        catch (ArgumentOutOfRangeException)
        {
            // خارج از بازه پشتیبانی تقویم شمسی: نمایش میلادی بهتر از خطاست.
            return PersianText.ToPersianDigits(tehran.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>ساعت به وقت ایران مانند <c>۱۱:۱۴</c>.</summary>
    public static string ToTimeText(DateTimeOffset moment) =>
        PersianText.ToPersianDigits(moment.ToOffset(TehranTime.Offset).ToString("HH:mm", CultureInfo.InvariantCulture));

    /// <summary>«تاریخ — ساعت» به وقت ایران؛ خروجی نمونه: <c>۱۴۰۱/۰۳/۱۹ — ۱۱:۱۴</c>.</summary>
    public static string ToJalaliDateTimeText(DateTimeOffset moment) =>
        $"{ToJalaliDateText(moment)} — {ToTimeText(moment)}";
}
