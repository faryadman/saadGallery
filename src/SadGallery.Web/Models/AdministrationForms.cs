using System.ComponentModel.DataAnnotations;
using SadGallery.Application.Administration;

namespace SadGallery.Web.Models;

public sealed class UserAccessForm
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    [Required(ErrorMessage = "نسخه کاربر مشخص نیست؛ صفحه را تازه کنید."), StringLength(64, ErrorMessage = "نسخه کاربر معتبر نیست.")] public string Version { get; set; } = "";
    [Required(ErrorMessage = "نقش کاربر را انتخاب کنید."), RegularExpression("^(Customer|Operator|Admin)$", ErrorMessage = "نقش انتخاب‌شده معتبر نیست.")] public string Role { get; set; } = "Customer";
    public bool IsActive { get; set; }
    public static UserAccessForm From(ManagedUser u) => new() { Id = u.Id, DisplayName = u.DisplayName, Version = u.Version, Role = u.Role, IsActive = u.IsActive };
}
public sealed class RateSettingsForm
{
    public bool RatesPaused { get; set; }
    [Range(10, 3600, ErrorMessage = "فاصله دریافت باید بین ۱۰ تا ۳۶۰۰ ثانیه و حداقل برابر فاصله مجاز سرور باشد.")]
    public int IntervalSeconds { get; set; }
    [Required(ErrorMessage = "نسخه تنظیمات مشخص نیست؛ صفحه را تازه کنید."), StringLength(12, ErrorMessage = "نسخه تنظیمات معتبر نیست.")] public string RowVersion { get; set; } = "";
    public byte[]? Version() { try { return Convert.FromBase64String(RowVersion ?? ""); } catch (FormatException) { return null; } }
    public static RateSettingsForm From(RuntimeRateSettings s) => new() { RatesPaused = s.Paused, IntervalSeconds = s.IntervalSeconds, RowVersion = Convert.ToBase64String(s.Version) };
}
