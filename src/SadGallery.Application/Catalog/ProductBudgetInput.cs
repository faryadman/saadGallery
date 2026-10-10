using System.Globalization;
using SadGallery.Application.Text;

namespace SadGallery.Application.Catalog;

/// <summary>پارس و اعتبارسنجیِ سقف بودجهٔ مشتری با واحد تومان.</summary>
public static class ProductBudgetInput
{
    /// <summary>بیشینهٔ سازگار با ستون‌های مبلغِ decimal(18,2) در SQL Server.</summary>
    public const decimal MaximumToman = 9_999_999_999_999_999.99m;

    /// <summary>
    /// ارقام فارسی/عربی و جداکننده‌های هزارگان را می‌پذیرد. مقدار خروجی تومان است؛
    /// هیچ تبدیل ریال/تومان انجام نمی‌شود.
    /// </summary>
    public static bool TryParseToman(string? input, out decimal amountToman)
    {
        amountToman = 0m;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = PersianText.ToAsciiDigits(input.Trim())
            .Replace("٬", string.Empty, StringComparison.Ordinal)
            .Replace("،", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Replace("\u202F", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("٫", ".", StringComparison.Ordinal);

        if (!decimal.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out amountToman))
        {
            amountToman = 0m;
            return false;
        }

        if (amountToman <= 0m || amountToman > MaximumToman)
        {
            amountToman = 0m;
            return false;
        }

        return true;
    }
}
