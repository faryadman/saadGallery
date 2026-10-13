using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SadGallery.Application.Tickets;
using SadGallery.Web.Models.Tickets;
using SadGallery.Web.Security;
namespace SadGallery.Web.Controllers;

[Authorize(Policy = Policies.MemberFeatures)]
public sealed class TicketsController(TicketService service) : Controller
{
    private int? UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : null;
    [HttpGet("/tickets")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        if (UserId is not { } user) return Forbid();
        if (page is < 1 or > 10000) return BadRequest();
        return View(await service.ListAsync(new(user, false), new(Page: page), ct));
    }
    [HttpGet("/tickets/new")]
    public IActionResult Create(int? productId = null) => View(new CreateTicketForm { ProductId = productId, Type = productId.HasValue ? SadGallery.Domain.Tickets.TicketType.PriceInquiry : SadGallery.Domain.Tickets.TicketType.Other });
    [HttpPost("/tickets/new"), EnableRateLimiting(RateLimitPolicies.Tickets)]
    public async Task<IActionResult> Create(CreateTicketForm form, CancellationToken ct)
    {
        if (UserId is not { } user) return Forbid();
        if (ModelState.IsValid)
        {
            var id = await service.CreateAsync(new(user, false), new(form.Subject, form.Type, form.Body, form.ProductId), ct);
            if (id.HasValue) return RedirectToAction(nameof(Details), new { id });
            ModelState.AddModelError("", "اطلاعات تیکت یا کالای انتخاب‌شده معتبر نیست.");
        }
        return View(form);
    }
    [HttpGet("/tickets/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        if (UserId is not { } user) return Forbid();
        var ticket = await service.GetCustomerAsync(user, id, ct);
        return ticket == null ? NotFound() : View(ticket);
    }
    [HttpPost("/tickets/{id:guid}/reply"), EnableRateLimiting(RateLimitPolicies.Tickets)]
    public async Task<IActionResult> Reply(Guid id, ChangeTicketForm form, CancellationToken ct)
    {
        if (UserId is not { } user) return Forbid();
        if (!ModelState.IsValid || form.Version() is not { } version) return BadRequest("اطلاعات پیام معتبر نیست.");
        // Never accept operator-only fields from customer input.
        var result = await service.ChangeAsync(new(user, false), id, new(form.Body, form.InternalNote, form.Status, form.AssignTo, version), ct);
        return result switch
        {
            TicketWriteResult.Saved => RedirectToAction(nameof(Details), new { id }),
            TicketWriteResult.NotFound => NotFound(),
            TicketWriteResult.Conflict => Conflict("تیکت تغییر کرده است؛ صفحه را تازه کنید."),
            _ => BadRequest("اطلاعات پیام معتبر نیست."),
        };
    }
}
