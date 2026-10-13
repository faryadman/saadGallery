using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Tickets;
using SadGallery.Infrastructure.Identity;

namespace SadGallery.Web.ViewComponents;

public sealed class TicketBadgeViewComponent(TicketInboxService inbox, ILogger<TicketBadgeViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(bool operatorQueue = false)
    {
        if (User.Identity?.IsAuthenticated != true ||
            !int.TryParse(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier), out var user) || user <= 0 ||
            (operatorQueue && !User.IsInRole(RoleNames.Operator) && !User.IsInRole(RoleNames.Admin))) return Content("");
        var key = "TicketUnread:" + operatorQueue;
        if (!HttpContext.Items.TryGetValue(key, out var value))
        {
            try { value = (await inbox.CountsAsync(new(user, operatorQueue), [], HttpContext.RequestAborted)).Total; }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A notification must not prevent unrelated pages from rendering when SQL is unavailable.
                logger.LogWarning("Ticket badge unavailable ({ErrorType})", exception.GetType().Name);
                value = 0;
            }
            HttpContext.Items[key] = value;
        }
        ViewData["Scope"] = operatorQueue ? "operator" : "customer";
        return View((int)value!);
    }
}
