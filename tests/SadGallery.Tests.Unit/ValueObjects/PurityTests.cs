using SadGallery.Domain.Constants;
using SadGallery.Domain.ValueObjects;
using Xunit;

namespace SadGallery.Tests.Unit.ValueObjects;

/// <summary>
/// تست‌های عیار. قاعده پروژه: طلای خالص هرگز با طلای ۱۸ عیار اشتباه گرفته نمی‌شود.
/// </summary>
public sealed class PurityTests
{
    [Fact]
    public void EighteenKarat_Fineness_Is_ThreeQuarters()
    {
        Assert.Equal(0.750m, Purity.Eighteen.Fineness);
        Assert.Equal(Measurements.Fineness18Karat, Purity.Eighteen.Fineness);
    }

    [Fact]
    public void PureKarat_Fineness_Is_One()
    {
        Assert.Equal(1m, Purity.TwentyFour.Fineness);
    }

    [Theory]
    [InlineData(0.900, 21.6)]
    [InlineData(0.750, 18)]
    public void FromFineness_ConvertsToKarat(double fineness, double expectedKarat)
    {
        var purity = Purity.FromFineness((decimal)fineness);

        Assert.Equal((decimal)expectedKarat, purity.Karat);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(24.001)]
    public void InvalidKarat_Throws(double karat)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Purity.FromKarat((decimal)karat));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.2)]
    public void InvalidFineness_Throws(double fineness)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Purity.FromFineness((decimal)fineness));
    }

    [Fact]
    public void Eighteen_IsNotEqualTo_Pure()
    {
        // قفل‌کردن یک خطای رایج: ۱۸ عیار ≠ طلای خالص
        Assert.NotEqual(Purity.TwentyFour.Fineness, Purity.Eighteen.Fineness);
    }
}
