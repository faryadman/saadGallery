using SadGallery.Application.Market;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>
/// تست‌های اعتبارسنجی تنظیمات بازار.
/// نکته امنیتی: پیام خطا باید نام متغیر محیطی را بگوید اما **هرگز مقدار اعتبارنامه را نشان ندهد**.
/// </summary>
public sealed class RateOptionsTests
{
    private static RateOptions Valid() => new()
    {
        Provider = RateOptions.ProviderTgn,
        Username = "user",
        Password = "secret-value-not-in-any-message",
        AllowedProviderDomains = ["webservice.tgnsrv.ir"],
        EndpointTemplate = RateOptions.DefaultEndpointTemplate,
    };

    [Fact]
    public void Defaults_AreFailClosed()
    {
        var options = new RateOptions();

        Assert.True(options.IsDisabled);
        Assert.False(options.IsFixtureProvider);
        Assert.False(options.IsTgnProvider);
        Assert.Equal(60, options.FetchIntervalSeconds);
        Assert.Equal(24, options.MaxStaleHours);
        Assert.Equal(RateOptions.AnomalyPolicyManualReview, options.AnomalyPolicy);
    }

    [Fact]
    public void DisabledProvider_ValidatesCleanly_EvenWithoutCredentials()
    {
        var options = new RateOptions { Provider = RateOptions.ProviderDisabled };

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void ValidOptions_HaveNoErrors()
    {
        Assert.Empty(Valid().Validate());
    }

    [Fact]
    public void MissingCredentials_ProduceGuidanceWithoutSecretValues()
    {
        var options = Valid();
        options.Username = string.Empty;

        var errors = options.Validate();

        var error = Assert.Single(errors, item => item.Contains("اعتبارنامه", StringComparison.Ordinal));
        Assert.Contains(RateOptions.UsernameEnvironmentVariable, error, StringComparison.Ordinal);
        Assert.Contains(RateOptions.PasswordEnvironmentVariable, error, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value-not-in-any-message", error, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidSettings_AreReported()
    {
        Assert.NotEmpty(With(options => options.Provider = "Unknown").Validate());
        Assert.NotEmpty(With(options => options.FetchIntervalSeconds = 5).Validate());
        Assert.NotEmpty(With(options => options.StaleThresholdMinutes = 0).Validate());
        Assert.NotEmpty(With(options => options.MaxStaleHours = 0).Validate());
        Assert.NotEmpty(With(options => options.CacheTtlSeconds = 0).Validate());
        Assert.NotEmpty(With(options => options.MaxResponseBytes = 100).Validate());
        Assert.NotEmpty(With(options => options.HttpTimeoutSeconds = 0).Validate());
        Assert.NotEmpty(With(options => options.AnomalyChangeThresholdPercent = 100m).Validate());
        Assert.NotEmpty(With(options => options.AnomalyPolicy = "Whatever").Validate());
        Assert.NotEmpty(With(options => options.MaxBackoffMinutes = 0).Validate());
        Assert.NotEmpty(With(options => options.StartupDelaySeconds = 999).Validate());
        Assert.NotEmpty(With(options => options.LeaseTtlSeconds = -1).Validate());
    }

    private static RateOptions With(Action<RateOptions> change)
    {
        var options = Valid();
        change(options);
        return options;
    }

    [Fact]
    public void InvalidAllowedDomain_Format_IsReported()
    {
        var options = Valid();
        options.AllowedProviderDomains = ["https://webservice.tgnsrv.ir/path"];

        Assert.Contains(options.Validate(), error => error.Contains("دامنه", StringComparison.Ordinal));
    }

    [Fact]
    public void EffectiveLeaseTtl_IsHttpTimeoutPlusThirtySeconds_UnlessConfigured()
    {
        var options = Valid();
        options.HttpTimeoutSeconds = 10;

        Assert.Equal(TimeSpan.FromSeconds(40), options.EffectiveLeaseTtl);

        options.LeaseTtlSeconds = 5;
        Assert.Equal(TimeSpan.FromSeconds(5), options.EffectiveLeaseTtl);
    }

    [Fact]
    public void FixtureProvider_DoesNotRequireCredentials()
    {
        var options = new RateOptions { Provider = RateOptions.ProviderFixture };

        Assert.Empty(options.Validate());
    }
}
