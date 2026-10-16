using Microsoft.AspNetCore.Mvc;
using SadGallery.Web.Services;

namespace SadGallery.Web.ViewComponents;

public sealed class ConnectionNoticeViewComponent(ConnectionNoticeDisplay display) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() => View(await display.GetAsync(HttpContext.RequestAborted));
}
