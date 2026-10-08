using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SadGallery.Tests.Integration.Endpoints;

/// <summary>
/// پاسبان رگرسیون دسترس‌پذیری (معیار فاز ۳):
///  ۱) نسبت کنتراست WCAG 2.1 برای جفت‌های واقعی رنگ در site.css ≥ ۴٫۵:۱
///  ۲) وجود قاعده هدف لمسی ۴۴px و نوار حالت آفلاین
/// نتیجه اجرای ابزار (scripts/accessibility-check.py) در docs/ACCESSIBILITY.md ثبت شده است.
/// </summary>
public sealed class AccessibilityTests
{
    private static readonly string CssPath = Path.Combine(FindRepositoryRoot(), "src/SadGallery.Web/wwwroot/css/site.css");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SadGallery.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private static string Css => File.ReadAllText(CssPath);

    private static string Variable(string name)
    {
        if (name.StartsWith('#'))
        {
            return name.ToLowerInvariant();
        }

        var match = Regex.Match(Css, $"--sg-{name}\\s*:\\s*(#[0-9a-fA-F]{{6}})", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"متغیر --sg-{name} در site.css پیدا نشد.");
        return match.Groups[1].Value.ToLowerInvariant();
    }

    private static double Luminance(string hex)
    {
        var value = hex.TrimStart('#');
        double Channel(int index)
        {
            var c = int.Parse(value.Substring(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(0)) + (0.7152 * Channel(2)) + (0.0722 * Channel(4));
    }

    private static double Contrast(string foreground, string background)
    {
        var (a, b) = (Luminance(foreground), Luminance(background));
        var (high, low) = (Math.Max(a, b), Math.Min(a, b));
        return (high + 0.05) / (low + 0.05);
    }

    [Theory]
    [InlineData("ink", "cream", 4.5)]
    [InlineData("muted", "#ffffff", 4.5)]
    [InlineData("muted", "cream", 4.5)]
    [InlineData("navy", "gold", 4.5)]
    [InlineData("gold", "navy", 4.5)]
    [InlineData("navy", "gold-dark", 4.5)]
    public void TextContrast_MeetsWcagAa(string foreground, string background, double minimum)
    {
        var ratio = Contrast(Variable(foreground), Variable(background));

        Assert.True(ratio >= minimum,
            $"کنتراست {foreground} روی {background} برابر {ratio:F2}:1 است و از حد AA ({minimum}) کمتر است.");
    }

    [Fact]
    public void TouchTargetRule_AndOfflineBanner_Exist()
    {
        Assert.Contains(".sg-touch", Css, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px", Css, StringComparison.Ordinal);
        Assert.Contains("min-width: 44px", Css, StringComparison.Ordinal);
        Assert.Contains(".sg-offline-banner", Css, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessibilityDocument_RecordsRealMeasurement()
    {
        var documentPath = Path.Combine(FindRepositoryRoot(), "docs/ACCESSIBILITY.md");

        Assert.True(File.Exists(documentPath), "docs/ACCESSIBILITY.md ثبت نشده است.");

        var document = File.ReadAllText(documentPath);
        Assert.Contains("accessibility-check.py", document, StringComparison.Ordinal);
        Assert.Contains("WCAG", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MainPages_UseTouchTargetClass_OnPrimaryControls()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new Infrastructure.SadGalleryWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(new Uri("/market/bubble", UriKind.Relative), cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Contains("sg-touch", html, StringComparison.Ordinal);
        Assert.Contains("aria-", html, StringComparison.Ordinal); // برچسب‌های دسترس‌پذیری در فرم
    }
}
