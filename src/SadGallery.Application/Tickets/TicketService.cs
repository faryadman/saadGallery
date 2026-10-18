using SadGallery.Domain.Tickets;
namespace SadGallery.Application.Tickets;

public sealed class TicketService(ITicketStore store)
{
    public static bool ValidText(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t');

    public Task<Guid?> CreateAsync(TicketActor actor, NewTicket input, CancellationToken ct)
    {
        if (actor.UserId <= 0 || !ValidText(input.Subject, 150) || !ValidText(input.Body, 4000) ||
            !Enum.IsDefined(input.Type) || input.ProductId is <= 0 || input.Photos?.Count > TicketPhotoService.MaxPhotos)
            return Task.FromResult<Guid?>(null);
        return store.CreateAsync(actor, input with { Subject = input.Subject.Trim(), Body = input.Body.Trim() }, ct);
    }
    public Task<IReadOnlyList<TicketSummary>> ListAsync(TicketActor actor, TicketFilter filter, CancellationToken ct)
    {
        if (actor.UserId <= 0 || filter.Page < 1 || filter.Page > 10000 || filter.Search?.Length > 150 ||
            (filter.Status.HasValue && !Enum.IsDefined(filter.Status.Value)) || filter.From > filter.Until)
            throw new ArgumentException("فیلتر جست‌وجو معتبر نیست.", nameof(filter));
        return store.ListAsync(actor, filter with { Search = filter.Search?.Trim() }, ct);
    }
    public Task<CustomerTicket?> GetCustomerAsync(int userId, Guid id, CancellationToken ct) => store.GetCustomerAsync(userId, id, ct);
    public Task<OperatorTicket?> GetOperatorAsync(Guid id, CancellationToken ct) => store.GetOperatorAsync(id, ct);
    public Task<TicketWriteResult> FinishAsync(TicketActor actor, Guid id, byte[] rowVersion, CancellationToken ct) =>
        actor.UserId <= 0 || rowVersion.Length != 8 ? Task.FromResult(TicketWriteResult.Invalid) : store.FinishAsync(actor, id, rowVersion, ct);
    public Task<TicketWriteResult> ChangeAsync(TicketActor actor, Guid id, TicketChange change, CancellationToken ct)
    {
        if (actor.UserId <= 0 || change.RowVersion.Length != 8 || change.Photos?.Count > TicketPhotoService.MaxPhotos ||
            (change.Photos is { Count: > 0 } && (change.InternalNote || change.Body == null)) ||
            (change.Body != null && !ValidText(change.Body, change.InternalNote ? 2000 : 4000)) ||
            (change.Status.HasValue && !Enum.IsDefined(change.Status.Value)) || change.AssignTo is <= 0 ||
            (change.Body == null && change.Status == null && change.AssignTo == null) ||
            (!actor.IsOperator && (change.InternalNote || change.Status != null || change.AssignTo != null)))
            return Task.FromResult(TicketWriteResult.Invalid);
        return store.ChangeAsync(actor, id, change with { Body = change.Body?.Trim() }, ct);
    }
}
