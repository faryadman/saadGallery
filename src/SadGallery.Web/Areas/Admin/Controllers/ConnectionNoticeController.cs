using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Settings;
using SadGallery.Web.Models;
using SadGallery.Web.Security;
using SadGallery.Web.Services;

namespace SadGallery.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Policy = Policies.AdminOnly), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ConnectionNoticeController(ConnectionNoticeService service, ConnectionNoticeDisplay display, ILogger<ConnectionNoticeController> logger) : Controller
{
    [HttpGet("/admin/connection-notice")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var settings = await Read(ct);
        if (settings == null)
        {
            Response.StatusCode = 503;
            ModelState.AddModelError("", "تنظیمات در دسترس نیست؛ اتصال دیتابیس و اجرای مهاجرت را بررسی کنید.");
        }
        return View(ConnectionNoticeForm.From(settings));
    }
    [HttpPost("/admin/connection-notice")]
    public async Task<IActionResult> Index(ConnectionNoticeForm form, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var user) || user <= 0) return Forbid();
        if (!ModelState.IsValid || form.Version() is not { Length: 8 } version)
            return await Error(form, "اطلاعات فرم معتبر نیست؛ عنوان و توضیح هر دو حالت را بررسی کنید.", 400, ct);
        NoticeSaveResult result;
        try { result = await service.SaveAsync(user, form.Text(), version, ct); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            display.Invalidate();
            logger.LogError("Connection notice save failed ({ErrorType})", exception.GetType().Name);
            return await Error(form, "ذخیره انجام نشد؛ متن شما حفظ شده است. دوباره تلاش کنید.", 503, ct);
        }
        if (result is NoticeSaveResult.Saved or NoticeSaveResult.Unchanged)
        {
            display.Invalidate();
            TempData["Notice"] = result == NoticeSaveResult.Saved ? "متن اعلان اتصال ذخیره شد؛ در صفحه‌های تازه نمایش داده می‌شود." : "متن‌ها تغییری نکرده‌اند.";
            return RedirectToAction(nameof(Index));
        }
        if (result == NoticeSaveResult.Forbidden) return Forbid();
        return await Error(form, result == NoticeSaveResult.Conflict
            ? "مدیر دیگری تنظیمات را تغییر داده است؛ متن شما حفظ شده. نسخه ذخیره‌شده را در صفحه جدا بررسی و دوباره ثبت کنید."
            : "ذخیره ممکن نیست؛ متن و وضعیت تنظیمات را بررسی کنید.",
            result == NoticeSaveResult.Conflict ? 409 : result == NoticeSaveResult.Unavailable ? 503 : 400, ct);
    }
    private async Task<ConnectionNoticeSettings?> Read(CancellationToken ct)
    {
        try { return await service.GetAsync(ct); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("Connection notice editor unavailable ({ErrorType})", exception.GetType().Name);
            return null;
        }
    }
    private async Task<IActionResult> Error(ConnectionNoticeForm form, string message, int status, CancellationToken ct)
    {
        var latest = await Read(ct);
        ModelState.AddModelError("", message);
        ModelState.Remove(nameof(form.RowVersion));
        form.RowVersion = latest == null ? "" : Convert.ToBase64String(latest.Version);
        form.Available = latest != null; form.UpdatedAtUtc = latest?.UpdatedAtUtc; form.UpdatedByUserId = latest?.UpdatedByUserId;
        Response.StatusCode = status;
        return View("Index", form);
    }
}
