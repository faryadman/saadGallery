using SadGallery.Application.Market;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>تست‌های نگهبان جهش غیرعادی نرخ.</summary>
public sealed class RateAnomalyDetectorTests
{
    [Fact]
    public void WithoutPreviousValue_NeverFlags()
    {
        var detector = new RateAnomalyDetector(50m);

        Assert.False(detector.IsAnomalous(null, 10_000_000m));
        Assert.False(detector.IsAnomalous(0m, 10_000_000m));
    }

    [Theory]
    [InlineData(1_000_000, 1_100_000)]  // +۱۰٪
    [InlineData(1_000_000, 900_000)]    // −۱۰٪
    [InlineData(1_000_000, 1_490_000)]  // +۴۹٪
    public void ChangeBelowThreshold_IsNormal(decimal previous, decimal current)
    {
        var detector = new RateAnomalyDetector(50m);

        Assert.False(detector.IsAnomalous(previous, current));
    }

    [Theory]
    [InlineData(1_000_000, 1_500_000)]     // دقیقاً روی آستانه
    [InlineData(1_000_000, 1_500_001)]     // بالاتر از آستانه
    [InlineData(1_000_000, 500_000)]       // −۵۰٪
    [InlineData(1_000_000, 14_428_000)]    // خطای ×۱۰ (ریال/تومان)
    public void ChangeAtOrAboveThreshold_IsAnomalous(decimal previous, decimal current)
    {
        var detector = new RateAnomalyDetector(50m);

        Assert.True(detector.IsAnomalous(previous, current));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Constructor_OutOfRangeThreshold_Throws(int threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateAnomalyDetector(threshold));
    }
}
