using SadGallery.Application.Text;
using Xunit;

namespace SadGallery.Tests.Unit.Text;

/// <summary>خواندن عدد از ورودی فارسی فرم‌ها (قواعد در ADR-0013/TESTING).</summary>
public sealed class PersianNumberTests
{
    [Theory]
    [InlineData("1234", 1234)]
    [InlineData("۱۲۳۴", 1234)]
    [InlineData("١٢٣٤", 1234)]
    [InlineData("۸٫۵", 8.5)]
    [InlineData("۸.۵", 8.5)]
    [InlineData("1٬250٬000", 1250000)]
    [InlineData("1,250,000", 1250000)]
    [InlineData(" 1 250 000 ", 1250000)]
    [InlineData("-۲٬۵۰۰", -2500)]
    [InlineData("+۴۲", 42)]
    public void Parses_CommonPersianInputs(string input, double expected)
    {
        Assert.True(PersianNumber.TryParse(input, out var value), $"«{input}» باید خوانده شود");
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("۱۲abc")]
    [InlineData("۱٫۲٫۳")]
    [InlineData("7.600.000")]
    [InlineData("-")]
    public void Rejects_AmbiguousOrInvalidInput(string? input)
    {
        Assert.False(PersianNumber.TryParse(input, out _));
    }

    [Fact]
    public void Error_Message_IsPersian_AndValueZero_OnFailure()
    {
        var ok = PersianNumber.TryParse("هزار", out var value, out var error);

        Assert.False(ok);
        Assert.Equal(0m, value);
        Assert.Contains("عدد", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_HasNoError()
    {
        Assert.True(PersianNumber.TryParse("۲۵۰", out var value, out var error));
        Assert.Null(error);
        Assert.Equal(250m, value);
    }
}
