using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Web.Security;

namespace SadGallery.Web.Areas.Operator.Controllers;

/// <summary>
/// پنل اپراتور. کنترل دسترسی سمت سرور با Policy «OperatorArea» (نقش Operator یا Admin + حساب فعال).
/// محتوای واقعی (محصولات، صف تیکت، گزارش عملیاتی) در فازهای ۴ و ۵ ساخته می‌شود.
/// </summary>
[Area("Operator")]
[Authorize(Policy = Policies.OperatorArea)]
public sealed class DashboardController : Controller
{
    public IActionResult Index() => View();
}
