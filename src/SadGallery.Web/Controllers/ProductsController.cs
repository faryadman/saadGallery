using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Catalog;
using SadGallery.Domain.Enums;
using SadGallery.Web.Models.Catalog;

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
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ProductService products, ProductOptions options, ILogger<ProductsController> logger)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _products = products;
        _options = options;
        _logger = logger;
    }

    [HttpGet("/products")]
    public async Task<IActionResult> Index(
        string? budget,
        int? categoryId,
        bool inStockOnly = true,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CategoryRecord> categories = [];
        PublicProductPage? result = null;
        decimal? budgetToman = null;
        var effectiveInStockOnly = inStockOnly;
        string? filterError = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(budget))
            {
                if (ProductBudgetInput.TryParseToman(budget, out var parsedBudget))
                {
                    budgetToman = parsedBudget;
                }
                else
                {
                    // ورودی نامعتبر نباید بی‌صدا به «بدون فیلتر» تبدیل شود.
                    filterError = "بودجه را به‌صورت عدد مثبت و با واحد تومان وارد کنید.";
                }
            }

            effectiveInStockOnly = inStockOnly || budgetToman.HasValue;
            categories = await _products.GetCategoriesAsync(cancellationToken);

            if (categoryId is { } requestedCategory &&
                (requestedCategory <= 0 || categories.All(category => category.Id != requestedCategory)))
            {
                filterError = "دسته‌بندی انتخاب‌شده معتبر نیست.";
            }

            if (filterError is null)
            {
                result = await _products.SearchPublishedCatalogAsync(
                    categoryId,
                    effectiveInStockOnly,
                    budgetToman,
                    page,
                    cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // از دسترس خارج شدنِ دیتابیس نباید استثنای خام یا فهرستِ خالیِ دروغین نشان دهد.
            _logger.LogError(exception, "جست‌وجوی ویترین محصولات عمومی ناموفق بود.");

            return View(new PublicCatalogViewModel
            {
                Categories = categories,
                BudgetInput = budget,
                BudgetToman = budgetToman,
                CategoryId = categoryId,
                InStockOnly = effectiveInStockOnly,
                Page = Math.Max(page, 1),
                PageSize = _options.CatalogPageSize,
                CatalogUnavailable = true,
                FilterError = filterError,
            });
        }

        return View(new PublicCatalogViewModel
        {
            Products = result?.Items ?? [],
            Categories = categories,
            BudgetInput = budget,
            BudgetToman = budgetToman,
            CategoryId = categoryId,
            InStockOnly = effectiveInStockOnly,
            Page = result?.Page ?? Math.Max(page, 1),
            PageSize = result?.PageSize ?? _options.CatalogPageSize,
            TotalCount = result?.TotalCount ?? 0,
            FilterError = filterError,
        });
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
