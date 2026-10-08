using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>گروه‌بندی بخش‌های صفحه اصلی (معیار فاز ۳: طلا/مظنه، مسکوکات، ارز، رمزارز با پیام صریح).</summary>
public sealed class RateSectionsTests
{
    private static RateDisplayItem Item(string code, AssetKind kind, string title) => new(
        code,
        title,
        kind,
        1_000m,
        CurrencyUnit.Irt,
        "۱٬۰۰۰",
        "تومان",
        RateQuality.Live,
        "لحظه‌ای",
        DateTimeOffset.UtcNow,
        "۱۴۰۵/۰۷/۱۶");

    [Fact]
    public void Sections_AreOrdered_GoldThenCoinsThenCurrencyThenOunceThenCrypto()
    {
        var sections = RateSections.Build(
        [
            Item("CURRENCY_USD", AssetKind.Currency, "دلار"),
            Item("COIN_EMAMI", AssetKind.Coin, "سکه امامی"),
            Item("GOLD_GRAM_18", AssetKind.GoldGram, "طلای ۱۸"),
            Item("GOLD_OUNCE_USD", AssetKind.MetalOunce, "انس طلا"),
            Item("GOLD_MESGHAL_17", AssetKind.GoldMesghal, "مظنه"),
        ]);

        Assert.Equal(["gold", "coins", "currency", "ounce", RateSections.CryptoKey],
            sections.Select(group => group.Key));

        Assert.Equal(2, sections[0].Items.Count); // گرم + مظنه
        Assert.Single(sections[1].Items);
        Assert.Single(sections[2].Items);
        Assert.Single(sections[3].Items);
    }

    [Fact]
    public void CryptoSection_IsAlwaysPresentWithExplicitNotice_AndNoItems()
    {
        var sections = RateSections.Build([Item("GOLD_GRAM_18", AssetKind.GoldGram, "طلای ۱۸")]);
        var crypto = sections.Single(group => group.Key == RateSections.CryptoKey);

        Assert.Empty(crypto.Items);
        Assert.Contains("رمزارز", crypto.Hint, StringComparison.Ordinal);
        Assert.Contains("Q-API-2", crypto.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyInput_YieldsOnlyCryptoSection()
    {
        var sections = RateSections.Build([]);

        Assert.Single(sections);
        Assert.Equal(RateSections.CryptoKey, sections[0].Key);
    }
}
