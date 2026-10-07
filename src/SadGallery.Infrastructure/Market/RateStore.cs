using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Market;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;

namespace SadGallery.Infrastructure.Market;

/// <summary>
/// پیاده‌سازی <see cref="IRateStore"/> روی SQL Server.
/// قفل چند-نمونه‌ای با «اجاره زمانی» روی رکورد تک‌تایی <c>MarketFetchLease</c> انجام می‌شود
/// (UPDATE شرطی اتمیک + انقضا؛ بدون وابستگی به Session و ایمن در برابر کرش).
/// </summary>
public sealed class RateStore : IRateStore
{
    private readonly SadGalleryDbContext _db;
    private readonly IClock _clock;

    public RateStore(SadGalleryDbContext db, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);

        _db = db;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<long> BeginRunAsync(RateFetchRunStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);

        var run = new MarketPriceFetchRun
        {
            ProviderId = Truncate(start.ProviderId, 64),
            Trigger = Truncate(start.Trigger, 32),
            StartedAtUtc = start.StartedAtUtc,
        };

        _db.MarketPriceFetchRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return run.Id;
    }

    /// <inheritdoc />
    public async Task CompleteRunAsync(long runId, RateFetchRunFinish finish, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(finish);

        var run = await _db.MarketPriceFetchRuns
            .FirstOrDefaultAsync(item => item.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return;
        }

        run.FinishedAtUtc = finish.FinishedAtUtc;
        run.DurationMs = finish.DurationMs;
        run.Outcome = (int)finish.Outcome;
        run.AcceptedCount = finish.AcceptedCount;
        run.FlaggedCount = finish.FlaggedCount;
        run.RejectedCount = finish.RejectedCount;
        run.HttpStatusCode = finish.HttpStatusCode;
        run.ErrorCode = TruncateNullable(finish.ErrorCode, 64);
        run.Notes = TruncateNullable(finish.Notes, 512);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveRatesAsync(long runId, IReadOnlyList<NormalizedRate> rates, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rates);

        if (rates.Count == 0)
        {
            return;
        }

        var entities = new List<MarketRateSnapshot>(rates.Count);

        foreach (var rate in rates)
        {
            // نگهبان نهایی پیش از ذخیره: هیچ نرخ ناقصی وارد دیتابیس نمی‌شود (معیار پذیرش فاز ۲).
            var violations = NormalizedRate.FindInvariantViolations(rate);

            if (violations.Count > 0)
            {
                throw new InvalidOperationException(
                    $"نرخ «{rate.AssetCode}» ثابت‌های لازم را ندارد و ذخیره نشد: {string.Join(" | ", violations)}");
            }

            entities.Add(new MarketRateSnapshot
            {
                AssetCode = rate.AssetCode,
                Amount = rate.Amount,
                QuoteUnit = rate.QuoteUnit,
                ProviderId = Truncate(rate.ProviderId, 64),
                QuotedAtUtc = rate.QuotedAtUtc,
                FetchedAtUtc = rate.FetchedAtUtc,
                Quality = rate.Quality,
                ProviderRawValue = rate.ProviderRawValue,
                ScaleApplied = rate.ScaleApplied,
                IsAnomalySuspected = rate.IsAnomalySuspected,
                FetchRunId = runId > 0 ? runId : null,
                CreatedAtUtc = rate.FetchedAtUtc,
            });
        }

        _db.MarketRates.AddRange(entities);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NormalizedRate>> GetLatestAsync(CancellationToken cancellationToken)
    {
        // آخرین نرخ هر دارایی = بیشترین شناسه آن دارایی (جدول فقط افزودنی است).
        var latestIds = await _db.MarketRates
            .Where(rate => !rate.IsAnomalySuspected)
            .GroupBy(rate => rate.AssetCode)
            .Select(group => group.Max(rate => rate.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (latestIds.Count == 0)
        {
            return [];
        }

        var rows = await _db.MarketRates
            .AsNoTracking()
            .Where(rate => latestIds.Contains(rate.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToDomain).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NormalizedRate>> GetHistoryAsync(string assetCode, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetCode))
        {
            return [];
        }

        var rows = await _db.MarketRates
            .AsNoTracking()
            .Where(rate => rate.AssetCode == assetCode && rate.QuotedAtUtc >= fromUtc && rate.QuotedAtUtc <= toUtc)
            .OrderBy(rate => rate.QuotedAtUtc)
            .ThenBy(rate => rate.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToDomain).ToArray();
    }

    /// <inheritdoc />
    public async Task<bool> TryAcquireLeaseAsync(string ownerId, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var now = _clock.UtcNow;
        var expiresAt = now + ttl;
        var owner = Truncate(ownerId, 64);

        // UPDATE شرطی اتمیک: یا اجاره منقضی است، یا دارنده همان نمونه فعلی است.
        var affected = await _db.Database.ExecuteSqlAsync(
            $"UPDATE MarketFetchLease WITH (UPDLOCK, ROWLOCK) SET OwnerId = {owner}, AcquiredAtUtc = {now}, ExpiresAtUtc = {expiresAt} WHERE Id = 1 AND (ExpiresAtUtc < {now} OR OwnerId = {owner})",
            cancellationToken).ConfigureAwait(false);

        return affected == 1;
    }

    /// <inheritdoc />
    public async Task ReleaseLeaseAsync(string ownerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var owner = Truncate(ownerId, 64);
        var epoch = DateTimeOffset.UnixEpoch;

        await _db.Database.ExecuteSqlAsync(
            $"UPDATE MarketFetchLease SET ExpiresAtUtc = {epoch}, AcquiredAtUtc = {epoch} WHERE Id = 1 AND OwnerId = {owner}",
            cancellationToken).ConfigureAwait(false);
    }

    private static NormalizedRate ToDomain(MarketRateSnapshot entity) => new()
    {
        AssetCode = entity.AssetCode,
        Title = AssetCatalog.FindByCode(entity.AssetCode)?.Title ?? entity.AssetCode,
        Amount = entity.Amount,
        QuoteUnit = entity.QuoteUnit,
        ProviderId = entity.ProviderId,
        QuotedAtUtc = entity.QuotedAtUtc,
        FetchedAtUtc = entity.FetchedAtUtc,
        Quality = Enum.IsDefined(entity.Quality) ? entity.Quality : RateQuality.Invalid,
        ProviderRawValue = entity.ProviderRawValue,
        ScaleApplied = entity.ScaleApplied,
        IsAnomalySuspected = entity.IsAnomalySuspected,
    };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? TruncateNullable(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
