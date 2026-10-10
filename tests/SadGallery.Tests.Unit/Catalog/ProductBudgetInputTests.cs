using SadGallery.Application.Catalog;
using Xunit;

namespace SadGallery.Tests.Unit.Catalog;

/// <summary>ورودی بودجه، بدون تبدیل واحدِ پنهان.</summary>
public sealed class ProductBudgetInputTests
{
    [Theory]
    [InlineData("۲۵٬۰۰۰٬۰۰۰", 25000000d)]
    [InlineData("٢٥٬٠٠٠٬٠٠٠", 25000000d)]
    [InlineData("25,000,000", 25000000d)]
    [InlineData(" ۱۲۳٫۵ ", 123.5d)]
    [InlineData("1 250 000", 1250000d)]
    public void TryParseToman_AcceptsPersianArabicDigitsAndGrouping(string input, double expected)
    {
        Assert.True(ProductBudgetInput.TryParseToman(input, out var amount));
        Assert.Equal((decimal)expected, amount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("۰")]
    [InlineData("-1000")]
    [InlineData("قیمت نامشخص")]
    [InlineData("1.2.3")]
    [InlineData("1000000000000000000")]
    public void TryParseToman_RejectsEmptyInvalidNonPositiveOrOutOfRange(string? input)
    {
        Assert.False(ProductBudgetInput.TryParseToman(input, out var amount));
        Assert.Equal(0m, amount);
    }
}
