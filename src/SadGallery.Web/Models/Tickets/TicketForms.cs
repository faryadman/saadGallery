using System.ComponentModel.DataAnnotations;
using SadGallery.Domain.Tickets;
namespace SadGallery.Web.Models.Tickets;
public sealed class CreateTicketForm
{
    [Required, StringLength(150)] public string Subject { get; set; } = "";
    public TicketType Type { get; set; } = TicketType.Other;
    [Required, StringLength(4000)] public string Body { get; set; } = "";
    public int? ProductId { get; set; }
}
public sealed class ChangeTicketForm
{
    [StringLength(4000)] public string? Body { get; set; }
    public bool InternalNote { get; set; }
    public TicketStatus? Status { get; set; }
    public int? AssignTo { get; set; }
    [Required, StringLength(12, MinimumLength = 12)] public string RowVersion { get; set; } = "";
    public byte[]? Version()
    {
        try { return Convert.FromBase64String(RowVersion); }
        catch (FormatException) { return null; }
    }
}
