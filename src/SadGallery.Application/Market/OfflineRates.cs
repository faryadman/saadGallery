using SadGallery.Application.Abstractions;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Market;

namespace SadGallery.Application.Market;

// Intentionally excludes identity, provider credentials, URLs, raw payloads and member history.
public sealed record OfflineRate(string AssetCode, string Title, string Amount, string Unit, DateTimeOffset QuotedAtUtc, DateTimeOffset ExpiresAtUtc);
public sealed record OfflineRates(int Schema, DateTimeOffset CapturedAtUtc, DateTimeOffset? FetchedAtUtc, bool IsSampleData, IReadOnlyList<OfflineRate> Items)
{
    public static OfflineRates From(RateDisplayModel model, RateFreshnessPolicy freshness, IClock clock) => new(1,
        clock.UtcNow, model.LastFetchedUtc, model.IsSampleData,
        model.Items.Where(x => x.Quality != RateQuality.Invalid && AssetCatalog.FindByCode(x.AssetCode) != null)
            .Take(100).Select(x => new OfflineRate(x.AssetCode, AssetCatalog.FindByCode(x.AssetCode)!.Title,
                x.AmountText, x.UnitText, x.QuotedAtUtc, x.QuotedAtUtc + freshness.MaxAge)).ToArray());
}
