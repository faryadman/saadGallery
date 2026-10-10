using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Catalog;

namespace SadGallery.Web.Controllers;

/// <summary>
/// ویترین عمومی محصولات: فهرست و جزئیات.
/// </summary>
/// <remarks>
/// <para>
/// این کنترلر عمداً هیچ ویژگیِ <c>[Authorize]</c> ندارد: ویترین برای همه باز است،
/// از جمله کاربرِ ناشناس. کنترل دسترسیِ مدیریت در ناحیهٔ Operator است.
/// </para>
/// <para>
/// <b>نکتهٔ امنیتی:</b> «منتشر نشده بودن» در اینجا با نپرسیدن از دیتابیس اعمال نمی‌شود،
/// بلکه در خودِ پرس‌وجو اعمال می‌شود؛ یعنی شناسهٔ یک کالای منتشرنشده یا حذف‌شده
/// دقیقاً همان پاسخی را می‌گیرد که یک شناسهٔ اصلاً موجود — ۴۰۴، نه ۴۰۳.
/// (۴۰۳ به مهاجم می‌گفت «این شناسه وجود دارد».)
/// </para>
/// </remarks>
public sealed class ProductsController : Controller
{
    private readonly ProductService _products;
    private readonly ProductOptions _options;

    public ProductsController(ProductService products, ProductOptions options)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(options);

        _products = products;
        _options = options;
    }

    [HttpGet("/products")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        IReadOnlyList<PublicProduct> items;

        try
        {
            // اندازهٔ صفحهٔ ویترین از تنظیمات می‌آید، نه از مقدارِ ثابت در کد،
            // و با اندازهٔ صفحهٔ خانه یکی نیست (HomeLatestCount در برابر CatalogPageSize).
            items = await _products.GetPublishedAsync(_options.CatalogPageSize, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // از دسترس خارج شدنِ دیتابیس نباید کل صفحه را با خطای ۵۰۰ بشکند؛
            // همان سیاستِ صفحهٔ خانه: نمایشِ وضعیتِ «در دسترس نیست» به‌جای استثنای خام.
            // جزئیاتِ خطا هرگز به مرورگر فرستاده نمی‌شود؛ فقط در لاگ ثبت می‌گردد.
            ViewData["CatalogUnavailable"] = true;
            items = [];

            // ثبت در لاغ برای پیگیریِ عملیاتی (بدون نشتِ اطلاعات به پاسخ)
            HttpContext.RequestServices
                .GetService<Microsoft.Extensions.Logging.ILogger<ProductsController>>()?
                .LogError(exception, "خواندن فهرست محصولات عمومی ناموفق بود.");
        }

        return View(items);
    }

    [HttpGet("/products/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        PublicProduct? product;

        try
        {
            product = await _products.GetPublishedDetailAsync(id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // اگر دیتابیس در دسترس نباشد، «یافت نشد» دروغین نشان نمی‌دهیم؛
            // وضعیتِ واقعی را به کاربر می‌گوییم تا بعداً دوباره تلاش کند.
            HttpContext.RequestServices
                .GetService<Microsoft.Extensions.Logging.ILogger<ProductsController>>()?
                .LogError(exception, "خواندن جزئیات محصول {ProductId} ناموفق بود.", id);

            ViewData["CatalogUnavailable"] = true;

            return View("Unavailable");
        }

        if (product is null)
        {
            return NotFound();
        }

        return View(product);
    }
}
