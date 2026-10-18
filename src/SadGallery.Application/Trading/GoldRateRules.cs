using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Trading;
namespace SadGallery.Application.Trading;

public sealed record GoldRateCandidate(GoldRate Rate, string AssetCode, CurrencyUnit Unit, RateQuality Quality, bool Anomaly);
public static class GoldRateRules
{
    public static TimeSpan MaximumAge(RateFreshnessPolicy freshness) => freshness.LiveWindow < TimeSpan.FromMinutes(5) ? freshness.LiveWindow : TimeSpan.FromMinutes(5);
    public static bool Eligible(GoldRateCandidate candidate, DateTimeOffset now, RateFreshnessPolicy freshness)
    {
        var r = candidate.Rate; var maxAge = MaximumAge(freshness);
        return candidate.AssetCode == "GOLD_GRAM_18" && r.ProviderId == "Tgn" && candidate.Unit == CurrencyUnit.Irt && r.AmountIrt > 0 && r.AmountIrt <= GoldPurchaseMath.MaxMoney
            && candidate.Quality == RateQuality.Live && !candidate.Anomaly && r.QuotedAtUtc <= now && r.FetchedAtUtc <= now
            && now - r.QuotedAtUtc <= maxAge && now - r.FetchedAtUtc <= maxAge;
    }
}
