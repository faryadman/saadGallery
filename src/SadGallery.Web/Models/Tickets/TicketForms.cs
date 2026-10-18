using System.ComponentModel.DataAnnotations;
using SadGallery.Domain.Tickets;
namespace SadGallery.Web.Models.Tickets;
public sealed class CreateTicketForm
{
    [MaxLength(SadGallery.Application.Tickets.TicketPhotoService.MaxPhotos, ErrorMessage = "در هر پیام حداکثر ۳ عکس انتخاب کنید.")]
    public List<IFormFile> Photos { get; set; } = [];
    [Required(ErrorMessage = "موضوع درخواست را وارد کنید."), StringLength(150, ErrorMessage = "موضوع حداکثر ۱۵۰ نویسه باشد.")] public string Subject { get; set; } = "";
    public TicketType Type { get; set; } = TicketType.Other;
    [Required(ErrorMessage = "متن پیام را وارد کنید."), StringLength(4000, ErrorMessage = "پیام حداکثر ۴۰۰۰ نویسه باشد.")] public string Body { get; set; } = "";
    public int? ProductId { get; set; }
}
public sealed class ChangeTicketForm
{
    [MaxLength(SadGallery.Application.Tickets.TicketPhotoService.MaxPhotos, ErrorMessage = "در هر پیام حداکثر ۳ عکس انتخاب کنید.")]
    public List<IFormFile> Photos { get; set; } = [];
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

public sealed class TicketStatusForm
{
    [Required, EnumDataType(typeof(TicketStatus))]
    public TicketStatus? NextStatus { get; set; }
    [Required, StringLength(12, MinimumLength = 12)]
    public string RowVersion { get; set; } = "";
    public byte[]? Version()
    {
        try { return Convert.FromBase64String(RowVersion); }
        catch (FormatException) { return null; }
    }
}

public sealed class TicketFinishForm
{
    [Required(ErrorMessage = "نسخه تیکت مشخص نیست؛ صفحه را تازه کنید."), StringLength(12, MinimumLength = 12, ErrorMessage = "نسخه تیکت معتبر نیست؛ صفحه را تازه کنید.")]
    public string RowVersion { get; set; } = "";
    public bool ReturnToList { get; set; }
    public byte[]? Version() { try { var bytes = Convert.FromBase64String(RowVersion ?? ""); return bytes.Length == 8 ? bytes : null; } catch (FormatException) { return null; } }
}
