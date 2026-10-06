using SadGallery.Application.Text;
using Xunit;

namespace SadGallery.Tests.Unit.Text;

/// <summary>
/// ورودی کاربر ممکن است با ارقام فارسی ارسال شود؛ نرمال‌سازی باید درست و بدون تغییر سایر کاراکترها باشد.
/// </summary>
public sealed class PersianTextTests
{
    [Theory]
    [InlineData("۰۹۱۲۳۴۵۶۷۸۹", "09123456789")]
    [InlineData("٠٩١٢٣٤٥٦٧٨٩", "09123456789")]
    [InlineData("0912-345-6789", "0912-345-6789")]
    [InlineData("user@example.com", "user@example.com")]
    public void ToAsciiDigits_NormalizesOnlyDigits(string input, string expected)
    {
        Assert.Equal(expected, PersianText.ToAsciiDigits(input));
    }

    [Fact]
    public void ToPersianDigits_ConvertsLatinDigits()
    {
        Assert.Equal("۱۲۳۴", PersianText.ToPersianDigits("1234"));
    }

    [Fact]
    public void ToAsciiDigits_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PersianText.ToAsciiDigits(null));
        Assert.Equal(string.Empty, PersianText.ToAsciiDigits(string.Empty));
    }

    [Fact]
    public void ToPersianDigits_KeepsNonDigitCharacters()
    {
        Assert.Equal("کد ۱۲۳", PersianText.ToPersianDigits("کد 123"));
    }
}
