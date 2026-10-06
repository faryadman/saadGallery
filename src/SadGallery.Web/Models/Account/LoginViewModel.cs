using System.ComponentModel.DataAnnotations;

namespace SadGallery.Web.Models.Account;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "شماره موبایل یا ایمیل را وارد کنید.")]
    [StringLength(150, ErrorMessage = "مقدار وارد شده بیش از حد طولانی است.")]
    [Display(Name = "شماره موبایل یا ایمیل")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور را وارد کنید.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "مرا به خاطر بسپار")]
    public bool RememberMe { get; set; }
}
