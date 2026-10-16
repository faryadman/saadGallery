using Microsoft.Extensions.Caching.Memory;
using SadGallery.Application.Settings;

namespace SadGallery.Web.Services;

public sealed class ConnectionNoticeDisplay(ConnectionNoticeService service, IMemoryCache cache, ILogger<ConnectionNoticeDisplay> logger)
{
    private const string Key = "PublicConnectionNotice:v1";
    public void Invalidate() => cache.Remove(Key);
    public async Task<ConnectionNoticeText> GetAsync(CancellationToken ct)
    {
        if (cache.TryGetValue<ConnectionNoticeText>(Key, out var cached) && cached != null) return cached;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            var settings = await service.GetAsync(deadline.Token);
            var text = settings?.Text ?? ConnectionNoticeText.Default;
            cache.Set(Key, text, TimeSpan.FromSeconds(30));
            return text;
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Connection notice unavailable ({ErrorType})", exception.GetType().Name);
            cache.Set(Key, ConnectionNoticeText.Default, TimeSpan.FromSeconds(10));
            return ConnectionNoticeText.Default;
        }
    }
}
