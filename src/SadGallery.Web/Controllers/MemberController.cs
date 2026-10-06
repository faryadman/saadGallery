using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Web.Security;

namespace SadGallery.Web.Controllers;

/// <summary>
/// ناحیه امکانات ویژه اعضا (نیازمند ورود و حساب فعال).
/// در فاز ۳ حباب‌سنج و ماشین‌حساب کامل، در فاز ۵ تیکت‌ها و استعلام قیمت اینجا نمایش داده می‌شوند.
/// </summary>
[Authorize(Policy = Policies.MemberFeatures)]
public sealed class MemberController : Controller
{
    public IActionResult Index() => View();
}
