using SadGallery.Application.Market;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>
/// تست‌های امنیتی ساخت آدرس منبع نرخ. اعتبارنامه در مسیر آدرس قرار می‌گیرد؛
/// پس هم‌زمان باید (۱) درست ساخته شود و (۲) در لاگ/گزارش هرگز دیده نشود.
/// </summary>
public sealed class TgnEndpointTemplateTests
{
    private const string Secret = "s3cr3t/with?chars";

    private static RateOptions Options() => new()
    {
        Provider = RateOptions.ProviderTgn,
        Username = "my-user",
        Password = Secret,
        AllowedProviderDomains = ["webservice.tgnsrv.ir"],
        EndpointTemplate = RateOptions.DefaultEndpointTemplate,
    };

    [Fact]
    public void Build_ReturnsHttpsUriWithEscapedCredentials()
    {
        var uri = TgnEndpointTemplate.Build(Options());

        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
        Assert.Equal("webservice.tgnsrv.ir", uri.Host);
        Assert.Contains("my-user", uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("with?chars", uri.AbsoluteUri, StringComparison.Ordinal); // ? باید encode شود
    }

    [Fact]
    public void Describe_NeverContainsCredentials()
    {
        var description = TgnEndpointTemplate.Describe(Options());

        Assert.DoesNotContain(Secret, description, StringComparison.Ordinal);
        Assert.DoesNotContain("my-user", description, StringComparison.Ordinal);
        Assert.Contains("***", description, StringComparison.Ordinal);
        Assert.Contains("webservice.tgnsrv.ir", description, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_OfRealUri_IsNotUsed_SoNothingCanLeakByAccident()
    {
        // سند: توصیف امن فقط از قالب ساخته می‌شود، نه از آدرس واقعی.
        var uri = TgnEndpointTemplate.Build(Options());

        // مقدار خام در آدرس نیست (encode می‌شود) اما رمز بخشی از مسیر است؛ توصیف امن هم آن را ندارد.
        Assert.Contains("s3cr3t%2Fwith%3Fchars", uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", TgnEndpointTemplate.Describe(Options()), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ValidOptions_ReturnsNull()
    {
        Assert.Null(TgnEndpointTemplate.Validate(Options()));
    }

    [Theory]
    [InlineData("http://webservice.tgnsrv.ir/Pr/Get/{username}/{password}")]   // بدون TLS
    [InlineData("https://webservice.tgnsrv.ir/Pr/Get/user/pass")]              // بدون جای‌نگهدار
    [InlineData("not-a-url")]                                                  // نامعتبر
    [InlineData("")]                                                           // خالی
    public void Validate_InvalidTemplate_ReturnsError(string template)
    {
        var options = Options();
        options.EndpointTemplate = template;

        Assert.NotNull(TgnEndpointTemplate.Validate(options));
    }

    [Fact]
    public void Validate_DomainNotInAllowList_IsRejected()
    {
        var options = Options();
        options.AllowedProviderDomains = ["another-host.invalid"];

        var error = TgnEndpointTemplate.Validate(options);

        Assert.NotNull(error);
        Assert.Contains("AllowedProviderDomains", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EmptyAllowList_IsRejected()
    {
        var options = Options();
        options.AllowedProviderDomains = [];

        Assert.NotNull(TgnEndpointTemplate.Validate(options));
    }

    [Fact]
    public void Build_InvalidOptions_ThrowsInsteadOfCallingOut()
    {
        var options = Options();
        options.AllowedProviderDomains = [];

        Assert.Throws<InvalidOperationException>(() => TgnEndpointTemplate.Build(options));
    }
}
