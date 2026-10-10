using SadGallery.Application.Abstractions;
using SadGallery.Application.Media;
using Xunit;

namespace SadGallery.Tests.Unit.Media;

/// <summary>
/// تست‌های تولید و ارزیابیِ نام فایلِ ذخیره‌شده (فاز ۴).
/// </summary>
/// <remarks>
/// هدفِ اصلی: اطمینان از این‌که نامِ ارسالیِ کاربر نمی‌تواند مسیر را دور بزند
/// (Path Traversal) یا فایلِ دیگری را بازنویسی کند.
/// </remarks>
public sealed class MediaFileNamingTests
{
    [Fact]
    public void NewStoredName_IsRandomHexWithAllowedExtension()
    {
        var name = MediaFileNaming.NewStoredName(ImageFormat.WebP);

        Assert.True(MediaFileNaming.IsWellFormed(name));
        Assert.EndsWith(".webp", name, StringComparison.Ordinal);
        Assert.Equal(32 + 5, name.Length);
    }

    [Fact]
    public void NewStoredName_UsesAllSupportedExtensions()
    {
        Assert.EndsWith(".jpg", MediaFileNaming.NewStoredName(ImageFormat.Jpeg), StringComparison.Ordinal);
        Assert.EndsWith(".png", MediaFileNaming.NewStoredName(ImageFormat.Png), StringComparison.Ordinal);
        Assert.EndsWith(".webp", MediaFileNaming.NewStoredName(ImageFormat.WebP), StringComparison.Ordinal);
    }

    [Fact]
    public void NewStoredName_IsUniqueAcrossManyCalls()
    {
        // تصادم در ۱۲۸ بیت آنتروپی عملاً ناممکن است؛ این تست جلوِ پیاده‌سازیِ
        // اشتباه (مثلاً استفاده از زمان یا شمارنده) را می‌گیرد.
        var names = new HashSet<string>();

        for (var i = 0; i < 5_000; i++)
        {
            Assert.True(names.Add(MediaFileNaming.NewStoredName(ImageFormat.WebP)));
        }
    }

    [Fact]
    public void NewStoredName_NeverContainsUserNameOrOriginalName()
    {
        // نام باید هیچ بخشی از نامِ کاربر را نداشته باشد؛ برای همین فقط هگز است.
        var name = MediaFileNaming.NewStoredName(ImageFormat.Jpeg);
        var hexPart = name[..32];

        Assert.All(hexPart, character => Assert.Contains(character, "0123456789abcdef"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("photo.jpg")]
    [InlineData("0123456789abcdef0123456789abcdef")]          // بدون پسوند
    [InlineData("0123456789abcdef0123456789abcdef.exe")]      // پسوندِ غیرمجاز
    [InlineData("0123456789abcdef0123456789abcdef.svg")]      // SVG مجاز نیست
    [InlineData("0123456789abcdef0123456789abcdef.webp.webp")] // بیش از یک نقطه
    [InlineData("../../web.config")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("0123456789abcdef0123456789abcde/.webp")]     // جداکنندهٔ مسیر در بخشِ هگز
    [InlineData("0123456789ABCDEF0123456789ABCDEF.webp")]     // هگزِ بزرگ
    [InlineData("0123456789abcdef0123456789abcdef0.webp")]    // طولِ نادرست
    public void IsWellFormed_RejectsUnsafeOrMalformedNames(string? name)
    {
        Assert.False(MediaFileNaming.IsWellFormed(name));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef.webp")]
    [InlineData("0123456789abcdef0123456789abcdef.jpg")]
    [InlineData("0123456789abcdef0123456789abcdef.png")]
    public void IsWellFormed_AcceptsGeneratedNames(string name)
    {
        Assert.True(MediaFileNaming.IsWellFormed(name));
    }

    [Fact]
    public void ExtensionFor_MatchesSupportedFormats()
    {
        Assert.Equal(".jpg", MediaFileNaming.ExtensionFor(ImageFormat.Jpeg));
        Assert.Equal(".png", MediaFileNaming.ExtensionFor(ImageFormat.Png));
        Assert.Equal(".webp", MediaFileNaming.ExtensionFor(ImageFormat.WebP));
    }

    [Fact]
    public void ExtensionFor_RejectsUnknownFormat()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MediaFileNaming.ExtensionFor((ImageFormat)999));
    }
}
