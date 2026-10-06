using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Web.Security;

namespace SadGallery.Web.Areas.Admin.Controllers;

/// <summary>
/// پنل مدیر. کنترل دسترسی سمت سرور با Policy «AdminOnly» (نقش Admin + حساب فعال).
/// محتوای واقعی (کاربران، نقش‌ها، منابع نرخ، تنظیمات، گزارش‌ها) در فاز ۶ ساخته می‌شود.
/// </summary>
[Area("Admin")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class DashboardController : Controller
{
    public IActionResult Index() => View();
}
