namespace SadGallery.Infrastructure.Persistence.Entities;

public sealed class StoreSettings
{
    public int Id { get; set; } = 1;
    public bool RatesPaused { get; set; }
    // Null preserves the deployment's configured interval until an administrator explicitly changes it.
    public int? FetchIntervalSeconds { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class AuditEvent
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public int? ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Fields { get; set; } = "";
    public string? IpAddress { get; set; }
    public string CorrelationId { get; set; } = "";
}
