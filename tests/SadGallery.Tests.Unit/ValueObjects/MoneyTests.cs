using SadGallery.Domain.Enums;
using SadGallery.Domain.ValueObjects;
using Xunit;

namespace SadGallery.Tests.Unit.ValueObjects;

/// <summary>
/// تست‌های نوع پول. بیشترین ریسک مالی پروژه، خطای «ریال/تومان» است؛
/// این تست‌ها همان مرز را قفل می‌کنند (ADR-0009).
/// </summary>
public sealed class MoneyTests
{
    [Fact]
    public void Add_SameUnit_AddsAmounts()
    {
        var first = new Money(1_500_000m, CurrencyUnit.Irt);
        var second = new Money(500_000m, CurrencyUnit.Irt);

        var result = first + second;

        Assert.Equal(2_000_000m, result.Amount);
        Assert.Equal(CurrencyUnit.Irt, result.Unit);
    }

    [Fact]
    public void Add_DifferentUnits_Throws()
    {
        var rial = new Money(1_000_000m, CurrencyUnit.Irr);
        var toman = new Money(100_000m, CurrencyUnit.Irt);

        var exception = Assert.Throws<InvalidOperationException>(() => rial + toman);

        Assert.Contains("units must match", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConvertTo_RialToToman_UsesExplicitRate()
    {
        var rial = new Money(10_000_000m, CurrencyUnit.Irr);

        var toman = rial.ConvertTo(CurrencyUnit.Irt, 0.1m);

        Assert.Equal(1_000_000m, toman.Amount);
        Assert.Equal(CurrencyUnit.Irt, toman.Unit);
    }

    [Fact]
    public void ConvertTo_TomanToRial_UsesExplicitRate()
    {
        var toman = new Money(1_000_000m, CurrencyUnit.Irt);

        var rial = toman.ConvertTo(CurrencyUnit.Irr, 10m);

        Assert.Equal(10_000_000m, rial.Amount);
        Assert.Equal(CurrencyUnit.Irr, rial.Unit);
    }

    [Fact]
    public void ConvertTo_SameUnit_ReturnsUnchanged()
    {
        var money = new Money(250_000m, CurrencyUnit.Irt);

        var result = money.ConvertTo(CurrencyUnit.Irt, 123m);

        Assert.Equal(money, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void ConvertTo_InvalidRate_Throws(double rate)
    {
        var money = new Money(1_000m, CurrencyUnit.Irt);

        Assert.Throws<ArgumentOutOfRangeException>(() => money.ConvertTo(CurrencyUnit.Irr, (decimal)rate));
    }

    [Fact]
    public void Subtract_CanProduceNegativeBubbleAmount()
    {
        // حباب منفی یک وضعیت واقعی بازار است و نباید صفر شود.
        var market = new Money(70_000_000m, CurrencyUnit.Irt);
        var intrinsic = new Money(72_500_000m, CurrencyUnit.Irt);

        var bubble = market - intrinsic;

        Assert.Equal(-2_500_000m, bubble.Amount);
    }

    [Fact]
    public void Comparison_DifferentUnits_Throws()
    {
        var rial = new Money(1m, CurrencyUnit.Irr);
        var toman = new Money(1m, CurrencyUnit.Irt);

        Assert.Throws<InvalidOperationException>(() => rial > toman);
    }

    [Fact]
    public void Constructor_UnsupportedUnit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(100m, (CurrencyUnit)999));
    }

    [Fact]
    public void Zero_HasZeroAmount_AndGivenUnit()
    {
        var zero = Money.Zero(CurrencyUnit.Usd);

        Assert.Equal(0m, zero.Amount);
        Assert.Equal(CurrencyUnit.Usd, zero.Unit);
    }
}
