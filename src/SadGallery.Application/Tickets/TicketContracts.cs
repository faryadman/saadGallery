using SadGallery.Domain.Tickets;
using SadGallery.Application.Abstractions;
namespace SadGallery.Application.Tickets;

public sealed record TicketActor(int UserId, bool IsOperator);
public sealed record TicketPhotoRecord(Guid Id, int Width, int Height, long SizeBytes);
public sealed record StoredTicketPhoto(Guid Id, string StoredFileName, ImageFormat Format, int Width, int Height, long SizeBytes);
public sealed record TicketPhotoFile(string StoredFileName, string ContentType);
public sealed record TicketMessageRecord(int AuthorUserId, bool IsOperator, string Body, DateTimeOffset CreatedAtUtc,
    IReadOnlyList<TicketPhotoRecord>? Photos = null, long Id = 0);
public sealed record TicketNoteRecord(int AuthorUserId, string Body, DateTimeOffset CreatedAtUtc);
public sealed record TicketSummary(Guid Id, string TrackingCode, string Subject, TicketType Type, TicketStatus Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset LastActivityAtUtc, DateTimeOffset? FirstResponseAtUtc,
    DateTimeOffset? ClosedAtUtc, int? AssignedOperatorUserId, int? RelatedProductId, byte[] RowVersion, int UnreadMessages = 0);
// Customer result deliberately has no internal-note property.
public sealed record CustomerTicket(TicketSummary Ticket, byte[] RowVersion, IReadOnlyList<TicketMessageRecord> Messages);
public sealed record TicketHistoryRecord(string Kind, string? Detail, int? ActorUserId, DateTimeOffset CreatedAtUtc);
public sealed record OperatorTicket(CustomerTicket Public, IReadOnlyList<TicketNoteRecord> InternalNotes, IReadOnlyList<TicketHistoryRecord> History);
public sealed record TicketFilter(TicketStatus? Status = null, string? Search = null,
    DateTimeOffset? From = null, DateTimeOffset? Until = null, int Page = 1);
public enum TicketWriteResult { Saved, NotFound, Invalid, Conflict }
public sealed record NewTicket(string Subject, TicketType Type, string Body, int? ProductId = null, IReadOnlyList<StoredTicketPhoto>? Photos = null);
public sealed record TicketChange(string? Body, bool InternalNote, TicketStatus? Status, int? AssignTo, byte[] RowVersion, IReadOnlyList<StoredTicketPhoto>? Photos = null);

public interface ITicketStore
{
    Task<Guid?> CreateAsync(TicketActor actor, NewTicket input, CancellationToken ct);
    Task<IReadOnlyList<TicketSummary>> ListAsync(TicketActor actor, TicketFilter filter, CancellationToken ct);
    Task<CustomerTicket?> GetCustomerAsync(int userId, Guid id, CancellationToken ct);
    Task<OperatorTicket?> GetOperatorAsync(Guid id, CancellationToken ct);
    Task<TicketWriteResult> ChangeAsync(TicketActor actor, Guid id, TicketChange change, CancellationToken ct);
    Task<bool> IsPhotoReferencedAsync(Guid photoId, CancellationToken ct);
    Task<TicketPhotoFile?> GetPhotoAsync(TicketActor actor, Guid photoId, CancellationToken ct);
    Task RecordSecurityAsync(int? userId, string kind, CancellationToken ct);
}
