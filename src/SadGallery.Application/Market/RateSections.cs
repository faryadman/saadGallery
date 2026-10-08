using SadGallery.Domain.Enums;

namespace SadGallery.Application.Market;

/// <summary>یک بخش نمایش نرخ در صفحه اصلی (گروه‌بندی بر اساس نوع دارایی).</summary>
public sealed record RateSection(
    string Key,
    string Title,
    string Hint,
    IReadOnlyList<RateDisplayItem> Items);

/// <summary>
/// گروه‌بندی نرخ‌ها برای صفحه اصلی: طلا و مظنه، مسکوکات، ارز، اونس جهانی و رمزارز.
/// ترتیب بخش‌ها ثابت است تا صفحه «بدون اسکرول/جست‌وجو» قابل اسکن باشد (معیار پذیرش فاز ۳).
/// رمزارز فعلاً در منبع نرخ وجود ندارد ⇒ بخش با پیام صریح و بدون عدد ساختگی نمایش می‌یابد.
/// </summary>
public static class RateSections
{
    /// <summary>کلید بخش رمزارز (شناسه تست/نمایش).</summary>
    public const string CryptoKey = "crypto";

    /// <summary>پیام صریح برای بخشی که منبع فعلی داده‌ای برایش ندارد.</summary>
    public const string CryptoNotice =
        "منبع نرخ فعلی، نرخی برای رمزارزها ارائه نمی‌کند؛ تا افزودن منبع، هیچ عددی نمایش داده نمی‌شود (در انتظار پاسخ سؤال Q-API-2 در OPEN_QUESTIONS).";

    /// <summary>ساخت بخش‌ها از ردیف‌های نمایش. بخش‌های خالی حذف می‌شوند؛ رمزارز همیشه به‌صورت بخش خالی برمی‌گردد.</summary>
    public static IReadOnlyList<RateSection> Build(IReadOnlyList<RateDisplayItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var gold = items.Where(item => item.Kind is AssetKind.GoldGram or AssetKind.GoldMesghal).ToArray();
        var coins = items.Where(item => item.Kind == AssetKind.Coin).ToArray();
        var currencies = items.Where(item => item.Kind == AssetKind.Currency).ToArray();
        var ounces = items.Where(item => item.Kind == AssetKind.MetalOunce).ToArray();

        var sections = new List<RateSection>(5);

        Add(sections, "gold", "طلا و مظنه", "نرخ طلای ۱۸ عیار گرم و مثقال و مظنه بازار", gold);
        Add(sections, "coins", "مسکوکات", "سکه‌های رسمی بانک مرکزی", coins);
        Add(sections, "currency", "ارز", "دلار، یورو و درهم (تومان)", currencies);
        Add(sections, "ounce", "اونس جهانی", "انس طلا (دلار) و انس نقره (دلار)", ounces);

        sections.Add(new RateSection(CryptoKey, "رمزارزها", CryptoNotice, []));

        return sections;

        static void Add(List<RateSection> target, string key, string title, string hint, IReadOnlyList<RateDisplayItem> sectionItems)
        {
            if (sectionItems.Count > 0)
            {
                target.Add(new RateSection(key, title, hint, sectionItems));
            }
        }
    }
}
