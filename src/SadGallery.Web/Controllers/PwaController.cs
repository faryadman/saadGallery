using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using SadGallery.Web.Pwa;

namespace SadGallery.Web.Controllers;

[AllowAnonymous]
public sealed class PwaController(PwaAssets assets, RateDisplayService rates, RateFreshnessPolicy freshness, IClock clock) : Controller
{
    [HttpGet("/service-worker.js")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Worker()
    {
        Response.Headers["Service-Worker-Allowed"] = "/";
        return Content(assets.Worker, "application/javascript; charset=utf-8");
    }
    [HttpGet("/pwa/version")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Version() => Json(new { version = PwaAssets.Version, build = assets.BuildId });
    [HttpGet("/pwa/install")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Install() => View();
    [HttpGet("/pwa/rates")]
    public async Task<IActionResult> Rates(CancellationToken ct)
    {
        var model = await rates.GetAsync(ct);
        if (model.Items.Count == 0) return StatusCode(503, new { available = false });
        Response.Headers.CacheControl = "public, max-age=0, must-revalidate";
        Response.Headers["X-SadGallery-Public"] = "rates-v1";
        return Json(OfflineRates.From(model, freshness, clock));
    }
}
