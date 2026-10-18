namespace SadGallery.Domain.Tickets;

public enum TicketStatus { New, InReview, AwaitingCustomer, Answered, Closed }
public enum TicketType { PriceInquiry, ProductQuestion, StockAndWeight, OrderFollowUp, AccountSupport, Other }

public static class TicketWorkflow
{
    public static bool CanFinish(TicketStatus status) => Enum.IsDefined(status) && status != TicketStatus.Closed;
    public static bool CanTransition(TicketStatus from, TicketStatus to) => (from, to) switch
    {
        (TicketStatus.New, TicketStatus.InReview) => true,
        (TicketStatus.InReview, TicketStatus.AwaitingCustomer or TicketStatus.Answered) => true,
        (TicketStatus.AwaitingCustomer, TicketStatus.InReview or TicketStatus.Closed) => true,
        (TicketStatus.Answered, TicketStatus.InReview or TicketStatus.Closed) => true,
        (TicketStatus.Closed, TicketStatus.InReview) => true,
        _ => false,
    };
    public static TicketStatus AfterCustomerReply(TicketStatus status) => status switch
    {
        TicketStatus.AwaitingCustomer or TicketStatus.Answered or TicketStatus.Closed => TicketStatus.InReview,
        _ => status,
    };
}
