using System.Security.Cryptography;
using SadGallery.Application.Abstractions;

namespace SadGallery.Application.Media;

/// <summary>
/// تولید و ارزیابی نام فایل‌های ذخیره‌شده.
/// </summary>
/// <remarks>
/// <para>قاعده: نام فایل هرگز از ورودی کاربر گرفته نمی‌شود. نام اصلی فقط برای
/// نمایش در گزارش استفاده می‌شود (و در دیتابیس پاک‌سازی‌شده نگهداری می‌گردد).</para>
/// <para>دلیل: نام کاربر می‌تواند شامل مسیر (<c>..\\..\\web.config</c>)، نویسه‌های
/// کنترلی، یا نام‌های تکراری باشد که فایل دیگران را بازنویسی کند.</para>
/// </remarks>
public static class MediaFileNaming
{
    /// <summary>تعداد نویسه‌های هگزِ نام (۳۲ نویسه = ۱۲۸ بیت آنتروپی).</summary>
    public const int NameLength = 32;

    private static readonly char[] NameAlphabet = "0123456789abcdef".ToCharArray();

    /// <summary>پسوند متناظر با قالب خروجی.</summary>
    public static string ExtensionFor(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => ".jpg",
        ImageFormat.Png => ".png",
        ImageFormat.WebP => ".webp",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image format."),
    };

    /// <summary>
    /// تولید نام جدید با تولیدکنندهٔ اعداد تصادفیِ رمزنگاری (نه <c>Random</c> و نه GUID)؛
    /// نام‌ها نباید قابل حدس یا شمارش باشند (ADR-0014 §۵).
    /// </summary>
    public static string NewStoredName(ImageFormat format)
    {
        Span<byte> buffer = stackalloc byte[NameLength / 2];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant() + ExtensionFor(format);
    }

    /// <summary>
    /// بررسیِ سخت‌گیرانهٔ شکل نام. تنها نام‌هایی پذیرفته می‌شوند که دقیقاً
    /// <c>۳۲ نویسه هگز + یکی از پسوندهای مجاز</c> باشند.
    /// </summary>
    /// <remarks>
    /// این متد سدِ اصلی در برابر «عبور از مسیر» (Path Traversal) است: هر نامی که
    /// شامل جداکنندهٔ مسیر، نقطهٔ اضافی یا نویسهٔ غیرهگز باشد رد می‌شود.
    /// </remarks>
    public static bool IsWellFormed(string? storedFileName)
    {
        if (string.IsNullOrEmpty(storedFileName))
        {
            return false;
        }

        // طول مجاز: ۳۲ نویسهٔ هگز + پسوند (".jpg"/".png" = ۴ نویسه، ".webp" = ۵ نویسه)
        if (storedFileName.Length is not (NameLength + 4 or NameLength + 5))
        {
            return false;
        }

        // تنها نقطهٔ مجاز باید درست پس از بخش هگز باشد. چون بخش هگز فقط شامل
        // نویسه‌های [0-9a-f] است، نقطه‌ای پیش از این جایگاه نمی‌تواند وجود داشته باشد؛
        // بنابراین این شرط هم‌زمان جلوِ نام‌هایی مانند «..\..\web.config» را می‌گیرد.
        if (storedFileName[NameLength] != '.')
        {
            return false;
        }

        for (var i = 0; i < NameLength; i++)
        {
            if (Array.IndexOf(NameAlphabet, storedFileName[i]) < 0)
            {
                return false;
            }
        }

        var extension = storedFileName[NameLength..];

        return extension is ".jpg" or ".png" or ".webp";
    }
}
