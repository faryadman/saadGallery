using SadGallery.Domain.Market;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>
/// قفل‌کردن مشخصات استاندارد مسکوکات (وزن/عیار) — تغییر هر مقدار باید آگاهانه و همراه با مستندسازی باشد.
/// مقادیر بر پایه استاندارد رسمی بانک مرکزی (منابع در XML مستند CoinStandards و docs/API.md).
/// </summary>
public sealed class CoinStandardsTests
{
    [Theory]
    [InlineData(CoinStandards.EmamiCode, 8.133)]
    [InlineData(CoinStandards.FullOldCode, 8.133)]
    [InlineData(CoinStandards.HalfCode, 4.0665)]
    [InlineData(CoinStandards.QuarterCode, 2.03325)]
    [InlineData(CoinStandards.GeramiCode, 1.01)]
    public void Weights_MatchOfficialStandard(string code, double expectedWeight)
    {
        var standard = CoinStandards.TryGet(code);

        Assert.NotNull(standard);
        Assert.Equal((decimal)expectedWeight, standard!.WeightGrams);
        Assert.Equal(CoinStandards.OfficialPurity, standard.Purity);
    }

    [Fact]
    public void HalfAndQuarter_AreExactHalves()
    {
        Assert.Equal(CoinStandards.TryGet(CoinStandards.EmamiCode)!.WeightGrams / 2m,
            CoinStandards.TryGet(CoinStandards.HalfCode)!.WeightGrams);

        Assert.Equal(CoinStandards.TryGet(CoinStandards.HalfCode)!.WeightGrams / 2m,
            CoinStandards.TryGet(CoinStandards.QuarterCode)!.WeightGrams);
    }

    [Fact]
    public void AllCoins_HaveSameOfficialPurity_AndPositiveWeight()
    {
        Assert.NotEmpty(CoinStandards.Known);

        foreach (var coin in CoinStandards.Known)
        {
            Assert.Equal(0.900m, coin.Purity);
            Assert.True(coin.WeightGrams > 0m);
            Assert.False(string.IsNullOrWhiteSpace(coin.Title));
        }
    }

    [Fact]
    public void UnknownCode_ReturnsNull_WithoutGuessing()
    {
        Assert.Null(CoinStandards.TryGet("COIN_UNKNOWN"));
        Assert.Null(CoinStandards.TryGet("Pelatin"));
    }

    [Fact]
    public void EveryCoinStandard_HasMatchingCatalogEntry()
    {
        foreach (var coin in CoinStandards.Known)
        {
            var definition = AssetCatalog.FindByCode(coin.AssetCode);

            Assert.NotNull(definition);
            Assert.Equal(Domain.Enums.AssetKind.Coin, definition!.Kind);
        }
    }
}
