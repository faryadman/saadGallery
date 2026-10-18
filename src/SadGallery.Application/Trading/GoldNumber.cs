using System.Globalization;
using SadGallery.Application.Text;
namespace SadGallery.Application.Trading;

public static class GoldNumber
{
    public static bool TryParse(string? input, out decimal number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(input) || input.Length > 40) return false;
        var text = PersianText.ToAsciiDigits(input.Trim()).Replace("٬", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).Replace("٫", ".", StringComparison.Ordinal);
        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
    }
    public static string Money(decimal value) => PersianText.ToPersianDigits(value.ToString("N0", CultureInfo.InvariantCulture));
    public static string Grams(decimal value) => PersianText.ToPersianDigits(value.ToString("0.###", CultureInfo.InvariantCulture));
    public static string Rate(decimal value) => PersianText.ToPersianDigits(value.ToString("#,0.####", CultureInfo.InvariantCulture));
}
