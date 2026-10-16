namespace SadGallery.Infrastructure.Persistence.Entities;

public sealed class ConnectionNoticeSetting
{
    public int Id { get; set; }
    public string OfflineTitle { get; set; } = "";
    public string OfflineDescription { get; set; } = "";
    public string OnlineTitle { get; set; } = "";
    public string OnlineDescription { get; set; } = "";
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class ConnectionNoticeEdit
{
    public long Id { get; set; }
    public int ActorUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string Fields { get; set; } = "";
}
