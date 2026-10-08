using System.Globalization;
using System.Text;
using SadGallery.Application.Text;
using SadGallery.Domain.Enums;

namespace SadGallery.Application.Market;

/// <summary>
/// سازنده نمودار خطی سبک (SVG درون‌خطی) از نقاط تاریخچه.
/// چرا SVG دست‌ساز؟ هیچ کتابخانه/منبع خارجی در پیش‌نمایش و محیط عملیاتی بارگذاری نمی‌شود
/// (تصمیم ADR-0013)؛ همچنین خروجی به‌سادگی در تست‌ها قابل بازرسی است.
/// </summary>
public static class MarketChart
{
    /// <summary>ساخت نمودار از نقاط (قدیمی‌ترین سمت چپ). بدون نقطه ⇒ رشته خالی.</summary>
    public static string BuildSvg(IReadOnlyList<RateHistoryPoint> points, string ariaLabel)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentException.ThrowIfNullOrWhiteSpace(ariaLabel);

        if (points.Count == 0)
        {
            return string.Empty;
        }

        var series = points.OrderBy(point => point.QuotedAtUtc).ToArray();
        var min = series.Min(point => point.Amount);
        var max = series.Max(point => point.Amount);
        var range = max - min;

        const int width = 100;
        const int height = 40;

        var builder = new StringBuilder();
        builder.Append("<svg class=\"sg-chart\" viewBox=\"0 0 ").Append(width).Append(' ').Append(height)
               .Append("\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"").Append(Escape(ariaLabel)).Append("\">");

        var polyline = new StringBuilder();

        for (var index = 0; index < series.Length; index++)
        {
            var x = series.Length == 1 ? width / 2m : width * index / (series.Length - 1m);
            var ratio = range == 0m ? 0.5m : (series[index].Amount - min) / range;
            var y = height - (ratio * height * 0.8m) - (height * 0.1m); // حاشیه ۱۰٪ بالا/پایین

            if (polyline.Length > 0)
            {
                polyline.Append(' ');
            }

            polyline.Append(Format(x)).Append(',').Append(Format(y));
        }

        builder.Append("<polyline points=\"").Append(polyline).Append("\" />");
        builder.Append("</svg>");

        return builder.ToString();
    }

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    /// <summary>گزارش متنی کمینه/بیشینه برای پانوشت نمودار.</summary>
    public static string DescribeRange(decimal min, decimal max, CurrencyUnit unit) =>
        $"کمینه {RateFormat.Money(min, unit)} — بیشینه {RateFormat.Money(max, unit)}";

    /// <summary>تعداد نقاط به‌صورت فارسی (برای پانوشت).</summary>
    public static string DescribeCount(int count) =>
        PersianText.ToPersianDigits(count.ToString(CultureInfo.InvariantCulture)) + " نقطه";
}
