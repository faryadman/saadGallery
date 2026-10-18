using SadGallery.Application.Trading;
using SadGallery.Domain.Trading;
using Xunit;
namespace SadGallery.Tests.Unit.Trading;

public sealed class GoldPurchaseMathTests
{
    [Fact] public void FeeIsAddedAndEachMoneyComponentRoundsToWholeToman()
    {
        Assert.Equal(new GoldCost(1_000_000m, 15_000m, 1_015_000m), GoldPurchaseMath.Calculate(.5m, 2_000_000m, 1.5m));
        Assert.Equal(new GoldCost(101m, 2m, 103m), GoldPurchaseMath.Calculate(.001m, 100_500m, 1.5m));
        Assert.Equal(new GoldCost(101m, 0m, 101m), GoldPurchaseMath.Calculate(.001m, 100_500m, 0));
    }
    [Theory]
    [InlineData(0, 2000000, 1)] [InlineData(-1, 2000000, 1)] [InlineData(.0001, 2000000, 1)] [InlineData(1000000, 2000000, 1)]
    [InlineData(1, 0, 1)] [InlineData(1, -1, 1)] [InlineData(1, 2000000, -1)] [InlineData(1, 2000000, 101)] [InlineData(1, 2000000, .0001)]
    public void RejectsInvalidInputs(decimal grams, decimal rate, decimal fee) => Assert.Null(GoldPurchaseMath.Calculate(grams, rate, fee));
    [Fact] public void StorageBoundsCannotOverflowOrRoundIntoFreeGold()
    {
        Assert.Null(GoldPurchaseMath.Calculate(GoldPurchaseMath.MaxGrams, GoldPurchaseMath.MaxMoney, 100));
        Assert.Null(GoldPurchaseMath.Calculate(.001m, .0001m, 0));
        Assert.False(GoldPurchaseMath.ValidMoney(.5m));
        Assert.False(GoldPurchaseMath.ValidGrams(GoldPurchaseMath.MaxGrams + .001m));
    }
    [Theory] [InlineData("۱٬۲۳۴٫۵۰۰", 1234.5)] [InlineData("٠٫٥", .5)] [InlineData("0", 0)]
    public void ParsesPersianAndArabicNumbers(string text, decimal expected) { Assert.True(GoldNumber.TryParse(text, out var actual)); Assert.Equal(expected, actual); }
    [Theory] [InlineData("1e3")] [InlineData("-1")] [InlineData("NaN")] [InlineData("۱۲٫۳٫۴")] [InlineData("")]
    public void RejectsInvalidNumbers(string text) => Assert.False(GoldNumber.TryParse(text, out _));
}
