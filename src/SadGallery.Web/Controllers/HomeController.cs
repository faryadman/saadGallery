using Microsoft.AspNetCore.Mvc;

namespace SadGallery.Web.Controllers;

/// <summary>
/// صفحه اصلی (عمومی، بدون نیاز به ورود).
/// در فاز ۳ این صفحه کارت‌های نرخ واقعی، نمودار و آخرین محصولات را نمایش می‌دهد؛
/// تا آن زمان فقط ساختار و وضعیت سامانه نشان داده می‌شود (بدون هیچ نرخ ساختگی).
/// </summary>
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
