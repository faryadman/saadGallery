using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Tickets;
using SadGallery.Domain.Tickets;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;
namespace SadGallery.Infrastructure.Tickets;

public sealed class TicketStore(SadGalleryDbContext db, IClock clock) : ITicketStore
{
    private static TicketSummary Summary(SupportTicket x) => new(x.Id,
        "SG-" + x.Number.ToString("D10", CultureInfo.InvariantCulture), x.Subject, x.Type, x.Status,
        x.CreatedAtUtc, x.LastActivityAtUtc, x.FirstResponseAtUtc, x.ClosedAtUtc, x.AssignedOperatorUserId, x.RelatedProductId);
    private IQueryable<SupportTicket> Visible(TicketActor a) => a.IsOperator ? db.Set<SupportTicket>() :
        db.Set<SupportTicket>().Where(x => x.CustomerUserId == a.UserId);
    private void Event(Guid? id, int? actor, string kind, string? detail = null) => db.Add(new TicketEvent
        { TicketId = id, ActorUserId = actor, Kind = kind, Detail = detail, CreatedAtUtc = clock.UtcNow });
    public async Task<Guid?> CreateAsync(TicketActor actor, NewTicket input, CancellationToken ct)
    {
        if (input.ProductId.HasValue && !await db.Products.AnyAsync(x => x.Id == input.ProductId && x.IsPublished && !x.IsDeleted, ct)) return null;
        var now = clock.UtcNow;
        var ticket = new SupportTicket { Id = Guid.NewGuid(), CustomerUserId = actor.UserId, Subject = input.Subject,
            Type = input.Type, RelatedProductId = input.ProductId, CreatedAtUtc = now, LastActivityAtUtc = now,
            Messages = [new TicketMessage { AuthorUserId = actor.UserId, Body = input.Body, CreatedAtUtc = now }] };
        db.Add(ticket); Event(ticket.Id, actor.UserId, "Ticket.Created");
        await db.SaveChangesAsync(ct); // Ticket + first message + event: one atomic transaction.
        return ticket.Id;
    }
    public async Task<IReadOnlyList<TicketSummary>> ListAsync(TicketActor actor, TicketFilter filter, CancellationToken ct)
    {
        var q = Visible(actor).AsNoTracking();
        if (filter.Status.HasValue) q = q.Where(x => x.Status == filter.Status);
        if (filter.From.HasValue) q = q.Where(x => x.CreatedAtUtc >= filter.From);
        if (filter.Until.HasValue) q = q.Where(x => x.CreatedAtUtc < filter.Until);
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var s = filter.Search;
            var number = s.StartsWith("SG-", StringComparison.OrdinalIgnoreCase) ? s[3..] : s;
            q = long.TryParse(number, out var n) ? q.Where(x => x.Number == n) : q.Where(x => x.Subject.StartsWith(s));
        }
        return (await q.OrderByDescending(x => x.LastActivityAtUtc).ThenBy(x => x.Id)
            .Skip((filter.Page - 1) * 25).Take(25).ToListAsync(ct)).Select(Summary).ToArray();
    }
    private async Task<CustomerTicket?> PublicAsync(IQueryable<SupportTicket> q, Guid id, CancellationToken ct)
    {
        var ticket = await q.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket == null) return null;
        var messages = await db.Set<TicketMessage>().AsNoTracking().Where(x => x.TicketId == id)
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Select(x => new TicketMessageRecord(x.AuthorUserId, x.IsOperator, x.Body, x.CreatedAtUtc)).ToListAsync(ct);
        return new CustomerTicket(Summary(ticket), ticket.RowVersion, messages);
    }
    public Task<CustomerTicket?> GetCustomerAsync(int userId, Guid id, CancellationToken ct) =>
        PublicAsync(db.Set<SupportTicket>().Where(x => x.CustomerUserId == userId), id, ct);
    public async Task<OperatorTicket?> GetOperatorAsync(Guid id, CancellationToken ct)
    {
        var ticket = await PublicAsync(db.Set<SupportTicket>(), id, ct);
        if (ticket == null) return null;
        var notes = await db.Set<TicketInternalNote>().AsNoTracking().Where(x => x.TicketId == id)
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Select(x => new TicketNoteRecord(x.AuthorUserId, x.Body, x.CreatedAtUtc)).ToListAsync(ct);
        var history = await db.Set<TicketEvent>().AsNoTracking().Where(x => x.TicketId == id)
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Select(x => new TicketHistoryRecord(x.Kind, x.Detail, x.ActorUserId, x.CreatedAtUtc)).ToListAsync(ct);
        return new OperatorTicket(ticket, notes, history);
    }
    public async Task<TicketWriteResult> ChangeAsync(TicketActor actor, Guid id, TicketChange change, CancellationToken ct)
    {
        var ticket = await Visible(actor).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket == null) { await RecordSecurityAsync(actor.UserId, "AccessDenied", ct); return TicketWriteResult.NotFound; }
        // A scoped context may have tracked this ticket before another request updated it.
        await db.Entry(ticket).ReloadAsync(ct);
        if (db.Entry(ticket).State == EntityState.Detached) return TicketWriteResult.NotFound;
        if (!ticket.RowVersion.SequenceEqual(change.RowVersion)) return TicketWriteResult.Conflict;
        var target = change.Status ?? ticket.Status;
        if (actor.IsOperator && change.Status.HasValue && !TicketWorkflow.CanTransition(ticket.Status, target)) return TicketWriteResult.Invalid;
        if (actor.IsOperator && change.Body != null && !change.InternalNote && target is not (TicketStatus.AwaitingCustomer or TicketStatus.Answered)) return TicketWriteResult.Invalid;
        if (change.AssignTo.HasValue && !await (from user in db.Users
            join ur in db.UserRoles on user.Id equals ur.UserId join role in db.Roles on ur.RoleId equals role.Id
            where user.Id == change.AssignTo && user.IsActive && (role.Name == RoleNames.Operator || role.Name == RoleNames.Admin)
            select user.Id).AnyAsync(ct)) return TicketWriteResult.Invalid;
        var now = clock.UtcNow;
        if (change.Body != null)
        {
            if (change.InternalNote) db.Add(new TicketInternalNote { TicketId = id, AuthorUserId = actor.UserId, Body = change.Body, CreatedAtUtc = now });
            else
            {
                db.Add(new TicketMessage { TicketId = id, AuthorUserId = actor.UserId, IsOperator = actor.IsOperator, Body = change.Body, CreatedAtUtc = now });
                if (actor.IsOperator) ticket.FirstResponseAtUtc ??= now;
                else target = TicketWorkflow.AfterCustomerReply(ticket.Status);
            }
        }
        if (change.AssignTo.HasValue) ticket.AssignedOperatorUserId = change.AssignTo;
        Event(id, actor.UserId, change.InternalNote ? "Ticket.InternalNoteAdded" : "Ticket.Changed",
            $"Status:{ticket.Status}->{target};Assigned:{ticket.AssignedOperatorUserId}");
        ticket.Status = target; ticket.ClosedAtUtc = target == TicketStatus.Closed ? now : null;
        ticket.LastActivityAtUtc = now;
        // Force a parent UPDATE even when a test clock (or clock resolution) yields the same time.
        // This makes rowversion guard the message/note transaction as well as status changes.
        db.Entry(ticket).Property(x => x.LastActivityAtUtc).IsModified = true;
        db.Entry(ticket).Property(x => x.RowVersion).OriginalValue = change.RowVersion;
        try { await db.SaveChangesAsync(ct); return TicketWriteResult.Saved; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return TicketWriteResult.Conflict; }
    }
    public async Task RecordSecurityAsync(int? userId, string kind, CancellationToken ct)
    {
        Event(null, userId, "SecurityEvent." + kind); await db.SaveChangesAsync(ct);
    }
}
