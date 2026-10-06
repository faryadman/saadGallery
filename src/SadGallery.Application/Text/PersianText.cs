using System.Text;

namespace SadGallery.Application.Text;

/// <summary>
/// کمک‌تابع‌های متن فارسی/اعداد. (لایه نمایش — منطق تجاری نیست)
/// </summary>
public static class PersianText
{
    private const string PersianDigits = "۰۱۲۳۴۵۶۷۸۹";
    private const string ArabicDigits = "٠١٢٣٤٥٦٧٨٩";

    /// <summary>
    /// تبدیل ارقام فارسی/عربی به ارقام لاتین.
    /// کاربرد: ورودی کاربر (شماره موبایل/کد یک‌بارمصرف) پیش از اعتبارسنجی و ذخیره.
    /// </summary>
    public static string ToAsciiDigits(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(input.Length);

        foreach (var character in input)
        {
            var persianIndex = PersianDigits.IndexOf(character, StringComparison.Ordinal);
            if (persianIndex >= 0)
            {
                builder.Append((char)('0' + persianIndex));
                continue;
            }

            var arabicIndex = ArabicDigits.IndexOf(character, StringComparison.Ordinal);
            if (arabicIndex >= 0)
            {
                builder.Append((char)('0' + arabicIndex));
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>نمایش عدد با ارقام فارسی (فقط برای نمایش؛ هرگز پیش از ذخیره استفاده نکنید).</summary>
    public static string ToPersianDigits(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(input.Length);

        foreach (var character in input)
        {
            if (character is >= '0' and <= '9')
            {
                builder.Append(PersianDigits[character - '0']);
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
