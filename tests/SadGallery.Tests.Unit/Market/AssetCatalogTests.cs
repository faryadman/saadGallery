using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Market;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>
/// تست‌های فهرست دارایی‌ها: یکتایی، نگاشت کلیدهای واقعی منبع و قفل قواعد واحد.
/// </summary>
public sealed class AssetCatalogTests
{
    [Fact]
    public void Codes_AndProviderKeys_AreUnique()
    {
        Assert.Equal(AssetCatalog.All.Count, AssetCatalog.All.Select(item => item.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(AssetCatalog.All.Count, AssetCatalog.All.Select(item => item.ProviderKey).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void CurrentProviderKeys_AreAllMapped_FromOwnerDocumentation()
    {
        // فهرست کلیدهای واقعی پاسخ سرویس (به‌جز Pelatin که واحدش ناشناخته است)
        string[] providerKeys =
        [
            "YekGram18", "YekMesghal18", "SekehRob", "SekehNim", "SekehEmam", "SekehTamam",
            "SekehGerami", "OunceTala", "YekMesghal17", "OunceNoghreh", "Dollar", "Euro",
            "Derham", "YekGram20", "YekGram21",
        ];

        foreach (var key in providerKeys)
        {
            Assert.NotNull(AssetCatalog.FindByProviderKey(key));
        }

        Assert.Equal(providerKeys.Length, AssetCatalog.All.Count);
    }

    [Fact]
    public void UnknownOrUnconfirmedKeys_AreNotMapped()
    {
        // Pelatin عمداً نگاشت نشده (واحدش از مستندات قطعی نبود) و TimeRead دارایی نیست.
        Assert.Null(AssetCatalog.FindByProviderKey("Pelatin"));
        Assert.Null(AssetCatalog.FindByProviderKey(AssetCatalog.TimeProviderKey));
        Assert.Null(AssetCatalog.FindByProviderKey("NotARealKey"));
    }

    [Fact]
    public void Lookup_IsCaseInsensitive_AndTrims()
    {
        Assert.NotNull(AssetCatalog.FindByProviderKey("yekgram18"));
        Assert.NotNull(AssetCatalog.FindByProviderKey("  SekehEmam  "));
        Assert.NotNull(AssetCatalog.FindByCode("GOLD_GRAM_18"));
        Assert.Null(AssetCatalog.FindByCode("gold_gram_18")); // کد داخلی حساس به بزرگی/کوچکی است
    }

    [Fact]
    public void Coins_AreScaledByThousand_AccordingToProviderDocumentation()
    {
        var coins = AssetCatalog.All.Where(item => item.Kind == AssetKind.Coin).ToArray();

        Assert.NotEmpty(coins);
        Assert.All(coins, coin =>
        {
            Assert.Equal(1_000m, coin.ProviderScale);
            Assert.Equal(CurrencyUnit.Irt, coin.QuoteUnit);
        });
    }

    [Fact]
    public void SilverOunce_IsScaledByOneThousandth_AndQuotedInUsd()
    {
        var silver = AssetCatalog.FindByCode("SILVER_OUNCE_USD")!;

        Assert.Equal(0.001m, silver.ProviderScale);
        Assert.Equal(CurrencyUnit.Usd, silver.QuoteUnit);
    }

    [Fact]
    public void GoldOunce_IsQuotedInUsd_WithoutScaling()
    {
        var ounce = AssetCatalog.FindByCode("GOLD_OUNCE_USD")!;

        Assert.Equal(1m, ounce.ProviderScale);
        Assert.Equal(CurrencyUnit.Usd, ounce.QuoteUnit);
    }

    [Fact]
    public void PlausibleRanges_AreSane()
    {
        Assert.All(AssetCatalog.All, item =>
        {
            Assert.True(item.MinPlausible > 0m, $"{item.Code}: کف بازه باید مثبت باشد.");
            Assert.True(item.MaxPlausible > item.MinPlausible, $"{item.Code}: سقف بازه باید بزرگ‌تر از کف باشد.");
        });
    }

    [Fact]
    public void OwnerSampleValues_AllFallInsidePlausibleRanges()
    {
        // تضمین اینکه نگهبان‌ها داده واقعی ارائه‌شده مالک را رد نمی‌کنند (جنبه مثبت).
        var parse = TgnResponseParser.Parse(
            "{\"YekGram18\":1442800,\"YekMesghal18\":6648900,\"SekehRob\":5520,\"SekehNim\":8520," +
            "\"SekehEmam\":14500,\"SekehTamam\":14940,\"SekehGerami\":3050,\"OunceTala\":1850," +
            "\"YekMesghal17\":6250000,\"OunceNoghreh\":21940,\"Dollar\":30000," +
            "\"Euro\":33000,\"Derham\":8000,\"YekGram20\":1602500,\"YekGram21\":1683300," +
            "\"TimeRead\":\"2022/06/09 11:14:48\"}");

        foreach (var rate in parse.Rates)
        {
            var definition = AssetCatalog.FindByCode(rate.AssetCode)!;
            var amount = rate.RawValue * definition.ProviderScale;

            Assert.True(
                amount >= definition.MinPlausible && amount <= definition.MaxPlausible,
                $"{rate.AssetCode}: مقدار {amount} بیرون از بازه [{definition.MinPlausible}..{definition.MaxPlausible}] افتاد.");
        }
    }

    [Fact]
    public void UnitMismatchFactor_IsTen()
    {
        Assert.Equal(10m, AssetCatalog.UnitMismatchFactor);
    }
}
