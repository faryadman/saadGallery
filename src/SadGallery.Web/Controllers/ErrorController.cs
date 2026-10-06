using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Web.Models;

namespace SadGallery.Web.Controllers;

/// <summary>
/// صفحه خطای فارسی.
/// قاعده امنیتی: هیچ جزئیات داخلی (Stack trace، نام جدول، پیام استثنا) به کاربر نمایش داده نمی‌شود.
/// </summary>
[AllowAnonymous]
[Route("error")]
public sealed class ErrorController : Controller
{
    [Route("")]
    [Route("{statusCode:int}")]
    public IActionResult Index(int? statusCode = null)
    {
        var effectiveStatusCode = statusCode ?? StatusCodes.Status500InternalServerError;

        Response.StatusCode = effectiveStatusCode;

        // نام View صریح است تا از Views/Shared/Error.cshtml استفاده شود
        // (مسیر پیش‌فرض «Views/Error/Index.cshtml» وجود ندارد و باعث خطای زمان اجرا می‌شد).
        return View("Error", ErrorViewModel.For(effectiveStatusCode));
    }
}
