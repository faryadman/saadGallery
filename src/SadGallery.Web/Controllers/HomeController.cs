using Microsoft.AspNetCore.Mvc;
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

    /// <summary>ساخت کنترلر.</summary>
    public HomeController(RateDisplayService rates)
    {
        ArgumentNullException.ThrowIfNull(rates);
        _rates = rates;
    }

    /// <summary>نمایش صفحه اصلی همراه وضعیت نرخ‌ها.</summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _rates.GetAsync(cancellationToken));
}
