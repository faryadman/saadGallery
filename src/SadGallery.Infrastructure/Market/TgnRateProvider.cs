using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;

namespace SadGallery.Infrastructure.Market;

/// <summary>
/// منبع نرخ واقعی (سرویس tgnsrv — قرارداد در docs/API.md §ب).
/// <para>
/// نکات امنیتی پیاده‌سازی (ADR-0012):
/// <list type="bullet">
///   <item>اعتبارنامه در مسیر آدرس قرار دارد ⇒ آدرس واقعی هرگز لاگ نمی‌شود؛ فقط
///         <see cref="TgnEndpointTemplate.Describe"/> (با ***) در گزارش‌ها می‌آید.</item>
///   <item>مهلت درخواست با CancellationToken مستقل اعمال می‌شود و حجم پاسخ سقف دارد
///         (محافظت از حافظه و از پاسخ‌های مخدوش).</item>
///   <item>میزبان محدود به دامنه‌های مجاز است (ضد SSRF) و طرح فقط https.</item>
///   <item>در هیچ حالت خطایی، استثنا به بیرون درز نمی‌کند (به‌جز لغو واقعی).</item>
/// </list>
/// </para>
/// </summary>
public sealed class TgnRateProvider : IRateProvider
{
    /// <summary>شناسه ثابت منبع.</summary>
    public const string Id = "Tgn";

    private readonly HttpClient _http;
    private readonly RateOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<TgnRateProvider> _logger;

    /// <summary>ساخت منبع.</summary>
    public TgnRateProvider(HttpClient http, RateOptions options, IClock clock, ILogger<TgnRateProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _http = http;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderId => Id;

    /// <inheritdoc />
    public bool IsConfigured =>
        _options.IsTgnProvider &&
        !string.IsNullOrWhiteSpace(_options.Username) &&
        !string.IsNullOrWhiteSpace(_options.Password) &&
        TgnEndpointTemplate.Validate(_options) is null;

    /// <inheritdoc />
    public async Task<RateProviderFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var startedAt = _clock.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        if (!IsConfigured)
        {
            return RateProviderFetchResult.Failure(
                ProviderId,
                RateFetchStatus.NotConfigured,
                startedAt,
                "NotConfigured",
                "منبع Tgn تنظیم نشده است (اعتبارنامه/دامنه مجاز/قالب آدرس).");
        }

        Uri endpoint;

        try
        {
            endpoint = TgnEndpointTemplate.Build(_options);
        }
        catch (InvalidOperationException exception)
        {
            return RateProviderFetchResult.Failure(ProviderId, RateFetchStatus.NotConfigured, startedAt, "InvalidEndpoint", exception.Message);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.HttpTimeoutSeconds));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token)
                .ConfigureAwait(false);

            var statusCode = (int)response.StatusCode;

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return Failure(RateFetchStatus.Unauthorized, "Unauthorized", statusCode, "اعتبارنامه منبع نرخ پذیرفته نشد.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return Failure(RateFetchStatus.HttpError, "RateLimited", statusCode, "منبع نرخ محدودیت درخواست اعمال کرد (429).");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Failure(RateFetchStatus.HttpError, $"Http{statusCode}", statusCode, "پاسخ ناموفق از منبع نرخ.");
            }

            if (response.Content.Headers.ContentLength is { } declaredLength && declaredLength > _options.MaxResponseBytes)
            {
                return Failure(RateFetchStatus.ResponseTooLarge, "ResponseTooLarge", statusCode, "حجم اعلام‌شده پاسخ بیش از سقف مجاز بود.");
            }

            var (readOk, body, readBytes) = await ReadBodyAsync(response, _options.MaxResponseBytes, timeoutSource.Token).ConfigureAwait(false);

            if (!readOk)
            {
                return Failure(RateFetchStatus.ResponseTooLarge, "ResponseTooLarge", statusCode, "حجم پاسخ از سقف مجاز بیشتر بود.");
            }

            stopwatch.Stop();

            var parse = TgnResponseParser.Parse(body);

            if (!string.IsNullOrWhiteSpace(parse.ProviderError))
            {
                var providerError = Truncate(parse.ProviderError, 32);

                return parse.ProviderError.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
                    ? Failure(RateFetchStatus.Unauthorized, "Unauthorized", statusCode, "منبع نرخ پاسخ Unauthorized داد.")
                    : Failure(RateFetchStatus.PayloadInvalid, providerError, statusCode, "منبع نرخ خطا اعلام کرد.");
            }

            if (parse.IsFatal)
            {
                return RateProviderFetchResult.Failure(
                    ProviderId,
                    RateFetchStatus.PayloadInvalid,
                    startedAt,
                    parse.FatalCode ?? "PayloadInvalid",
                    parse.FatalDetail,
                    statusCode,
                    (int)stopwatch.ElapsedMilliseconds);
            }

            _logger.LogInformation(
                "دریافت نرخ از منبع {ProviderId} موفق بود: {RateCount} قلم، {Bytes} بایت، {DurationMs}ms (آدرس پوشانده‌شده: {Endpoint})",
                ProviderId,
                parse.Rates.Count,
                readBytes,
                stopwatch.ElapsedMilliseconds,
                TgnEndpointTemplate.Describe(_options));

            return RateProviderFetchResult.Ok(ProviderId, startedAt, parse, (int)stopwatch.ElapsedMilliseconds, statusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // لغو واقعی برنامه — بالا می‌رود
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogWarning(exception, "مهلت درخواست منبع نرخ {ProviderId} تمام شد ({TimeoutSeconds}s).", ProviderId, _options.HttpTimeoutSeconds);
            return Failure(RateFetchStatus.Timeout, "Timeout", null, $"مهلت درخواست ({_options.HttpTimeoutSeconds} ثانیه) تمام شد.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning("خطای شبکه در تماس با منبع نرخ {ProviderId}: {ExceptionType}", ProviderId, exception.GetType().Name);
            return Failure(RateFetchStatus.TransportError, "TransportError", null, $"خطای شبکه ({exception.GetType().Name}).");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "خطای پیش‌بینی‌نشده در تماس با منبع نرخ {ProviderId}: {ExceptionType}", ProviderId, exception.GetType().Name);
            return Failure(RateFetchStatus.TransportError, "UnexpectedError", null, $"خطای پیش‌بینی‌نشده ({exception.GetType().Name}).");
        }

        RateProviderFetchResult Failure(RateFetchStatus status, string code, int? httpStatusCode, string detail)
        {
            _logger.LogWarning(
                "دریافت نرخ از منبع {ProviderId} ناموفق بود: {ErrorCode} (HTTP {HttpStatusCode}) — {Detail}",
                ProviderId,
                code,
                httpStatusCode,
                detail);

            return RateProviderFetchResult.Failure(ProviderId, status, startedAt, code, detail, httpStatusCode, (int)stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>خواندن بدنه با سقف بایت (بدون اتکا به Content-Length).</summary>
    private static async Task<(bool Ok, string? Body, long ReadBytes)> ReadBodyAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > maxBytes)
            {
                return (false, null, buffer.Length + read);
            }

            buffer.Write(chunk, 0, read);
        }

        return (true, Encoding.UTF8.GetString(buffer.ToArray()), buffer.Length);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
