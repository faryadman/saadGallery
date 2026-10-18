using SadGallery.Application.Tickets;
namespace SadGallery.Web.Models.Tickets;

public sealed record TicketFinishButtonModel(TicketSummary Ticket, bool IsOperator, bool ReturnToList);

public sealed record TicketListViewModel(IReadOnlyList<TicketSummary> Rows, bool IsOperator)
{
    public string Path => IsOperator ? "/operator/tickets" : "/tickets";
}
