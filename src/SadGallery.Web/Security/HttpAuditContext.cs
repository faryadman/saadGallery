using System.Security.Claims;
using SadGallery.Application.Administration;

namespace SadGallery.Web.Security;

public sealed class HttpAuditContext(IHttpContextAccessor accessor) : IAuditContext
{
    public AuditActor? Actor
    {
        get
        {
            var context = accessor.HttpContext;
            if (context?.User.Identity?.IsAuthenticated != true || !int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
            var correlation = context.TraceIdentifier;
            return new(id, context.Connection.RemoteIpAddress?.ToString(), correlation.Length > 64 ? correlation[..64] : correlation);
        }
    }
}
