using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>نمودار SVG درون‌خطی (بدون کتابخانه خارجی) — بازرسی ساختار خروجی.</summary>
public sealed class MarketChartTests
{
    private static RateHistoryPoint Point(int day, decimal amount) => new(
        new DateTimeOffset(2026, 10, day, 12, 0, 0, TimeSpan.Zero),
        $"روز {day}",
        amount,
        amount.ToString(),
        "تومان",
        RateQuality.Live);

    [Fact]
    public void EmptySeries_YieldsEmptyString_NoFakeChart()
    {
        Assert.Equal(string.Empty, MarketChart.BuildSvg([], "آزمون"));
    }

    [Fact]
    public void Series_ProducesSvgWithAccessibleLabel_AndPolyline()
    {
        var svg = MarketChart.BuildSvg([Point(1, 100m), Point(2, 150m), Point(3, 120m)], "نمودار آزمون");

        Assert.StartsWith("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("role=\"img\"", svg, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"نمودار آزمون\"", svg, StringComparison.Ordinal);
        Assert.Contains("<polyline", svg, StringComparison.Ordinal);
        Assert.Contains("points=\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void SinglePoint_IsCentered_AndDoesNotCrash()
    {
        var svg = MarketChart.BuildSvg([Point(1, 100m)], "تک‌نقطه");

        Assert.Contains("50,20", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void FlatSeries_UsesMiddleLine()
    {
        var svg = MarketChart.BuildSvg([Point(1, 100m), Point(2, 100m)], "یکنواخت");

        // بدون بازه (range=0) ⇒ همه نقاط روی خط وسط (y = 20)
        Assert.Contains("0,20", svg, StringComparison.Ordinal);
        Assert.Contains("100,20", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void RangeDescription_UsesPersianFormattingAndUnit()
    {
        var text = MarketChart.DescribeRange(1_000m, 2_000m, CurrencyUnit.Irt);

        Assert.Contains("۱٬۰۰۰ تومان", text, StringComparison.Ordinal);
        Assert.Contains("۲٬۰۰۰ تومان", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CountDescription_UsesPersianDigits()
    {
        Assert.Equal("۱۲ نقطه", MarketChart.DescribeCount(12));
    }

    [Fact]
    public void AriaLabel_IsHtmlEscaped()
    {
        var svg = MarketChart.BuildSvg([Point(1, 1m)], "نمودار <script>");

        Assert.DoesNotContain("<script>", svg, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", svg, StringComparison.Ordinal);
    }
}
