using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using SadGallery.Infrastructure.Market;
using Xunit;

namespace SadGallery.Tests.Integration.Market;

/// <summary>
/// تست‌های منبع HTTP واقعی با پاسخ‌دهنده ساختگی (بدون هیچ تماس اینترنتی).
/// پوشش: موفق، احراز هویت ناموفق، ۴۲۹، ۵۰۰، مهلت، پاسخ غول‌آسا، تنظیم‌نشدگی،
/// و — مهم‌ترین — «هیچ اعتبارنامه‌ای در لاگ‌ها دیده نشود».
/// </summary>
public sealed class TgnRateProviderTests
{
    private const string Sample =
        "{\"YekGram18\":1442800,\"SekehEmam\":14500,\"OunceNoghreh\":21940," +
        "\"TimeRead\":\"2022/06/09 11:14:48\"}";

    private const string Username = "rate-user";
    private const string Password = "TopSecret-Pass-1";

    private static RateOptions Options(int timeoutSeconds = 5, int maxBytes = 4096) => new()
    {
        Provider = RateOptions.ProviderTgn,
        Username = Username,
        Password = Password,
        EndpointTemplate = RateOptions.DefaultEndpointTemplate,
        AllowedProviderDomains = ["webservice.tgnsrv.ir"],
        HttpTimeoutSeconds = timeoutSeconds,
        MaxResponseBytes = maxBytes,
    };

    private static (TgnRateProvider Provider, StubHandler Handler, ListLogger Logger) Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
        RateOptions? options = null)
    {
        var effective = options ?? Options();
        var handler = new StubHandler(responder);
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var logger = new ListLogger();

        return (new TgnRateProvider(http, effective, new TestClock(DateTimeOffset.UtcNow), logger), handler, logger);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Fetch_Success_ReturnsParsedRates()
    {
        var (provider, handler, _) = Create((_, _) => Task.FromResult(Json(Sample)));

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.Success, result.Status);
        Assert.Equal(3, result.Payload!.Rates.Count);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.StartsWith("https://webservice.tgnsrv.ir/Pr/Get/", handler.LastUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fetch_NeverLogsCredentials()
    {
        var (provider, _, logger) = Create((_, _) => Task.FromResult(Json(Sample)));

        await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(logger.Messages);
        Assert.DoesNotContain(logger.Messages, message => message.Contains(Password, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains(Username, StringComparison.Ordinal));

        // گزارش باید آدرس پوشانده‌شده داشته باشد.
        Assert.Contains(logger.Messages, message => message.Contains("***", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fetch_NeverLogsCredentials_OnFailurePaths()
    {
        var (unauthorized, _, unauthorizedLog) = Create((_, _) =>
            Task.FromResult(Json("{\"Error\":\"Unauthorized\"}")));
        var (serverError, _, errorLog) = Create((_, _) =>
            Task.FromResult(Json("{\"message\":\"boom\"}", HttpStatusCode.InternalServerError)));

        await unauthorized.FetchAsync(TestContext.Current.CancellationToken);
        await serverError.FetchAsync(TestContext.Current.CancellationToken);

        foreach (var logger in new[] { unauthorizedLog, errorLog })
        {
            Assert.DoesNotContain(logger.Messages, message => message.Contains(Password, StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("webservice.tgnsrv.ir/Pr/Get/rate-user", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Fetch_ProviderUnauthorizedBody_IsReportedAsUnauthorized()
    {
        var (provider, _, _) = Create((_, _) => Task.FromResult(Json("{\"Error\":\"Unauthorized\"}")));

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.Unauthorized, result.Status);
        Assert.Equal("Unauthorized", result.ErrorCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Fetch_AuthHttpStatus_IsReportedAsUnauthorized(HttpStatusCode status)
    {
        var (provider, _, _) = Create((_, _) => Task.FromResult(new HttpResponseMessage(status)));

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task Fetch_TooManyRequests_IsReportedForBackoff()
    {
        var (provider, _, _) = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.HttpError, result.Status);
        Assert.Equal("RateLimited", result.ErrorCode);
        Assert.True(RateBackoffPolicy.IsFailure(result.Status));
    }

    [Fact]
    public async Task Fetch_ServerError_IsReportedAsHttpError()
    {
        var (provider, _, _) = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.HttpError, result.Status);
        Assert.Equal("Http500", result.ErrorCode);
    }

    [Fact]
    public async Task Fetch_InvalidJson_IsReportedAsInvalidPayload()
    {
        var (provider, _, _) = Create((_, _) => Task.FromResult(Json("<html>error</html>")));

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.PayloadInvalid, result.Status);
        Assert.Equal("InvalidJson", result.ErrorCode);
    }

    [Fact]
    public async Task Fetch_Timeout_IsReportedWithoutThrowing()
    {
        var (provider, _, _) = Create(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return Json(Sample);
            },
            Options(timeoutSeconds: 1));

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.Timeout, result.Status);
        Assert.Equal("Timeout", result.ErrorCode);
    }

    [Fact]
    public async Task Fetch_OversizedBody_IsRejected()
    {
        var huge = "{\"YekGram18\":1442800,\"TimeRead\":\"2022/06/09 11:14:48\",\"pad\":\"" + new string('x', 5000) + "\"}";
        var (provider, _, _) = Create((_, _) => Task.FromResult(Json(huge)), Options(maxBytes: 1024));

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.ResponseTooLarge, result.Status);
    }

    [Fact]
    public async Task Fetch_OversizedDeclaredContentLength_IsRejectedWithoutReading()
    {
        var content = new StringContent(new string('x', 2000));
        content.Headers.ContentLength = 2000;
        var (provider, _, _) = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }), Options(maxBytes: 512));

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.ResponseTooLarge, result.Status);
    }

    [Fact]
    public async Task Fetch_NetworkFailure_IsReportedAsTransportError()
    {
        var (provider, _, _) = Create((_, _) => throw new HttpRequestException("connection refused"));

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.TransportError, result.Status);
    }

    [Fact]
    public async Task Fetch_WithCancellation_PropagatesCancellation()
    {
        var (provider, _, _) = Create(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return Json(Sample);
        });

        using var source = new CancellationTokenSource();
        var pending = provider.FetchAsync(source.Token);
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task Fetch_WithoutCredentials_IsNotConfigured_AndMakesNoCall()
    {
        var options = Options();
        options.Username = string.Empty;

        var (provider, handler, _) = Create((_, _) => Task.FromResult(Json(Sample)), options);

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.False(provider.IsConfigured);
        Assert.Equal(RateFetchStatus.NotConfigured, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Fetch_WithDomainNotAllowed_IsNotConfigured_AndMakesNoCall()
    {
        var options = Options();
        options.AllowedProviderDomains = ["other-domain.invalid"];

        var (provider, handler, _) = Create((_, _) => Task.FromResult(Json(Sample)), options);

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.False(provider.IsConfigured);
        Assert.Equal(RateFetchStatus.NotConfigured, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Fetch_DisabledProvider_IsNotConfigured()
    {
        var options = Options();
        options.Provider = RateOptions.ProviderDisabled;

        var (provider, handler, _) = Create((_, _) => Task.FromResult(Json(Sample)), options);

        var result = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.False(provider.IsConfigured);
        Assert.Equal(RateFetchStatus.NotConfigured, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastMethod = request.Method;
            LastUri = request.RequestUri;

            return responder(request, cancellationToken);
        }
    }

    internal sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    internal sealed class ListLogger : ILogger<TgnRateProvider>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Messages.Add(formatter(state, exception));
        }
    }
}
