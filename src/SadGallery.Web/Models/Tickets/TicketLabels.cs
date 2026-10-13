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
    public static string Status(TicketStatus s) => s switch
    {
        TicketStatus.New => "جدید", TicketStatus.InReview => "در حال بررسی",
        TicketStatus.AwaitingCustomer => "منتظر پاسخ شما", TicketStatus.Answered => "پاسخ داده‌شده",
        TicketStatus.Closed => "بسته‌شده", _ => "نامشخص",
    };
    public static string Type(TicketType t) => t switch
    {
        TicketType.PriceInquiry => "استعلام قیمت", TicketType.ProductQuestion => "پرسش درباره کالا",
        TicketType.StockAndWeight => "موجودی و وزن", TicketType.OrderFollowUp => "پیگیری سفارش",
        TicketType.AccountSupport => "پشتیبانی حساب", _ => "سایر",
    };
}
