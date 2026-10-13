using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SadGallery.Application.Tickets;
using SadGallery.Domain.Tickets;
using SadGallery.Web.Models.Tickets;
using SadGallery.Web.Security;
namespace SadGallery.Web.Areas.Operator.Controllers;

[Area("Operator"), Authorize(Policy = Policies.OperatorArea), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TicketsController(TicketService service, TicketInboxService inbox, ILogger<TicketsController> logger) : Controller
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
        if (!ModelState.IsValid || form.NextStatus is not { } next || form.Version() is not { Length: 8 } version)
            return await QueueError(new(user, true), new(status, search, from, until, page), "اطلاعات تغییر وضعیت معتبر نیست.", 400, ct);
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
        if (result == TicketWriteResult.NotFound) return NotFound();
        return await QueueError(new(user, true), new(status, search, from, until, page), result == TicketWriteResult.Conflict
            ? "تیکت توسط کاربر دیگری تغییر کرده است؛ فهرست تازه را بررسی و دوباره اقدام کنید."
            : "این تغییر وضعیت برای تیکت مجاز نیست.", result == TicketWriteResult.Conflict ? 409 : 400, ct);
    }
    private async Task<IActionResult> QueueError(TicketActor actor, TicketFilter filter, string error, int status, CancellationToken ct)
    {
        ModelState.AddModelError("", error);
        IReadOnlyList<TicketSummary> rows;
        try { rows = await service.ListAsync(actor, filter, ct); }
        catch (ArgumentException) { rows = await service.ListAsync(actor, new(), ct); }
        Response.StatusCode = status;
        return View("Index", rows);
    }
    [HttpGet("/operator/tickets/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        if (UserId is not { } user) return Forbid();
        var ticket = await service.GetOperatorAsync(id, ct);
        if (ticket != null) await inbox.MarkReadAsync(new(user, true), id, ticket.Public.Messages, ct);
        return ticket == null ? NotFound() : View(ticket);
    }
    [HttpPost("/operator/tickets/{id:guid}"), EnableRateLimiting(RateLimitPolicies.Tickets)]
    public async Task<IActionResult> Change(Guid id, ChangeTicketForm form, CancellationToken ct)
    {
        if (UserId is not { } user) return Forbid();
        if (!ModelState.IsValid || form.Version() is not { Length: 8 } version)
            return await FormError(id, form, "اطلاعات فرم معتبر نیست؛ ورودی‌ها را بررسی کنید.", 400, ct);
        TicketWriteResult result;
        try
        {
            result = await service.ChangeAsync(new(user, true), id, new(form.Body, form.InternalNote, form.Status, form.AssignTo, version), ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("Ticket change failed ({ErrorType})", exception.GetType().Name);
            return await FormError(id, form, "ثبت انجام نشد؛ متن شما حفظ شده است. دوباره تلاش کنید.", 503, ct);
        }
        if (result == TicketWriteResult.Saved) return RedirectToAction(nameof(Details), new { id });
        if (result == TicketWriteResult.NotFound) return NotFound();
        return await FormError(id, form, result == TicketWriteResult.Conflict
            ? "تیکت تغییر کرده است؛ گفت‌وگوی تازه را بررسی کنید و پاسخ حفظ‌شده را دوباره ثبت کنید."
            : "پیام، یادداشت یا تغییر وضعیت معتبر نیست. برای ثبت، پیام بنویسید یا وضعیت / مسئول را تغییر دهید.",
            result == TicketWriteResult.Conflict ? 409 : 400, ct);
    }

    private async Task<IActionResult> FormError(Guid id, ChangeTicketForm form, string error, int status, CancellationToken ct)
    {
        var ticket = await service.GetOperatorAsync(id, ct);
        if (ticket == null) return NotFound();
        ModelState.AddModelError("", error);
        ViewData["ChangeForm"] = form;
        Response.StatusCode = status;
        return View("Details", ticket);
    }
}
