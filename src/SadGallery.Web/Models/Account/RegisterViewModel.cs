using System.ComponentModel.DataAnnotations;

namespace SadGallery.Web.Models.Account;

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "شماره موبایل یا ایمیل را وارد کنید.")]
    [StringLength(150, ErrorMessage = "مقدار وارد شده بیش از حد طولانی است.")]
    [Display(Name = "شماره موبایل یا ایمیل")]
    public string Identifier { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "نام نمایشی حداکثر ۱۰۰ کاراکتر است.")]
    [Display(Name = "نام و نام خانوادگی (اختیاری)")]
    public string? DisplayName { get; set; }

    [Required(ErrorMessage = "رمز عبور را وارد کنید.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "رمز عبور باید حداقل ۸ کاراکتر باشد.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "تکرار رمز عبور را وارد کنید.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "رمز عبور و تکرار آن یکسان نیستند.")]
    [Display(Name = "تکرار رمز عبور")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
