using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Administration;
using SadGallery.Web.Security;

namespace SadGallery.Web.Areas.Operator.Controllers;

[Area("Operator"), Authorize(Policy = Policies.OperatorArea), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportsController(IAdministrationStore store, ILogger<ReportsController> logger) : Controller
{
    [HttpGet("/operator/reports")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try { return View(await store.DashboardAsync(ct)); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Operational report unavailable ({ErrorType})", ex.GetType().Name);
            Response.StatusCode = 503; return View("~/Views/Shared/ManagementUnavailable.cshtml");
        }
    }
}
