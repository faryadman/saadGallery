using SadGallery.Domain.Enums;

namespace SadGallery.Domain.Market;

/// <summary>
/// فهرست دارایی‌های پشتیبانی‌شده و نگاشت آن‌ها به کلیدهای پاسخ منبع نرخ.
/// منبع این نگاشت: مستندات رسمی سرویس نرخ که مالک پروژه ارائه کرده است (docs/API.md §ب).
/// قواعد واحد (عیناً از همان مستندات):
///   • قیمت گرم/مثقال طلا: تومان، مقدار کامل.
///   • قیمت سکه‌ها: «با حذف سه صفر» اعلام می‌شود ⇒ ضرب در ۱۰۰۰ برای رسیدن به تومان.
///   • انس طلا: دلار.
///   • انس نقره: «در هزار ضرب شده است» ⇒ ضرب در ۰٫۰۰۱ برای رسیدن به دلار.
///   • دلار/یورو/درهم: تومان، مقدار کامل.
/// کلید <c>Pelatin</c> عمداً در فهرست نیست: واحد آن از مستندات قابل استنتاج قطعی نبود
/// و طبق قاعده پروژه، حدس زده نمی‌شود (OPEN_QUESTIONS — Q-API-2). مقدار آن در هر
/// فراخوانی به‌عنوان «کلید ناشناخته» ثبت می‌شود تا از قلم نیفتد.
/// </summary>
public static class AssetCatalog
{
    /// <summary>کلید زمانی پاسخ منبع (نه دارایی).</summary>
    public const string TimeProviderKey = "TimeRead";

    /// <summary>حداقل فاصله تحمل‌شده برای تشخیص «خطای واحد» (ریال به‌جای تومان) — ضریب ۱۰.</summary>
    public const decimal UnitMismatchFactor = 10m;

    private static readonly AssetDefinition[] Definitions =
    [
        new AssetDefinition
        {
            Code = "GOLD_GRAM_18",
            ProviderKey = "YekGram18",
            Title = "طلای ۱۸ عیار (هر گرم)",
            Kind = AssetKind.GoldGram,
            QuoteUnit = CurrencyUnit.Irt,
            MinPlausible = 100_000m,
            MaxPlausible = 500_000_000m,
        },
        new AssetDefinition
        {
            Code = "GOLD_MESGHAL_18",
            ProviderKey = "YekMesghal18",
            Title = "طلای ۱۸ عیار (هر مثقال)",
            Kind = AssetKind.GoldMesghal,
            QuoteUnit = CurrencyUnit.Irt,
            MinPlausible = 500_000m,
            MaxPlausible = 2_500_000_000m,
        },
        new AssetDefinition
        {
            Code = "GOLD_MESGHAL_17",
            ProviderKey = "YekMesghal17",
            Title = "مظنه (مثقال ۱۷ عیار)",
            Kind = AssetKind.GoldMesghal,
            QuoteUnit = CurrencyUnit.Irt,
            MinPlausible = 500_000m,
            MaxPlausible = 2_500_000_000m,
        },
        new AssetDefinition
        {
            Code = "GOLD_GRAM_20",
            ProviderKey = "YekGram20",
            Title = "طلای ۲۰ عیار (هر گرم)",
            Kind = AssetKind.GoldGram,
            QuoteUnit = CurrencyUnit.Irt,
            MinPlausible = 100_000m,
            MaxPlausible = 500_000_000m,
        },
        new AssetDefinition
        {
            Code = "GOLD_GRAM_21",
            ProviderKey = "YekGram21",
            Title = "طلای ۲۱ عیار (هر گرم)",
            Kind = AssetKind.GoldGram,
            QuoteUnit = CurrencyUnit.Irt,
            MinPlausible = 100_000m,
            MaxPlausible = 500_000_000m,
        },
        new AssetDefinition
        {
            Code = "COIN_EMAMI",
            ProviderKey = "SekehEmam",
            Title = "سکه تمام بهار آزادی (طرح جدید / امامی)",
            Kind = AssetKind.Coin,
            QuoteUnit = CurrencyUnit.Irt,
            ProviderScale = 1_000m,
            MinPlausible = 1_000_000m,
            MaxPlausible = 2_000_000_000m,
        },
        new AssetDefinition
        {
            Code = "COIN_FULL_OLD",
            ProviderKey = "SekehTamam",
            Title = "سکه تمام بهار آزادی (طرح قدیم)",
            Kind = AssetKind.Coin,
            QuoteUnit = CurrencyUnit.Irt,
            ProviderScale = 1_000m,
            MinPlausible = 1_000_000m,
            MaxPlausible = 2_000_000_000m,
        },
        new AssetDefinition
        {
            Code = "COIN_HALF",
            ProviderKey = "SekehNim",
            Title = "نیم سکه بهار آزادی",
            Kind = AssetKind.Coin,
            QuoteUnit = CurrencyUnit.Irt,
            ProviderScale = 1_000m,
            MinPlausible = 500_000m,
            MaxPlausible = 1_000_000_000m,
        },
        new AssetDefinition
        {
            Code = "COIN_QUARTER",
            ProviderKey = "SekehRob",
            Title = "ربع سکه بهار آزادی",
            Kind = AssetKind.Coin,
            QuoteUnit = CurrencyUnit.Irt,
            ProviderScale = 1_000m,
            MinPlausible = 200_000m,
            MaxPlausible = 500_000_000m,
        },
        new AssetDefinition
        {
            Code = "COIN_GERAMI",
            ProviderKey = "SekehGerami",
            Title = "سکه گرمی",
            Kind = AssetKind.Coin,
            QuoteUnit = CurrencyUnit.Irt,
            ProviderScale = 1_000m,
            MinPlausible = 100_000m,
            MaxPlausible = 500_000_000m,
        },
        new AssetDefinition
        {
            Code = "GOLD_OUNCE_USD",
            ProviderKey = "OunceTala",
            Title = "انس طلا (جهانی)",
            Kind = AssetKind.MetalOunce,
            QuoteUnit = CurrencyUnit.Usd,
            MinPlausible = 100m,
            MaxPlausible = 100_000m,
        },
        new AssetDefinition
        {
            Code = "SILVER_OUNCE_USD",
            ProviderKey = "OunceNoghreh",
            Title = "انس نقره (جهانی)",
            Kind = AssetKind.MetalOunce,
            QuoteUnit = CurrencyUnit.Usd,
            ProviderScale = 0.001m,
            MinPlausible = 1m,
            MaxPlausible = 10_000m,
        },
        new AssetDefinition
        {
            Code = "CURRENCY_USD",
            ProviderKey = "Dollar",
            Title = "دلار آمریکا",
            Kind = AssetKind.Currency,
            QuoteUnit = CurrencyUnit.Irt,
            MinPlausible = 1_000m,
            MaxPlausible = 100_000_000m,
        },
        new AssetDefinition
        {
            Code = "CURRENCY_EUR",
            ProviderKey = "Euro",
            Title = "یورو",
            Kind = AssetKind.Currency,
            QuoteUnit = CurrencyUnit.Irt,
            MinPlausible = 1_000m,
            MaxPlausible = 100_000_000m,
        },
        new AssetDefinition
        {
            Code = "CURRENCY_AED",
            ProviderKey = "Derham",
            Title = "درهم امارات",
            Kind = AssetKind.Currency,
            QuoteUnit = CurrencyUnit.Irt,
            MinPlausible = 1_000m,
            MaxPlausible = 100_000_000m,
        },
    ];

    private static readonly Dictionary<string, AssetDefinition> ByProviderKeyMap =
        Definitions.ToDictionary(d => d.ProviderKey, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, AssetDefinition> ByCodeMap =
        Definitions.ToDictionary(d => d.Code, StringComparer.Ordinal);

    /// <summary>همه دارایی‌ها به ترتیب نمایش.</summary>
    public static IReadOnlyList<AssetDefinition> All { get; } = Definitions;

    /// <summary>یافتن تعریف با کلید منبع نرخ (بی‌توجه به بزرگی/کوچکی حروف).</summary>
    public static AssetDefinition? FindByProviderKey(string providerKey) =>
        string.IsNullOrWhiteSpace(providerKey) ? null : ByProviderKeyMap.GetValueOrDefault(providerKey.Trim());

    /// <summary>یافتن تعریف با کد داخلی.</summary>
    public static AssetDefinition? FindByCode(string code) =>
        string.IsNullOrWhiteSpace(code) ? null : ByCodeMap.GetValueOrDefault(code.Trim());
}
