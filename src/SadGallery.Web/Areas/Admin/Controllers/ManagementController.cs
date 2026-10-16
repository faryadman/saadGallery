using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Administration;
using SadGallery.Application.Market;
using SadGallery.Web.Models;
using SadGallery.Web.Security;

namespace SadGallery.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Policy = Policies.AdminOnly), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ManagementController(IAdministrationStore store, RateOptions rateOptions, ILogger<ManagementController> logger) : Controller
{
    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private IActionResult Unavailable(Exception ex)
    {
        logger.LogWarning("Administration unavailable ({ErrorType})", ex.GetType().Name);
        Response.StatusCode = 503;
        return View("~/Views/Shared/ManagementUnavailable.cshtml");
    }
    [HttpGet("/admin/users")]
    public async Task<IActionResult> Users(int page = 1, string? search = null, CancellationToken ct = default)
    {
        ViewData["Search"] = search?.Length > 100 ? search[..100] : search;
        try { return View(await store.UsersAsync(page, search, ct)); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpGet("/admin/users/{id:int}")]
    public async Task<IActionResult> UserAccess(int id, CancellationToken ct)
    {
        try { var user = await store.UserAsync(id, ct); return user == null ? NotFound() : View(UserAccessForm.From(user)); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpPost("/admin/users/{id:int}")]
    public async Task<IActionResult> UserAccess(int id, UserAccessForm form, CancellationToken ct)
    {
        form.Id = id;
        AdminSaveResult result;
        try
        {
            result = ModelState.IsValid ? await store.SaveUserAsync(ActorId, id, form.Role, form.IsActive, form.Version, ct) : AdminSaveResult.Invalid;
            if (result == AdminSaveResult.Forbidden) return Forbid();
            if (result == AdminSaveResult.NotFound) return NotFound();
            if (result is AdminSaveResult.Saved or AdminSaveResult.Unchanged)
            {
                TempData["Notice"] = result == AdminSaveResult.Saved ? "دسترسی کاربر ذخیره شد؛ نشست قبلی او باطل شد." : "دسترسی تغییری نکرده است.";
                return RedirectToAction(nameof(Users));
            }
            var latest = await store.UserAsync(id, ct); if (latest == null) return NotFound();
            form.DisplayName = latest.DisplayName;
            form.Version = latest.Version; ModelState.Remove(nameof(form.Version));
            if (result == AdminSaveResult.Conflict) ViewData["CurrentAccess"] = $"نسخه ذخیره‌شده: {AdminRules.RoleTitle(latest.Role)}، {(latest.IsActive ? "فعال" : "غیرفعال")}";
            Response.StatusCode = result == AdminSaveResult.Conflict ? 409 : 400;
            ModelState.AddModelError("", result switch
            {
                AdminSaveResult.Conflict => "دسترسی کاربر تغییر کرده است؛ انتخاب شما حفظ شد. نسخه ذخیره‌شده را بررسی و دوباره ثبت کنید.",
                AdminSaveResult.SelfChange => "برای حفظ دسترسی مدیریت، تغییر نقش یا وضعیت حساب خودتان از این بخش مجاز نیست.",
                AdminSaveResult.LastAdmin => "حداقل یک مدیر فعال باید باقی بماند.",
                _ => "اطلاعات معتبر نیست؛ نقش و وضعیت حساب را بررسی کنید."
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("User access save failed ({ErrorType})", ex.GetType().Name);
            Response.StatusCode = 503; ModelState.AddModelError("", "ذخیره انجام نشد؛ انتخاب شما حفظ شده است. دوباره تلاش کنید.");
        }
        return View(form);
    }
    [HttpGet("/admin/settings")]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        ViewData["MinimumSeconds"] = Math.Max(10, rateOptions.FetchIntervalSeconds);
        try
        {
            var settings = await store.GetAsync(ct);
            if (settings == null) { Response.StatusCode = 503; return View("~/Views/Shared/ManagementUnavailable.cshtml"); }
            return View(RateSettingsForm.From(settings));
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpPost("/admin/settings")]
    public async Task<IActionResult> Settings(RateSettingsForm form, CancellationToken ct)
    {
        ViewData["MinimumSeconds"] = Math.Max(10, rateOptions.FetchIntervalSeconds);
        try
        {
            var version = form.Version();
            var result = ModelState.IsValid && version is { Length: 8 }
                ? await store.SaveSettingsAsync(ActorId, form.RatesPaused, form.IntervalSeconds, version, ct) : AdminSaveResult.Invalid;
            if (result == AdminSaveResult.Forbidden) return Forbid();
            if (result is AdminSaveResult.Saved or AdminSaveResult.Unchanged)
            { TempData["Notice"] = "تنظیمات دریافت نرخ ذخیره شد."; return RedirectToAction(nameof(Settings)); }
            var latest = await store.GetAsync(ct);
            if (latest != null)
            {
                form.RowVersion = Convert.ToBase64String(latest.Version); ModelState.Remove(nameof(form.RowVersion));
                ViewData["CurrentSettings"] = $"نسخه ذخیره‌شده: {(latest.Paused ? "متوقف" : "فعال")}، فاصله دریافت {latest.IntervalSeconds} ثانیه";
            }
            Response.StatusCode = result == AdminSaveResult.Conflict ? 409 : 400;
            ModelState.AddModelError("", result == AdminSaveResult.Conflict ? "مدیر دیگری تنظیمات را تغییر داده است؛ انتخاب شما حفظ شد. نسخه ذخیره‌شده را بررسی و دوباره ثبت کنید." : "ذخیره ممکن نیست؛ فاصله دریافت باید حداقل برابر مقدار مجاز سرور و حداکثر ۳۶۰۰ ثانیه باشد.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Rate settings save failed ({ErrorType})", ex.GetType().Name);
            Response.StatusCode = 503; ModelState.AddModelError("", "ذخیره انجام نشد؛ انتخاب شما حفظ شده است. دوباره تلاش کنید.");
        }
        return View(form);
    }
    [HttpGet("/admin/audit")]
    public async Task<IActionResult> Audit(int page = 1, CancellationToken ct = default)
    {
        try { return View(await store.AuditAsync(page, ct)); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpGet("/admin/audit.csv")]
    public async Task<IActionResult> AuditExport(CancellationToken ct)
    {
        try { return File(AdminCsv.Audit(await store.AuditExportAsync(ct)), "text/csv; charset=utf-8", "operations-audit.csv"); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
}
