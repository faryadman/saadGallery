using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Catalog;
using SadGallery.Application.Market;

namespace SadGallery.Web.Controllers;

/// <summary>
/// صفحه اصلی (عمومی، بدون نیاز به ورود).
/// نرخ‌های بازار از کش درون‌فرایندی خوانده می‌شوند (بدون کوئری دیتابیس در مسیر گرم)؛
/// اگر هیچ نرخ معتبری موجود نباشد، به‌جای عدد ساختگی، پیام صریح نمایش داده می‌شود.
/// </summary>
public sealed class HomeController : Controller
{
    private readonly RateDisplayService _rates;
    private readonly ProductService _products;

    /// <summary>ساخت کنترلر.</summary>
    public HomeController(RateDisplayService rates, ProductService products)
    {
        ArgumentNullException.ThrowIfNull(rates);
        ArgumentNullException.ThrowIfNull(products);

        _rates = rates;
        _products = products;
    }

    /// <summary>
    /// نمایش صفحه اصلی همراه وضعیت نرخ‌ها و آخرین محصولاتِ منتشرشده.
    /// </summary>
    /// <remarks>
    /// اگر دیتابیس در دسترس نباشد، بخشِ ویترین به‌جای خطا پیامِ روشن نشان می‌دهد؛
    /// نمایشِ نرخ‌ها و بقیهٔ صفحه مختل نمی‌شود (جداسازیِ خرابی).
    /// </remarks>
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var rates = await _rates.GetAsync(cancellationToken);

        IReadOnlyList<SadGallery.Application.Catalog.PublicProduct> latest = [];
        var catalogUnavailable = false;

        try
        {
            latest = await _products.GetPublishedAsync(take: null, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // دیتابیس/ویترین نباید کلِ صفحهٔ اصلی را از کار بیندازد.
            // فقط پیام می‌دهیم؛ ریشهٔ خطا در لاگ ثبت می‌شود.
            catalogUnavailable = true;
        }

        ViewData["LatestProducts"] = latest;
        ViewData["CatalogUnavailable"] = catalogUnavailable;

        return View(rates);
    }
}
