using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Administration;
using SadGallery.Web.Security;

namespace SadGallery.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Policy = Policies.AdminOnly), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DashboardController(IAdministrationStore store, ILogger<DashboardController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try { return View(await store.DashboardAsync(ct)); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Dashboard unavailable ({ErrorType})", ex.GetType().Name);
            Response.StatusCode = 503;
            return View("~/Views/Shared/ManagementUnavailable.cshtml");
        }
    }
}
