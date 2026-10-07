using SadGallery.Application.Market;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>
/// تست‌های تجزیه پاسخ منبع نرخ. معیار پذیرش فاز ۲: نگاشت با نمونه واقعی مالک
/// و دست‌کم ۶ تست روی حالت‌های مرزی/Fuzz (JSON ناقص، null، عدد متنی، واحد ریال/تومان، تاریخ بدون Timezone).
/// </summary>
public sealed class TgnResponseParserTests
{
    /// <summary>نمونه واقعی مالک (۱۴۰۱/۰۳/۱۹) — عین متن مستندات.</summary>
    private const string OwnerSample =
        "{\"YekGram18\":1442800,\"YekMesghal18\":6648900,\"SekehRob\":5520,\"SekehNim\":8520," +
        "\"SekehEmam\":14500,\"SekehTamam\":14940,\"SekehGerami\":3050,\"OunceTala\":1850," +
        "\"YekMesghal17\":6250000,\"OunceNoghreh\":21940,\"Pelatin\":1442818,\"Dollar\":30000," +
        "\"Euro\":33000,\"Derham\":8000,\"YekGram20\":1602500,\"YekGram21\":1683300," +
        "\"TimeRead\":\"2022/06/09 11:14:48\"}";

    [Fact]
    public void Parse_OwnerSample_MapsEveryKnownAsset()
    {
        var result = TgnResponseParser.Parse(OwnerSample);

        Assert.False(result.IsFatal);
        Assert.Null(result.ProviderError);
        Assert.Equal(15, result.Rates.Count);
        Assert.Empty(result.Problems);
        Assert.Empty(result.MissingKeys);
    }

    [Fact]
    public void Parse_OwnerSample_ReadsQuotedTimeAsTehranTime()
    {
        var result = TgnResponseParser.Parse(OwnerSample);

        // ۱۱:۱۴:۴۸ به وقت ایران (+03:30) ⇒ ۰۷:۴۴:۴۸ UTC همان روز.
        Assert.NotNull(result.QuotedAtUtc);
        Assert.Equal(new DateTimeOffset(2022, 6, 9, 7, 44, 48, TimeSpan.Zero), result.QuotedAtUtc!.Value);
        Assert.Equal("2022/06/09 11:14:48", result.TimeRaw);
    }

    [Fact]
    public void Parse_OwnerSample_ReportsUnknownKeysWithoutFailing()
    {
        // قرارداد منبع: تعداد/ترتیب کلیدها تضمین‌شده نیست. Pelatin واحدش ناشناخته است و
        // عمداً نگاشت نمی‌شود، اما باید دیده و ثبت شود (نه اینکه بی‌صدا حذف شود).
        var result = TgnResponseParser.Parse(OwnerSample);

        Assert.Equal(["Pelatin"], result.UnmappedKeys);
    }

    [Fact]
    public void Parse_OwnerSample_KeepsRawValuesWithoutUnitScaling()
    {
        // مقیاس در نرمال‌ساز اعمال می‌شود، نه در تجزیه (تفکیک مسئولیت‌ها).
        var result = TgnResponseParser.Parse(OwnerSample);

        var coin = Assert.Single(result.Rates, rate => rate.ProviderKey == "SekehEmam");
        Assert.Equal(14500m, coin.RawValue);
        Assert.Equal("COIN_EMAMI", coin.AssetCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyBody_IsFatalEmptyResponse(string? body)
    {
        var result = TgnResponseParser.Parse(body);

        Assert.True(result.IsFatal);
        Assert.Equal("EmptyResponse", result.FatalCode);
    }

    [Theory]
    [InlineData("{\"YekGram18\":1442800,\"TimeRead\":\"2022/06/09 11:14")]          // JSON ناقص (Fuzz ۱)
    [InlineData("<html>500 Internal Server Error</html>")]                          // پاسخ HTML (خطای رایج واسط‌ها)
    public void Parse_InvalidJson_IsFatalInvalidJson(string body)
    {
        var result = TgnResponseParser.Parse(body);

        Assert.True(result.IsFatal);
        Assert.Equal("InvalidJson", result.FatalCode);
    }

    [Fact]
    public void Parse_TrailingComma_IsTolerated()  // سخت‌گیری بیش از حد، دسترسی به داده را قطع می‌کند
    {
        var body = OwnerSample.Replace("},", ",}", StringComparison.Ordinal);

        var result = TgnResponseParser.Parse(body);

        Assert.False(result.IsFatal);
        Assert.Equal(15, result.Rates.Count);
    }

    [Fact]
    public void Parse_NullValue_RejectsThatItemOnly()  // Fuzz ۲
    {
        var body = OwnerSample.Replace("\"OunceTala\":1850", "\"OunceTala\":null", StringComparison.Ordinal);

        var result = TgnResponseParser.Parse(body);

        Assert.False(result.IsFatal);
        Assert.Equal(14, result.Rates.Count);
        Assert.Contains(result.Problems, problem =>
            problem.ProviderKey == "OunceTala" && problem.Reason == RateProblemReason.NullValue);
    }

    [Theory]
    [InlineData("\"7,600,000\"", 7_600_000)]          // عدد متنی با جداکننده (Fuzz ۳)
    [InlineData("\"7600000\"", 7_600_000)]
    [InlineData("\"۷٬۶۰۰٬۰۰۰\"", 7_600_000)]          // ارقام فارسی + جداکننده فارسی
    [InlineData("  \" 7600000 \" ", 7_600_000)]
    [InlineData("\"7600000.5\"", 7_600_000.5)]       // اعشار با نقطه
    public void Parse_TextualNumber_IsAccepted(string rawJsonValue, decimal expected)
    {
        var body = OwnerSample.Replace("\"YekGram18\":1442800", $"\"YekGram18\":{rawJsonValue}", StringComparison.Ordinal);

        var result = TgnResponseParser.Parse(body);

        Assert.False(result.IsFatal);
        var rate = Assert.Single(result.Rates, item => item.ProviderKey == "YekGram18");
        Assert.Equal(expected, rate.RawValue);
    }

    [Theory]
    [InlineData("\"7.600.000\"")]     // جداکننده اعشاری اروپایی — ابهام‌آفرین، پذیرفته نمی‌شود
    [InlineData("\"۷۶۰۰ هزار\"")]
    [InlineData("\"abc\"")]
    [InlineData("\"\"")]              // رشته خالی
    public void Parse_AmbiguousOrNonNumericText_IsRejected(string rawJsonValue)
    {
        var body = OwnerSample.Replace("\"YekGram18\":1442800", $"\"YekGram18\":{rawJsonValue}", StringComparison.Ordinal);

        var result = TgnResponseParser.Parse(body);

        Assert.False(result.IsFatal);
        Assert.DoesNotContain(result.Rates, item => item.ProviderKey == "YekGram18");
        Assert.Contains(result.Problems, problem =>
            problem.ProviderKey == "YekGram18" && problem.Reason == RateProblemReason.NotANumber);
    }

    [Fact]
    public void Parse_UnexpectedType_IsReportedNotThrown()
    {
        var body = OwnerSample.Replace("\"Dollar\":30000", "\"Dollar\":{\"value\":30000}", StringComparison.Ordinal);

        var result = TgnResponseParser.Parse(body);

        Assert.False(result.IsFatal);
        Assert.Contains(result.Problems, problem =>
            problem.ProviderKey == "Dollar" && problem.Reason == RateProblemReason.UnexpectedType);
    }

    [Fact]
    public void Parse_NumberTooLargeForDecimal_IsRejected()
    {
        var body = OwnerSample.Replace("\"Dollar\":30000", "\"Dollar\":1e400", StringComparison.Ordinal);

        var result = TgnResponseParser.Parse(body);

        Assert.False(result.IsFatal);
        Assert.Contains(result.Problems, problem => problem.ProviderKey == "Dollar");
    }

    [Fact]
    public void Parse_MissingKey_IsInformationalNotAProblem()
    {
        // بند ۶ مستندات سرویس: تعداد و ترتیب کلیدها تضمین‌شده نیست ⇒ غیبت کلید نباید «خطا» باشد.
        var body = OwnerSample.Replace("\"Euro\":33000,", string.Empty, StringComparison.Ordinal);

        var result = TgnResponseParser.Parse(body);

        Assert.False(result.IsFatal);
        Assert.Contains("Euro", result.MissingKeys);
        Assert.Empty(result.Problems);
        Assert.Equal(14, result.Rates.Count);
    }

    [Fact]
    public void Parse_SparseResponse_IsAccepted_AndMissingKeysAreListed()
    {
        // پاسخ فقط با چند کلید (وضعیتی که مستندات منبع مجاز می‌داند).
        var result = TgnResponseParser.Parse("{\"YekGram18\":1442800,\"TimeRead\":\"2022/06/09 11:14:48\"}");

        Assert.False(result.IsFatal);
        var rate = Assert.Single(result.Rates);
        Assert.Equal("GOLD_GRAM_18", rate.AssetCode);
        Assert.Equal(14, result.MissingKeys.Count);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void Parse_MissingTime_IsFatal_BecauseRateCannotBePublishedWithoutTime()
    {
        var body = OwnerSample.Replace(",\"TimeRead\":\"2022/06/09 11:14:48\"", string.Empty, StringComparison.Ordinal);

        var result = TgnResponseParser.Parse(body);

        Assert.True(result.IsFatal);
        Assert.Equal("MissingTime", result.FatalCode);
    }

    [Theory]
    [InlineData("\"2022/06/09\"")]                 // تاریخ بدون ساعت
    [InlineData("\"not-a-date\"")]
    [InlineData("\"\u06F1\u06F4\u06F0\u06F1/\u06F0\u06F3/\u06F1\u06F9\"")] // ۱۴۰۱/۰۳/۱۹ بی‌ساعت (ورودی نامعتبر)
    public void Parse_InvalidTime_IsFatal(string rawJsonValue)  // Fuzz تاریخ
    {
        var body = OwnerSample.Replace("\"2022/06/09 11:14:48\"", rawJsonValue, StringComparison.Ordinal);

        var result = TgnResponseParser.Parse(body);

        Assert.True(result.IsFatal);
        Assert.Equal("InvalidTime", result.FatalCode);
    }

    [Fact]
    public void Parse_TimeWithPersianDigits_IsAccepted()
    {
        var body = OwnerSample.Replace(
            "\"2022/06/09 11:14:48\"",
            "\"\u06F2\u06F0\u06F2\u06F2/\u06F0\u06F6/\u06F0\u06F9 \u06F1\u06F1:\u06F1\u06F4:\u06F4\u06F8\"",
            StringComparison.Ordinal);

        var result = TgnResponseParser.Parse(body);

        Assert.False(result.IsFatal);
        Assert.Equal(new DateTimeOffset(2022, 6, 9, 7, 44, 48, TimeSpan.Zero), result.QuotedAtUtc);
    }

    [Fact]
    public void Parse_JsonStringWrapper_IsUnwrapped()  // برخی واسط‌ها پاسخ را رشته می‌کنند
    {
        var wrapped = "\"" + OwnerSample.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

        var result = TgnResponseParser.Parse(wrapped);

        Assert.False(result.IsFatal);
        Assert.Equal(15, result.Rates.Count);
    }

    [Theory]
    [InlineData("{\"Error\":\"Unauthorized\"}")]
    [InlineData("{\"error\":\"unauthorized\"}")]  // بزرگی/کوچکی حروف مهم نیست
    public void Parse_ProviderError_IsReportedAsProviderError(string body)
    {
        var result = TgnResponseParser.Parse(body);

        Assert.False(result.IsFatal);
        Assert.Equal("Unauthorized", result.ProviderError, ignoreCase: true);
        Assert.Empty(result.Rates);
    }

    [Fact]
    public void Parse_UnexpectedRootKind_IsFatal()
    {
        var result = TgnResponseParser.Parse("[1,2,3]");

        Assert.True(result.IsFatal);
        Assert.Equal("UnexpectedRoot", result.FatalCode);
    }

    [Fact]
    public void TryParseTextNumber_NegativeValue_IsParsed_AndRejectedLaterByValidator()
    {
        Assert.True(TgnResponseParser.TryParseTextNumber("-5", out var value));
        Assert.Equal(-5m, value);
    }
}
