using SadGallery.Domain.Constants;

namespace SadGallery.Domain.Catalog;

/// <summary>
/// ثابت‌های فرمول قیمت‌گذاری محصول (فاز ۴).
/// </summary>
/// <remarks>
/// <para>
/// فرمول بر اساس عرف و مقررات رایج بازار طلای ایران (سال ۱۴۰۵) پیاده شده است:
/// </para>
/// <list type="bullet">
///   <item>ارزش طلای خام = وزن × نرخ هر گرم طلای ۱۸ عیار (با تعدیل عیار).</item>
///   <item>اجرت ساخت = درصدی از ارزش طلای خام.</item>
///   <item>سود فروشنده = درصدی از (ارزش طلا + اجرت)؛ سقف متداول ۷٪.</item>
///   <item>مالیات بر ارزش افزوده = درصدی از (اجرت + سود)؛ نرخ ۱۰٪ در سال ۱۴۰۵.</item>
/// </list>
/// <para>
/// نکتهٔ مهم و تأییدشده: مالیات بر ارزش افزوده به «اصل طلا» تعلق نمی‌گیرد و فقط
/// اجرت و سود را شامل می‌شود (اصل طلا از این مالیات معاف است). این قاعده در منابع
/// متعدد بازار به‌طور یکسان گزارش شده است؛ به همین دلیل مبنای مالیات در اینجا
/// «اجرت + سود» است، نه کل مبلغ.
/// </para>
/// <para>
/// هشدار: تغییر هر جزء این فرمول باید همراه با افزایش <see cref="Version"/> باشد،
/// چون قیمت‌های ذخیره‌شدهٔ قبل با مبنای متفاوتی محاسبه شده‌اند (پاسخ ADR-0009).
/// </para>
/// </remarks>
public static class ProductPriceFormula
{
    /// <summary>نسخه فرمول. با هر تغییر در فرمول یا ثابت‌ها افزایش می‌یابد.</summary>
    public const string Version = "price-v1";

    /// <summary>
    /// عیار مرجع نرخ روز بازار. نرخ اعلامی بازار برای «هر گرم طلای ۱۸ عیار» است،
    /// بنابراین ارزش سایر عیارها نسبت به ۱۸ محاسبه می‌شود (مانند ماشین‌حساب فاز ۳).
    /// </summary>
    public const decimal ReferenceKarat = 18m;

    /// <summary>سقف متداول سود فروشنده در بازار مصنوعات طلا (درصد).</summary>
    public const decimal DefaultProfitPercent = 7m;

    /// <summary>نرخ مالیات بر ارزش افزوده در سال ۱۴۰۵ (درصد) — فقط بر اجرت و سود.</summary>
    public const decimal DefaultTaxPercent = 10m;

    /// <summary>اجرت پیش‌فرض (درصد). اپراتور باید برای هر کالا مقدار واقعی را وارد کند.</summary>
    public const decimal DefaultMakingChargePercent = 0m;
}

/// <summary>
/// ورودی‌های صریح محاسبه قیمت. هیچ مقداری از «پیش‌فرض پنهان» یا نرخ لحظه‌ای
/// داخل این رکورد خوانده نمی‌شود (قاعده فاز ۳ برای محاسبه‌گرها، اینجا هم رعایت شده).
/// </summary>
public sealed record PriceInputs(
    decimal WeightGrams,
    decimal Karat,
    decimal Rate18KaratIrt,
    decimal MakingChargePercent,
    decimal ProfitPercent,
    decimal TaxPercent);

/// <summary>
/// اجزای محاسبه‌شدهٔ قیمت. همه مبالغ به <b>تومان</b> (Irt) هستند.
/// </summary>
/// <remarks>
/// مجموعِ اجزا دقیقاً برابر <see cref="TotalIrt"/> است، چون هر جزء پیش از جمع
/// به‌صورت مستقل گرد می‌شود (توضیح در <see cref="ProductPriceMath"/>).
/// </remarks>
public sealed record PriceBreakdown(
    decimal GoldValueIrt,
    decimal MakingChargeIrt,
    decimal ProfitIrt,
    decimal TaxIrt,
    decimal TotalIrt);

/// <summary>
/// نتیجه محاسبه قیمت؛ نامعتبر بودن با پیام فارسی همراه است (بدون استثنا برای ورودی کاربر).
/// </summary>
public sealed record PriceResult
{
    public bool IsValid { get; init; }

    /// <summary>دلیل نامعتبر بودن، به فارسی و قابل نمایش به کاربر.</summary>
    public string? Reason { get; init; }

    public PriceBreakdown? Breakdown { get; init; }

    public static PriceResult Fail(string reason) => new() { IsValid = false, Reason = reason };

    public static PriceResult Ok(PriceBreakdown breakdown) => new() { IsValid = true, Breakdown = breakdown };
}

/// <summary>
/// ریاضیات خالص قیمت‌گذاری. هیچ وابستگی به دیتابیس، نرخ لحظه‌ای یا وب ندارد.
/// </summary>
/// <remarks>
/// <para>قاعده دقت (آموخته‌شده از باگ فاز ۳): همه عملیات‌ها «اول ضرب، آخر تقسیم» انجام
/// می‌شوند تا تقسیم‌های تکرارشونده (مانند ۲۴÷۱۸) دقت <c>decimal</c> را از بین نبرند.</para>
/// <para>گرد کردن: هر جزء به «تومانِ کامل» گرد می‌شود (دور از صفر) و سپس جمع می‌شود؛
/// با این کار فاکتور چاپی مجموعاً همان عددی را نشان می‌دهد که اجزایش نشان می‌دهند.</para>
/// </remarks>
public static class ProductPriceMath
{
    /// <summary>
    /// ارزش طلای خامِ قطعه: وزن × تعدیل عیار × نرخ هر گرم طلای ۱۸ عیار.
    /// </summary>
    public static decimal GoldValueIrt(decimal weightGrams, decimal karat, decimal rate18KaratIrt)
        => Round(weightGrams * karat * rate18KaratIrt / ProductPriceFormula.ReferenceKarat);

    /// <summary>اجرت ساخت: درصدی از ارزش طلای خام.</summary>
    public static decimal MakingChargeIrt(decimal goldValueIrt, decimal makingChargePercent)
        => Round(goldValueIrt * makingChargePercent / 100m);

    /// <summary>سود فروشنده: درصدی از مجموع ارزش طلا و اجرت.</summary>
    public static decimal ProfitIrt(decimal goldValueIrt, decimal makingChargeIrt, decimal profitPercent)
        => Round((goldValueIrt + makingChargeIrt) * profitPercent / 100m);

    /// <summary>
    /// مالیات بر ارزش افزوده: درصدی از مجموع اجرت و سود.
    /// اصل طلا مشمول این مالیات نیست (توضیح در <see cref="ProductPriceFormula"/>).
    /// </summary>
    public static decimal TaxIrt(decimal makingChargeIrt, decimal profitIrt, decimal taxPercent)
        => Round((makingChargeIrt + profitIrt) * taxPercent / 100m);

    /// <summary>
    /// محاسبه کامل قیمت. ورودی‌های نامعتبر «نامعتبر» برمی‌گردانند (بدون استثنا).
    /// </summary>
    public static PriceResult Compute(PriceInputs input)
    {
        if (input.WeightGrams <= 0m)
        {
            return PriceResult.Fail("وزن کالا باید بزرگ‌تر از صفر باشد.");
        }

        if (input.Karat <= 0m || input.Karat > Measurements.PureKarat)
        {
            return PriceResult.Fail($"عیار باید بزرگ‌تر از صفر و حداکثر {Measurements.PureKarat} باشد.");
        }

        if (input.Rate18KaratIrt <= 0m)
        {
            return PriceResult.Fail("نرخ هر گرم طلای ۱۸ عیار در دسترس نیست؛ قیمت قابل محاسبه نیست.");
        }

        if (input.MakingChargePercent < 0m || input.ProfitPercent < 0m || input.TaxPercent < 0m)
        {
            return PriceResult.Fail("درصدهای اجرت، سود و مالیات نمی‌توانند منفی باشند.");
        }

        if (input.MakingChargePercent > 100m || input.ProfitPercent > 100m || input.TaxPercent > 100m)
        {
            return PriceResult.Fail("درصدهای اجرت، سود و مالیات نمی‌توانند بیش از ۱۰۰ باشند.");
        }

        var goldValue = GoldValueIrt(input.WeightGrams, input.Karat, input.Rate18KaratIrt);
        var making = MakingChargeIrt(goldValue, input.MakingChargePercent);
        var profit = ProfitIrt(goldValue, making, input.ProfitPercent);
        var tax = TaxIrt(making, profit, input.TaxPercent);

        return PriceResult.Ok(new PriceBreakdown(
            GoldValueIrt: goldValue,
            MakingChargeIrt: making,
            ProfitIrt: profit,
            TaxIrt: tax,
            TotalIrt: goldValue + making + profit + tax));
    }

    /// <summary>گرد کردن مبلغ به تومان کامل (دور از صفر).</summary>
    private static decimal Round(decimal amount) => Math.Round(amount, 0, MidpointRounding.AwayFromZero);
}
