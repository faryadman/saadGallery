using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SadGallery.Application.Tickets;
using SadGallery.Domain.Tickets;
using SadGallery.Web.Models.Tickets;
using SadGallery.Web.Security;
namespace SadGallery.Web.Areas.Operator.Controllers;

[Area("Operator"), Authorize(Policy = Policies.OperatorArea)]
public sealed class TicketsController(TicketService service) : Controller
{
    private int? UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : null;
    [HttpGet("/operator/tickets")]
    public async Task<IActionResult> Index(TicketStatus? status, string? search, DateTimeOffset? from, DateTimeOffset? until, int page = 1, CancellationToken ct = default)
    {
        if (UserId is not { } user) return Forbid();
        if (!ModelState.IsValid) return BadRequest("فیلتر جست‌وجو معتبر نیست.");
        try { return View(await service.ListAsync(new(user, true), new(status, search, from, until, page), ct)); }
        catch (ArgumentException) { return BadRequest("فیلتر جست‌وجو معتبر نیست."); }
    }
    [HttpPost("/operator/tickets/{id:guid}/status"), EnableRateLimiting(RateLimitPolicies.Tickets)]
    public async Task<IActionResult> UpdateStatus(Guid id, TicketStatusForm form,
        [FromQuery] TicketStatus? status, [FromQuery] string? search,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? until,
        [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (UserId is not { } user) return Forbid();
        if (!ModelState.IsValid || form.NextStatus is not { } next || form.Version() is not { } version)
            return BadRequest("اطلاعات تغییر وضعیت معتبر نیست.");
        var result = await service.ChangeAsync(new(user, true), id, new(null, false, next, null, version), ct);
        if (result == TicketWriteResult.Saved)
        {
            TempData["Notice"] = $"وضعیت تیکت به «{TicketLabels.Status(next, true)}» تغییر کرد.";
            return RedirectToAction(nameof(Index), new
            {
                status = (int?)status, search,
                from = Request.Query["from"].ToString(), until = Request.Query["until"].ToString(), page,
            });
        }
        return result switch
        {
            TicketWriteResult.NotFound => NotFound(),
            TicketWriteResult.Conflict => Conflict("تیکت توسط کاربر دیگری تغییر کرده است؛ فهرست را تازه کنید."),
            _ => BadRequest("این تغییر وضعیت برای تیکت مجاز نیست."),
        };
    }
    [HttpGet("/operator/tickets/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        if (UserId == null) return Forbid();
        var ticket = await service.GetOperatorAsync(id, ct);
        return ticket == null ? NotFound() : View(ticket);
    }
    [HttpPost("/operator/tickets/{id:guid}"), EnableRateLimiting(RateLimitPolicies.Tickets)]
    public async Task<IActionResult> Change(Guid id, ChangeTicketForm form, CancellationToken ct)
    {
        if (UserId is not { } user) return Forbid();
        if (!ModelState.IsValid || form.Version() is not { } version) return BadRequest("اطلاعات تغییر معتبر نیست.");
        var result = await service.ChangeAsync(new(user, true), id, new(form.Body, form.InternalNote, form.Status, form.AssignTo, version), ct);
        return result switch
        {
            TicketWriteResult.Saved => RedirectToAction(nameof(Details), new { id }),
            TicketWriteResult.NotFound => NotFound(),
            TicketWriteResult.Conflict => Conflict("تیکت تغییر کرده است؛ صفحه را تازه کنید."),
            _ => BadRequest("تغییر وضعیت یا اطلاعات پیام معتبر نیست."),
        };
    }
}
