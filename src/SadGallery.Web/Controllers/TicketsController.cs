using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SadGallery.Application.Tickets;
using SadGallery.Domain.Tickets;
using SadGallery.Domain.Enums;
using SadGallery.Application.Abstractions;
using SadGallery.Infrastructure.Identity;
using SadGallery.Web.Models.Tickets;
using SadGallery.Web.Security;
namespace SadGallery.Web.Controllers;

[Authorize(Policy = Policies.MemberFeatures)]
public sealed class TicketsController(TicketService service, TicketPhotoService photos, IMediaStore media, ILogger<TicketsController> logger) : Controller
{
    private int? UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : null;
    [HttpGet("/tickets")]
    public async Task<IActionResult> Index(TicketStatus? status, string? search, DateTimeOffset? from, DateTimeOffset? until, int page = 1, CancellationToken ct = default)
    {
        if (UserId is not { } user) return Forbid();
        if (!ModelState.IsValid) return BadRequest("فیلتر جست‌وجو معتبر نیست.");
        try { return View(await service.ListAsync(new(user, false), new(status, search, from, until, page), ct)); }
        catch (ArgumentException) { return BadRequest("فیلتر جست‌وجو معتبر نیست."); }
    }
    [HttpGet("/tickets/new")]
    public IActionResult Create(int? productId = null) => View(new CreateTicketForm { ProductId = productId, Type = productId.HasValue ? SadGallery.Domain.Tickets.TicketType.PriceInquiry : SadGallery.Domain.Tickets.TicketType.Other });
    [HttpPost("/tickets/new"), EnableRateLimiting(RateLimitPolicies.Tickets)]
    [RequestSizeLimit(TicketPhotoService.MaxRequestBytes), RequestFormLimits(MultipartBodyLengthLimit = TicketPhotoService.MaxRequestBytes)]
    public async Task<IActionResult> Create(CreateTicketForm form, CancellationToken ct)
    {
        if (UserId is not { } user) return Forbid();
        if (ModelState.IsValid)
        {
            var selected = await TicketPhotoReader.ReadAsync(form.Photos, ct);
            if (selected.Error != null) ModelState.AddModelError("Photos", selected.Error);
            else
            {
                try
                {
                    var outcome = await photos.CreateAsync(new(user, false), new(form.Subject, form.Type, form.Body, form.ProductId), selected.Photos, ct);
                    if (outcome.Result == TicketWriteResult.Saved) return RedirectToAction(nameof(Details), new { id = outcome.TicketId });
                    ModelState.AddModelError("", outcome.Error ?? "اطلاعات تیکت یا عکس‌ها معتبر نیست.");
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError("Ticket submission failed ({ErrorType})", exception.GetType().Name);
                    ModelState.AddModelError("", "ارسال درخواست انجام نشد؛ لطفاً دوباره تلاش کنید.");
                }
            }
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
    [HttpGet("/tickets/photos/{photoId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Photo(Guid photoId, CancellationToken ct)
    {
        if (UserId is not { } user) return Forbid();
        var staff = User.IsInRole(RoleNames.Operator) || User.IsInRole(RoleNames.Admin);
        var photo = await photos.GetAsync(new(user, staff), photoId, ct);
        if (photo == null || !media.Exists(MediaKind.PrivateFile, photo.StoredFileName)) return NotFound();
        Response.Headers.CacheControl = "no-store, private";
        return PhysicalFile(media.GetPhysicalPath(MediaKind.PrivateFile, photo.StoredFileName), photo.ContentType);
    }
    [HttpPost("/tickets/{id:guid}/reply"), EnableRateLimiting(RateLimitPolicies.Tickets)]
    [RequestSizeLimit(TicketPhotoService.MaxRequestBytes), RequestFormLimits(MultipartBodyLengthLimit = TicketPhotoService.MaxRequestBytes)]
    public async Task<IActionResult> Reply(Guid id, ChangeTicketForm form, CancellationToken ct)
    {
        if (UserId is not { } user) return Forbid();
        var ticket = await service.GetCustomerAsync(user, id, ct);
        if (ticket == null)
        {
            await photos.RecordAccessDeniedAsync(user, ct);
            return NotFound();
        }
        ViewData["ReplyBody"] = form.Body;
        if (!ModelState.IsValid || form.Version() is not { } version)
        {
            ModelState.AddModelError("", "اطلاعات پیام معتبر نیست؛ صفحه را تازه کنید.");
            return View("Details", ticket);
        }
        var selected = await TicketPhotoReader.ReadAsync(form.Photos, ct);
        if (selected.Error != null)
        {
            ModelState.AddModelError("Photos", selected.Error);
            return View("Details", ticket);
        }
        TicketSubmissionOutcome outcome;
        try
        {
            outcome = await photos.ReplyAsync(new(user, false), id,
                new(form.Body, form.InternalNote, form.Status, form.AssignTo, version), selected.Photos, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("Ticket reply failed ({ErrorType})", exception.GetType().Name);
            ModelState.AddModelError("", "ارسال پاسخ انجام نشد؛ لطفاً دوباره تلاش کنید.");
            return View("Details", ticket);
        }
        if (outcome.Error != null)
        {
            ModelState.AddModelError("Photos", outcome.Error);
            return View("Details", ticket);
        }
        return outcome.Result switch
        {
            TicketWriteResult.Saved => RedirectToAction(nameof(Details), new { id }),
            TicketWriteResult.NotFound => NotFound(),
            TicketWriteResult.Conflict => Conflict("تیکت تغییر کرده است؛ صفحه را تازه کنید."),
            _ => BadRequest("اطلاعات پیام معتبر نیست."),
        };
    }
}
