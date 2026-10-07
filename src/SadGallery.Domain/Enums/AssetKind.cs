namespace SadGallery.Domain.Enums;

/// <summary>
/// نوع دارایی بازار. برای گروه‌بندی نمایش و انتخاب فرمول محاسبه (فاز ۳) استفاده می‌شود.
/// </summary>
public enum AssetKind
{
    /// <summary>طلا بر مبنای گرم (مثلاً گرم ۱۸ عیار).</summary>
    GoldGram = 1,

    /// <summary>طلا بر مبنای مثقال (مثلاً مظنه).</summary>
    GoldMesghal = 2,

    /// <summary>مسکوک (سکه تمام، نیم، ربع، گرمی).</summary>
    Coin = 3,

    /// <summary>ارز (قیمت یک واحد ارز بر حسب واحد پول ایران).</summary>
    Currency = 4,

    /// <summary>فلز گران‌بها بر مبنای انس جهانی (قیمت به دلار).</summary>
    MetalOunce = 5,
}
