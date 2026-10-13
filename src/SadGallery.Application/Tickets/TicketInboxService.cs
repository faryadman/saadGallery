namespace SadGallery.Application.Tickets;

public sealed record TicketUnreadCounts(int Total, IReadOnlyDictionary<Guid, int> Tickets);

public interface ITicketInboxStore
{
    Task<TicketUnreadCounts> CountsAsync(TicketActor actor, IReadOnlyList<Guid> ticketIds, CancellationToken ct);
    Task MarkReadAsync(TicketActor actor, Guid ticketId, IReadOnlyList<long> messageIds, CancellationToken ct);
}

public sealed class TicketInboxService(ITicketInboxStore store)
{
    public Task<TicketUnreadCounts> CountsAsync(TicketActor actor, IReadOnlyList<Guid> ticketIds, CancellationToken ct)
    {
        if (actor.UserId <= 0 || ticketIds.Count > 25) throw new ArgumentException("درخواست اعلان معتبر نیست.");
        return store.CountsAsync(actor, ticketIds.Distinct().ToArray(), ct);
    }

    public Task MarkReadAsync(TicketActor actor, Guid ticketId, IReadOnlyList<TicketMessageRecord> displayed, CancellationToken ct)
    {
        if (actor.UserId <= 0) throw new ArgumentException("کاربر معتبر نیست.");
        // Acknowledge the exact rendered snapshot, so messages arriving during this request remain unread.
        var ids = displayed.Where(x => x.IsOperator != actor.IsOperator && x.AuthorUserId != actor.UserId && x.Id > 0)
            .Select(x => x.Id).Distinct().ToArray();
        return ids.Length == 0 ? Task.CompletedTask : store.MarkReadAsync(actor, ticketId, ids, ct);
    }
}
