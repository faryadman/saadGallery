using SadGallery.Domain.Tickets;
namespace SadGallery.Web.Models.Tickets;
public static class TicketLabels
{
    public static string Event(string kind) => kind switch
    {
        "Ticket.Created" => "ثبت درخواست",
        "Ticket.InternalNoteAdded" => "افزودن یادداشت داخلی",
        "Ticket.Changed" => "تغییر تیکت",
        _ => "رویداد تیکت",
    };
    public static string Status(TicketStatus s, bool isOperator = false) => s switch
    {
        TicketStatus.New => "جدید", TicketStatus.InReview => "در حال بررسی",
        TicketStatus.AwaitingCustomer => isOperator ? "منتظر پاسخ مشتری" : "منتظر پاسخ شما", TicketStatus.Answered => "پاسخ داده‌شده",
        TicketStatus.Closed => "اتمام‌یافته", _ => "نامشخص",
    };
    public static string StatusBadge(TicketStatus status) => status switch
    {
        TicketStatus.New => "text-bg-primary",
        TicketStatus.InReview => "text-bg-info",
        TicketStatus.AwaitingCustomer => "text-bg-warning",
        TicketStatus.Answered => "text-bg-success",
        _ => "text-bg-secondary",
    };
    public static string Type(TicketType t) => t switch
    {
        TicketType.PriceInquiry => "استعلام قیمت", TicketType.ProductQuestion => "پرسش درباره کالا",
        TicketType.StockAndWeight => "موجودی و وزن", TicketType.OrderFollowUp => "پیگیری سفارش",
        TicketType.AccountSupport => "پشتیبانی حساب", _ => "سایر",
    };
}
