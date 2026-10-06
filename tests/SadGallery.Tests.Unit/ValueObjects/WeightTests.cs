using SadGallery.Domain.Constants;
using SadGallery.Domain.ValueObjects;
using Xunit;

namespace SadGallery.Tests.Unit.ValueObjects;

/// <summary>
/// تست‌های وزن و تبدیل مثقال. مبنای رسمی: ۱ مثقال = ۴.۶۰۸۳ گرم (ADR-0009).
/// </summary>
public sealed class WeightTests
{
    [Fact]
    public void OneMesghal_Equals_FourPointSixZeroEightThree_Grams()
    {
        var weight = Weight.FromMesghal(1m);

        Assert.Equal(4.6083m, weight.Grams);
    }

    [Fact]
    public void Mesghal_RoundTrip_PreservesValue()
    {
        var original = Weight.FromMesghal(2.5m);

        var back = original.ToMesghal();

        Assert.Equal(2.5m, back);
    }

    [Fact]
    public void Grams_RoundTrip_WithMesghalConstant_IsStable()
    {
        var weight = Weight.FromGrams(10m);

        var mesghal = weight.ToMesghal();
        var backToGrams = Weight.FromMesghal(mesghal).Grams;

        // تلورانس صریح برای عملیات اعشاری؛ گردکردن فقط در لبه نمایش انجام می‌شود.
        Assert.InRange(backToGrams, 10m - 0.0000001m, 10m + 0.0000001m);
    }

    [Fact]
    public void NegativeWeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Weight.FromGrams(-0.01m));
    }

    [Fact]
    public void Zero_IsAllowed_ForUnweighedItems()
    {
        Assert.Equal(0m, Weight.Zero.Grams);
    }

    [Fact]
    public void MeasurementConstant_IsDocumentedValue()
    {
        Assert.Equal(4.6083m, Measurements.MesghalInGrams);
        Assert.Equal(24m, Measurements.PureKarat);
    }
}
