using System.Globalization;
using System.Text;

namespace SadGallery.Application.Text;

/// <summary>
/// خواندن عدد از ورودی فارسی‌زبان (فرم‌ها).
/// قاعده‌ها (مصوب پروژه — همان قواعد پارسر پاسخ منبع: ADR-0012 §۶):
///   • ارقام فارسی/عربی ⇒ لاتین
///   • جداکننده هزارگان: ٬ یا , یا فاصلهٔ نازک ⇒ حذف
///   • جداکننده اعشار: ٫ (و . فقط وقتی تنها جداکننده باشد) ⇒ '.'
///   • «7.600.000» (چند نقطه) رد می‌شود — مبهم است (هزارگان یا اعشار؟)
///   • ورودی منفی پذیرفته می‌شود (برای نمایش حباب منفی در فرم‌ها)، اما اعتبارسنجی دامنه جای دیگر است.
/// </summary>
public static class PersianNumber
{
    /// <summary>تلاش برای خواندن عدد؛ شکست ⇒ false و مقدار صفر.</summary>
    public static bool TryParse(string? input, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = PersianText.ToAsciiDigits(input);

        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            switch (character)
            {
                case '\u066C' or ',' or '\u060C' or ' ' or '\u200C' or '_':
                    // جداکننده هزارگان/فاصله‌ها حذف می‌شوند.
                    continue;
                case '\u066B' or '\u002E':
                    builder.Append('.');
                    continue;
                case '-':
                    builder.Append('-');
                    continue;
                default:
                    if (character is >= '0' and <= '9')
                    {
                        builder.Append(character);
                        continue;
                    }

                    if (character == '+')
                    {
                        continue;
                    }

                    return false;
            }
        }

        var text = builder.ToString();

        if (text.Length == 0 || text == "-" || text == ".")
        {
            return false;
        }

        // بیش از یک جداکننده اعشار = مبهم (مثال «7.600.000») ⇒ رد، نه حدس.
        if (text.Count(c => c == '.') > 1)
        {
            return false;
        }

        return decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>خواندن عدد با پیام خطای فارسی.</summary>
    public static bool TryParse(string? input, out decimal value, out string? error)
    {
        if (TryParse(input, out value))
        {
            error = null;
            return true;
        }

        error = "عدد واردشده خوانا نیست؛ لطفاً فقط ارقام (۰-۹) و جداکننده اعشار ٫ را وارد کنید.";
        return false;
    }
}
