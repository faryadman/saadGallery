using Microsoft.AspNetCore.Identity;

namespace SadGallery.Web.Identity;

/// <summary>
/// ترجمه خطاهای Identity به پیام فارسی کاربرپسند.
/// قاعده امنیتی: پیام‌ها نباید وجود/عدم وجود یک حساب را افشا کنند؛ پس متن‌ها کلی و یکنواخت هستند.
/// </summary>
public static class IdentityErrorTranslator
{
    private static readonly Dictionary<string, string> Messages = new(StringComparer.Ordinal)
    {
        ["DuplicateUserName"] = "این شناسه قبلاً ثبت شده است.",
        ["DuplicateEmail"] = "این ایمیل قبلاً ثبت شده است.",
        ["PasswordTooShort"] = "رمز عبور باید حداقل ۸ کاراکتر باشد.",
        ["PasswordRequiresDigit"] = "رمز عبور باید حداقل یک رقم داشته باشد.",
        ["PasswordMismatch"] = "رمز عبور نادرست است.",
        ["InvalidUserName"] = "شناسه وارد شده معتبر نیست.",
        ["InvalidEmail"] = "ایمیل وارد شده معتبر نیست.",
        ["UserAlreadyHasPassword"] = "برای این حساب قبلاً رمز عبور تعیین شده است.",
        ["ConcurrencyFailure"] = "اطلاعات حساب در جای دیگری تغییر کرده است؛ دوباره تلاش کنید.",
    };

    public static string Translate(IdentityError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return Messages.TryGetValue(error.Code, out var message)
            ? message
            : "ثبت‌نام انجام نشد. لطفاً اطلاعات را بررسی و دوباره تلاش کنید.";
    }
}
