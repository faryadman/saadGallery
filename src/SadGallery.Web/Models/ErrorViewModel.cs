namespace SadGallery.Web.Models;

/// <summary>
/// مدل نمایش صفحه خطا: فقط اطلاعات بی‌خطر و کاربرپسند.
/// </summary>
public sealed class ErrorViewModel
{
    public required int StatusCode { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public static ErrorViewModel For(int statusCode) => statusCode switch
    {
        StatusCodes.Status404NotFound => new ErrorViewModel
        {
            StatusCode = statusCode,
            Title = "صفحه یافت نشد",
            Message = "نشانی وارد شده وجود ندارد یا حذف شده است. می‌توانید از صفحه اصلی ادامه دهید.",
        },
        StatusCodes.Status403Forbidden => new ErrorViewModel
        {
            StatusCode = statusCode,
            Title = "دسترسی مجاز نیست",
            Message = "برای مشاهده این بخش دسترسی ندارید. در صورت نیاز با پشتیبانی تماس بگیرید.",
        },
        StatusCodes.Status429TooManyRequests => new ErrorViewModel
        {
            StatusCode = statusCode,
            Title = "درخواست‌های زیاد",
            Message = "تعداد درخواست‌های شما بیش از حد مجاز بوده است. چند لحظه بعد دوباره تلاش کنید.",
        },
        _ => new ErrorViewModel
        {
            StatusCode = statusCode,
            Title = "خطای غیرمنتظره",
            Message = "در پردازش درخواست شما مشکلی رخ داد. لطفاً دوباره تلاش کنید؛ اگر ادامه داشت با پشتیبانی تماس بگیرید.",
        },
    };
}
