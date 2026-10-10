using System.ComponentModel.DataAnnotations;
using SadGallery.Domain.Enums;

namespace SadGallery.Web.Models.Catalog;

/// <summary>گزینهٔ یک دسته‌بندی در فهرستِ کشویی.</summary>
public sealed record CategoryOption(int Id, string Name);

/// <summary>
/// فرم ایجاد/ویرایش کالا.
/// </summary>
/// <remarks>
/// فیلدهای عددی <b>رشته</b> هستند تا کاربر بتواند با ارقام فارسی و جداکننده وارد کند
/// (مانند «۸٫۱۳۳» یا «۱٬۴۰۰٬۰۰۰»)؛ تبدیل با <c>PersianNumber</c> انجام می‌شود و
/// اعتبارسنجیِ نهایی همیشه در لایه Application است (قاعدهٔ یکسان با فرم‌های فاز ۳).
/// </remarks>
public sealed class ProductFormModel
{
    public int Id { get; set; }

    [Display(Name = "عنوان کالا")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "خلاصه (کارت محصول)")]
    public string? Summary { get; set; }

    [Display(Name = "توضیحات کامل")]
    public string? Description { get; set; }

    [Display(Name = "دسته‌بندی")]
    public int? CategoryId { get; set; }

    [Display(Name = "سیاست قیمت")]
    public PricePolicy PricePolicy { get; set; } = PricePolicy.Computed;

    /// <summary>قیمت ثابت به تومان (فقط برای سیاست Fixed).</summary>
    [Display(Name = "قیمت ثابت (تومان)")]
    public string? FixedPriceIrt { get; set; }

    /// <summary>وزن به گرم.</summary>
    [Display(Name = "وزن (گرم)")]
    public string? WeightGrams { get; set; }

    /// <summary>عیار (۰ تا ۲۴).</summary>
    [Display(Name = "عیار (۰ تا ۲۴)")]
    public string? Karat { get; set; }

    [Display(Name = "اجرت ساخت (درصد از ارزش طلا)")]
    public string? MakingChargePercent { get; set; }

    [Display(Name = "سود فروشنده (درصد)")]
    public string? ProfitPercent { get; set; }

    [Display(Name = "مالیات بر ارزش افزوده (درصد)")]
    public string? TaxPercent { get; set; }

    [Display(Name = "موجود است")]
    public bool IsInStock { get; set; } = true;

    [Display(Name = "منتشر شده در ویترین")]
    public bool IsPublished { get; set; }

    // ---- داده‌های کمکیِ فرم ----

    public IReadOnlyList<CategoryOption> Categories { get; set; } = [];

    /// <summary>خطاهای اعتبارسنجی (از لایه Application).</summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>پیام موفقیت.</summary>
    public string? Notice { get; set; }
}
