namespace SadGallery.Application.Settings;

public sealed record ConnectionNoticeText(string OfflineTitle, string OfflineDescription, string OnlineTitle, string OnlineDescription)
{
    public static ConnectionNoticeText Default { get; } = new("اتصال اینترنت قطع است",
        "اطلاعات صفحه به‌روز نمی‌شود؛ نرخ‌های ذخیره‌شده لحظه‌ای نیستند.", "اتصال شبکه برگشت",
        "برای دریافت اطلاعات تازه، صفحه را به‌روز کنید.");
}
public sealed record ConnectionNoticeSettings(ConnectionNoticeText Text, byte[] Version, DateTimeOffset? UpdatedAtUtc, int? UpdatedByUserId);
public enum NoticeSaveResult { Saved, Unchanged, Invalid, Conflict, Unavailable, Forbidden }
public interface IConnectionNoticeStore
{
    Task<ConnectionNoticeSettings?> GetAsync(CancellationToken ct);
    Task<NoticeSaveResult> SaveAsync(int actorUserId, ConnectionNoticeText text, byte[] version, CancellationToken ct);
}
public sealed class ConnectionNoticeService(IConnectionNoticeStore store)
{
    public const int TitleLimit = 80;
    public const int DescriptionLimit = 200;
    private static bool Valid(string? text, int limit) => !string.IsNullOrWhiteSpace(text) && text.Length <= limit &&
        !text.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t');
    public Task<ConnectionNoticeSettings?> GetAsync(CancellationToken ct) => store.GetAsync(ct);
    public Task<NoticeSaveResult> SaveAsync(int actorUserId, ConnectionNoticeText text, byte[] version, CancellationToken ct)
    {
        if (actorUserId <= 0 || version.Length != 8 || !Valid(text.OfflineTitle, TitleLimit) ||
            !Valid(text.OnlineTitle, TitleLimit) || !Valid(text.OfflineDescription, DescriptionLimit) || !Valid(text.OnlineDescription, DescriptionLimit))
            return Task.FromResult(NoticeSaveResult.Invalid);
        return store.SaveAsync(actorUserId, new(text.OfflineTitle.Trim(), text.OfflineDescription.Trim(), text.OnlineTitle.Trim(), text.OnlineDescription.Trim()), version, ct);
    }
}
