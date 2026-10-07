using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>
/// تست‌های نرمال‌سازی: مقیاس منبع، واحد، بازه معقول، تازگی و سیاست جهش.
/// این تست‌ها «تبدیل‌های واحد» را قفل می‌کنند — حساس‌ترین بخش مالی فاز ۲.
/// </summary>
public sealed class RateNormalizerTests
{
    private static readonly DateTimeOffset QuotedAt = new(2022, 6, 9, 7, 44, 48, TimeSpan.Zero);

    private static RateOptions CreateOptions(string anomalyPolicy = RateOptions.AnomalyPolicyManualReview) => new()
    {
        Provider = RateOptions.ProviderTgn,
        FetchIntervalSeconds = 60,
        StaleThresholdMinutes = 15,
        MaxStaleHours = 24,
        AnomalyChangeThresholdPercent = 50m,
        AnomalyPolicy = anomalyPolicy,
    };

    private static RateNormalizer CreateNormalizer(RateOptions options) => new(
        RateFreshnessPolicy.FromOptions(options),
        new RateAnomalyDetector(options.AnomalyChangeThresholdPercent),
        options);

    private static RateParseResult ParseSample() => TgnResponseParser.Parse(
        "{\"YekGram18\":1442800,\"SekehEmam\":14500,\"OunceNoghreh\":21940,\"TimeRead\":\"2022/06/09 11:14:48\"}");

    /// <summary>زمان دریافت کمی بعد از زمان اعلام (وضعیت Live).</summary>
    private static DateTimeOffset LiveNow() => QuotedAt + TimeSpan.FromSeconds(30);

    [Fact]
    public void Normalize_CoinScale_IsAppliedExactlyOnce()
    {
        var result = CreateNormalizer(CreateOptions()).Normalize(ParseSample(), "Tgn", LiveNow(), LiveNow());

        var coin = Assert.Single(result.Accepted, rate => rate.AssetCode == "COIN_EMAMI");

        // مستندات منبع: قیمت سکه‌ها با حذف سه صفر اعلام می‌شود ⇒ ۱۴۵۰۰ ⇒ ۱۴٫۵۰۰٫۰۰۰ تومان
        Assert.Equal(14_500_000m, coin.Amount);
        Assert.Equal(1_000m, coin.ScaleApplied);
        Assert.Equal(14500m, coin.ProviderRawValue);
        Assert.Equal(CurrencyUnit.Irt, coin.QuoteUnit);
    }

    [Fact]
    public void Normalize_SilverOunceScale_DividesByThousand()
    {
        var result = CreateNormalizer(CreateOptions()).Normalize(ParseSample(), "Tgn", LiveNow(), LiveNow());

        var silver = Assert.Single(result.Accepted, rate => rate.AssetCode == "SILVER_OUNCE_USD");

        // مستندات منبع: «قیمت انس نقره در هزار ضرب شده است» ⇒ ۲۱۹۴۰ ⇒ ۲۱٫۹۴ دلار
        Assert.Equal(21.94m, silver.Amount);
        Assert.Equal(CurrencyUnit.Usd, silver.QuoteUnit);
    }

    [Fact]
    public void Normalize_GoldGram_PassesThroughInToman()
    {
        var result = CreateNormalizer(CreateOptions()).Normalize(ParseSample(), "Tgn", LiveNow(), LiveNow());

        var gram = Assert.Single(result.Accepted, rate => rate.AssetCode == "GOLD_GRAM_18");

        Assert.Equal(1_442_800m, gram.Amount);
        Assert.Equal(CurrencyUnit.Irt, gram.QuoteUnit);
    }

    [Fact]
    public void Normalize_AcceptedRates_SatisfyAllRequiredInvariants()
    {
        // معیار پذیرش فاز ۲: هیچ نرخی بدون واحد/زمان/منبع/وضعیت اعتبار پذیرفته نمی‌شود.
        var result = CreateNormalizer(CreateOptions()).Normalize(ParseSample(), "Tgn", LiveNow(), LiveNow());

        Assert.NotEmpty(result.Accepted);

        foreach (var rate in result.Accepted)
        {
            Assert.Empty(NormalizedRate.FindInvariantViolations(rate));
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1000")]
    public void Normalize_NonPositiveValue_IsRejected(string rawValue)
    {
        var parse = TgnResponseParser.Parse(
            $"{{\"Dollar\":{rawValue},\"TimeRead\":\"2022/06/09 11:14:48\"}}");

        var result = CreateNormalizer(CreateOptions()).Normalize(parse, "Tgn", LiveNow(), LiveNow());

        Assert.Empty(result.Accepted);
        Assert.Contains(result.Rejected, rejection => rejection.Reason == RateRejectionReason.NonPositive);
    }

    [Fact]
    public void Normalize_ValueTenTimesBelowRange_IsFlaggedAsUnitMismatch()
    {
        // مثقال ۱۸ با مقدار ۶۶٫۴۸۹ (به‌جای ۶۶۴٫۸۹۰): ده برابر کوچک‌تر از بازه معقول است،
        // اما ضرب در ۱۰ داخل بازه می‌افتد ⇒ نشانه قوی خطای واحد/مقیاس، نه مقدار واقعی.
        var parse = TgnResponseParser.Parse(
            "{\"YekMesghal18\":66489,\"TimeRead\":\"2022/06/09 11:14:48\"}");

        var result = CreateNormalizer(CreateOptions()).Normalize(parse, "Tgn", LiveNow(), LiveNow());

        Assert.Empty(result.Accepted);
        Assert.Contains(result.Rejected, rejection =>
            rejection.Reason == RateRejectionReason.UnitMismatchSuspected && rejection.AssetCode == "GOLD_MESGHAL_18");
    }

    [Fact]
    public void Normalize_TenfoldValueWithBaseline_IsCaughtByAnomalyGuard()
    {
        // خطای «ریال به‌جای تومان» روی دارایی‌هایی که بازه معقول‌شان گسترده است،
        // با نگهبان جهش (مقایسه با آخرین مقدار ثبت‌شده) گرفته می‌شود ⇒ منتشر نمی‌شود.
        var parse = TgnResponseParser.Parse(
            "{\"YekGram18\":14428000,\"TimeRead\":\"2022/06/09 11:14:48\"}");

        var previous = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["GOLD_GRAM_18"] = 1_442_800m,
        };

        var result = CreateNormalizer(CreateOptions()).Normalize(parse, "Tgn", LiveNow(), LiveNow(), previous);

        Assert.Empty(result.Accepted);
        var flagged = Assert.Single(result.Flagged, rate => rate.AssetCode == "GOLD_GRAM_18");
        Assert.True(flagged.IsAnomalySuspected);
    }

    [Fact]
    public void Normalize_AbsurdlySmallValue_IsRejectedAsOutOfRange()
    {
        var parse = TgnResponseParser.Parse(
            "{\"YekGram18\":99,\"TimeRead\":\"2022/06/09 11:14:48\"}");

        var result = CreateNormalizer(CreateOptions()).Normalize(parse, "Tgn", LiveNow(), LiveNow());

        Assert.Empty(result.Accepted);
        Assert.Contains(result.Rejected, rejection =>
            rejection.Reason == RateRejectionReason.OutOfPlausibleRange);
    }

    [Fact]
    public void Normalize_AnomalousJump_WithManualReview_IsNotPublished()
    {
        var previous = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["GOLD_GRAM_18"] = 800_000m, // جهش ۸۰٪ نسبت به قبلی (آستانه ۵۰٪)
        };

        var result = CreateNormalizer(CreateOptions()).Normalize(ParseSample(), "Tgn", LiveNow(), LiveNow(), previous);

        Assert.DoesNotContain(result.Accepted, rate => rate.AssetCode == "GOLD_GRAM_18");

        var flagged = Assert.Single(result.Flagged, rate => rate.AssetCode == "GOLD_GRAM_18");
        Assert.True(flagged.IsAnomalySuspected);
    }

    [Fact]
    public void Normalize_AnomalousJump_WithAutoAccept_IsPublishedAndMarked()
    {
        var previous = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["GOLD_GRAM_18"] = 800_000m,
        };

        var result = CreateNormalizer(CreateOptions(RateOptions.AnomalyPolicyAutoAccept))
            .Normalize(ParseSample(), "Tgn", LiveNow(), LiveNow(), previous);

        var accepted = Assert.Single(result.Accepted, rate => rate.AssetCode == "GOLD_GRAM_18");
        Assert.True(accepted.IsAnomalySuspected);
    }

    [Fact]
    public void Normalize_FirstEverRate_IsNeverTreatedAsAnomaly()
    {
        var empty = new Dictionary<string, decimal>(StringComparer.Ordinal);

        var result = CreateNormalizer(CreateOptions()).Normalize(ParseSample(), "Tgn", LiveNow(), LiveNow(), empty);

        Assert.Empty(result.Flagged);
        Assert.Equal(3, result.Accepted.Count);
    }

    [Fact]
    public void Normalize_VeryOldQuote_IsStoredAsFlaggedNotPublished()
    {
        var parse = ParseSample();
        var now = QuotedAt + TimeSpan.FromDays(3); // فراتر از MaxStaleHours = 24

        var result = CreateNormalizer(CreateOptions()).Normalize(parse, "Tgn", now, now);

        Assert.Empty(result.Accepted);
        Assert.Equal(3, result.Flagged.Count);
        Assert.All(result.Flagged, rate => Assert.Equal(RateQuality.Invalid, rate.Quality));
    }

    [Fact]
    public void Normalize_StaleButWithinMaxAge_IsPublishedWithStaleQuality()
    {
        var now = QuotedAt + TimeSpan.FromHours(2); // کهنه اما زیر ۲۴ ساعت

        var result = CreateNormalizer(CreateOptions()).Normalize(ParseSample(), "Tgn", now, now);

        Assert.Equal(3, result.Accepted.Count);
        Assert.All(result.Accepted, rate => Assert.Equal(RateQuality.Stale, rate.Quality));
    }

    [Fact]
    public void Normalize_ParseProblems_AreCarriedIntoRejections()
    {
        var parse = TgnResponseParser.Parse(
            "{\"YekGram18\":null,\"TimeRead\":\"2022/06/09 11:14:48\"}");

        var result = CreateNormalizer(CreateOptions()).Normalize(parse, "Tgn", LiveNow(), LiveNow());

        Assert.Contains(result.Rejected, rejection =>
            rejection.ProviderKey == "YekGram18" && rejection.Reason == RateRejectionReason.NullValue);

        // کلیدهای غایب «خطا» نیستند (بند ۶ مستندات سرویس): فقط در MissingKeys ثبت می‌شوند.
        Assert.Equal(14, parse.MissingKeys.Count);
        Assert.DoesNotContain(result.Rejected, rejection => rejection.ProviderKey == "Dollar");
    }

    [Fact]
    public void Normalize_WithoutQuotedTime_YieldsNothing()
    {
        var parse = RateParseResult.Fatal("MissingTime");

        var result = CreateNormalizer(CreateOptions()).Normalize(parse, "Tgn", LiveNow(), LiveNow());

        Assert.Empty(result.Accepted);
        Assert.Empty(result.Flagged);
    }

    [Fact]
    public void FindInvariantViolations_DetectsMissingPieces()
    {
        var broken = new NormalizedRate
        {
            AssetCode = string.Empty,
            Title = string.Empty,
            Amount = 0m,
            QuoteUnit = CurrencyUnit.Irt,
            ProviderId = string.Empty,
            QuotedAtUtc = default,
            FetchedAtUtc = default,
            Quality = RateQuality.Live,
        };

        var violations = NormalizedRate.FindInvariantViolations(broken);

        Assert.Contains(violations, item => item.Contains("AssetCode", StringComparison.Ordinal));
        Assert.Contains(violations, item => item.Contains("ProviderId", StringComparison.Ordinal));
        Assert.Contains(violations, item => item.Contains("QuotedAtUtc", StringComparison.Ordinal));
        Assert.Contains(violations, item => item.Contains("FetchedAtUtc", StringComparison.Ordinal));
        Assert.Contains(violations, item => item.Contains("مثبت", StringComparison.Ordinal));
    }
}
