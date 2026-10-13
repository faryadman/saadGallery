using SadGallery.Domain.Tickets;
namespace SadGallery.Infrastructure.Persistence.Entities;

public sealed class SupportTicket
{
    public Guid Id { get; set; }
    public long Number { get; set; }
    public int CustomerUserId { get; set; }
    public string Subject { get; set; } = "";
    public TicketType Type { get; set; }
    public int? RelatedProductId { get; set; }
    public TicketStatus Status { get; set; }
    public int? AssignedOperatorUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastActivityAtUtc { get; set; }
    public DateTimeOffset? FirstResponseAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<TicketMessage> Messages { get; set; } = [];
}
public sealed class TicketMessage
{
    public long Id { get; set; }
    public Guid TicketId { get; set; }
    public int AuthorUserId { get; set; }
    public bool IsOperator { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class TicketInternalNote
{
    public long Id { get; set; }
    public Guid TicketId { get; set; }
    public int AuthorUserId { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class TicketEvent
{
    public long Id { get; set; }
    public Guid? TicketId { get; set; }
    public int? ActorUserId { get; set; }
    public string Kind { get; set; } = "";
    public string? Detail { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
