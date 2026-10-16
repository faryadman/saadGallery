using System.ComponentModel.DataAnnotations;
using SadGallery.Application.Settings;

namespace SadGallery.Web.Models;

public sealed class ConnectionNoticeForm
{
    [Required(ErrorMessage = "عنوان قطع اتصال را وارد کنید."), StringLength(ConnectionNoticeService.TitleLimit, ErrorMessage = "عنوان حداکثر ۸۰ نویسه باشد.")]
    public string OfflineTitle { get; set; } = "";
    [Required(ErrorMessage = "توضیح قطع اتصال را وارد کنید."), StringLength(ConnectionNoticeService.DescriptionLimit, ErrorMessage = "توضیح حداکثر ۲۰۰ نویسه باشد.")]
    public string OfflineDescription { get; set; } = "";
    [Required(ErrorMessage = "عنوان بازگشت اتصال را وارد کنید."), StringLength(ConnectionNoticeService.TitleLimit, ErrorMessage = "عنوان حداکثر ۸۰ نویسه باشد.")]
    public string OnlineTitle { get; set; } = "";
    [Required(ErrorMessage = "توضیح بازگشت اتصال را وارد کنید."), StringLength(ConnectionNoticeService.DescriptionLimit, ErrorMessage = "توضیح حداکثر ۲۰۰ نویسه باشد.")]
    public string OnlineDescription { get; set; } = "";
    [Required(ErrorMessage = "نسخه تنظیمات مشخص نیست؛ صفحه را تازه کنید."), StringLength(12, MinimumLength = 12, ErrorMessage = "نسخه تنظیمات معتبر نیست؛ صفحه را تازه کنید.")]
    public string RowVersion { get; set; } = "";
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
    public bool Available { get; set; }
    public ConnectionNoticeText Text() => new(OfflineTitle, OfflineDescription, OnlineTitle, OnlineDescription);
    public byte[]? Version() { try { return Convert.FromBase64String(RowVersion); } catch (FormatException) { return null; } }
    public static ConnectionNoticeForm From(ConnectionNoticeSettings? settings)
    {
        var text = settings?.Text ?? ConnectionNoticeText.Default;
        return new() { OfflineTitle = text.OfflineTitle, OfflineDescription = text.OfflineDescription,
            OnlineTitle = text.OnlineTitle, OnlineDescription = text.OnlineDescription,
            RowVersion = settings == null ? "" : Convert.ToBase64String(settings.Version), Available = settings != null,
            UpdatedAtUtc = settings?.UpdatedAtUtc, UpdatedByUserId = settings?.UpdatedByUserId };
    }
}
