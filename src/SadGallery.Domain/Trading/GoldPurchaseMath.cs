namespace SadGallery.Domain.Trading;

public enum GoldOrderStatus { Quote = 0, Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4 }
public enum GoldLedgerKind { TomanCredit = 1, GoldCredit = 2, Purchase = 3 }
public sealed record GoldCost(decimal PrincipalIrt, decimal FeeIrt, decimal TotalIrt);

/// <summary>خرید گرم معادل ۷۵۰، تومان کامل، وزن با دقت یک سوت؛ نسخه مستقل از قیمت مصنوعات.</summary>
public static class GoldPurchaseMath
{
    public const string Version = "gold-buy-v1";
    public const int Fineness = 750;
    public const decimal MaxMoney = 999_999_999_999_999m;
    public const decimal MaxGrams = 999_999m;
    public static bool ValidGrams(decimal grams) => grams > 0 && grams <= MaxGrams && decimal.Round(grams, 3) == grams;
    public static bool ValidMoney(decimal amount) => amount > 0 && amount <= MaxMoney && decimal.Truncate(amount) == amount;
    public static bool ValidFee(decimal fee) => fee is >= 0 and <= 100 && decimal.Round(fee, 3) == fee;
    public static GoldCost? Calculate(decimal grams, decimal rateIrt, decimal feePercent)
    {
        if (!ValidGrams(grams) || rateIrt <= 0 || rateIrt > MaxMoney || decimal.Round(rateIrt, 4) != rateIrt || !ValidFee(feePercent)) return null;
        var principal = decimal.Round(grams * rateIrt, 0, MidpointRounding.AwayFromZero);
        var fee = decimal.Round(principal * feePercent / 100m, 0, MidpointRounding.AwayFromZero);
        var total = principal + fee;
        return ValidMoney(principal) && ValidMoney(total) ? new(principal, fee, total) : null;
    }
}
