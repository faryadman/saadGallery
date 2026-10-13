using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Tickets;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;

namespace SadGallery.Infrastructure.Tickets;

public sealed class TicketInboxStore(SadGalleryDbContext db, IClock clock) : ITicketInboxStore
{
    internal static IQueryable<TicketMessage> Unread(SadGalleryDbContext db, TicketActor actor) =>
        from message in db.Set<TicketMessage>().AsNoTracking()
        join ticket in db.Set<SupportTicket>() on message.TicketId equals ticket.Id
        where (actor.IsOperator || ticket.CustomerUserId == actor.UserId) && message.IsOperator != actor.IsOperator
            && message.AuthorUserId != actor.UserId
            && !db.Set<TicketMessageRead>().Any(r => r.MessageId == message.Id && r.UserId == actor.UserId && r.OperatorView == actor.IsOperator)
        select message;

    public async Task<TicketUnreadCounts> CountsAsync(TicketActor actor, IReadOnlyList<Guid> ticketIds, CancellationToken ct)
    {
        var q = Unread(db, actor);
        var total = await q.CountAsync(ct);
        var rows = ticketIds.Count == 0 ? new Dictionary<Guid, int>() : await q.Where(x => ticketIds.Contains(x.TicketId))
            .GroupBy(x => x.TicketId).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return new(total, rows);
    }

    public async Task MarkReadAsync(TicketActor actor, Guid ticketId, IReadOnlyList<long> messageIds, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(messageIds);
        // Parameterized, idempotent insert. Range locks protect the unique key during concurrent opens.
        // Recheck ownership, direction and authorship in SQL rather than trusting the caller's snapshot.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO TicketMessageReads (UserId, OperatorView, MessageId, ReadAtUtc)
            SELECT {actor.UserId}, {actor.IsOperator}, m.Id, {clock.UtcNow}
            FROM TicketMessages m JOIN SupportTickets t ON t.Id = m.TicketId
            WHERE m.TicketId = {ticketId} AND ({actor.IsOperator} = 1 OR t.CustomerUserId = {actor.UserId})
                AND m.IsOperator <> {actor.IsOperator} AND m.AuthorUserId <> {actor.UserId}
                AND m.Id IN (SELECT CAST([value] AS bigint) FROM OPENJSON({json}))
                AND NOT EXISTS (SELECT 1 FROM TicketMessageReads r WITH (UPDLOCK, HOLDLOCK)
                    WHERE r.UserId = {actor.UserId} AND r.OperatorView = {actor.IsOperator} AND r.MessageId = m.Id)
            """, ct);
    }
}
