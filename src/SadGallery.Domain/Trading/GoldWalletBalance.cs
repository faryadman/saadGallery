namespace SadGallery.Domain.Trading;

/// <summary>قواعد مانده بدون وابستگی به ذخیره‌سازی؛ ذخیره اتمیک نتیجه بر عهده مخزن SQL است.</summary>
public sealed record GoldWalletBalance(decimal CashIrt, decimal ReservedIrt, decimal Grams750)
{
    public decimal AvailableIrt => CashIrt - ReservedIrt;
    public bool IsValid => CashIrt is >= 0 and <= GoldPurchaseMath.MaxMoney && ReservedIrt >= 0 && ReservedIrt <= CashIrt
        && decimal.Truncate(CashIrt) == CashIrt && decimal.Truncate(ReservedIrt) == ReservedIrt
        && Grams750 is >= 0 and <= GoldPurchaseMath.MaxGrams && decimal.Round(Grams750, 3) == Grams750;
    public GoldWalletBalance? Credit(GoldLedgerKind kind, decimal amount)
    {
        if (!IsValid) return null;
        var next = kind switch
        {
            GoldLedgerKind.TomanCredit when GoldPurchaseMath.ValidMoney(amount) => this with { CashIrt = CashIrt + amount },
            GoldLedgerKind.GoldCredit when GoldPurchaseMath.ValidGrams(amount) => this with { Grams750 = Grams750 + amount },
            _ => null
        };
        return next?.IsValid == true ? next : null;
    }
    public GoldWalletBalance? Reserve(decimal totalIrt) => IsValid && GoldPurchaseMath.ValidMoney(totalIrt) && AvailableIrt >= totalIrt ? this with { ReservedIrt = ReservedIrt + totalIrt } : null;
    public GoldWalletBalance? Release(decimal totalIrt) => IsValid && GoldPurchaseMath.ValidMoney(totalIrt) && ReservedIrt >= totalIrt ? this with { ReservedIrt = ReservedIrt - totalIrt } : null;
    public GoldWalletBalance? Purchase(decimal totalIrt, decimal grams)
    {
        if (Release(totalIrt) is not {} released || !GoldPurchaseMath.ValidGrams(grams)) return null;
        var next = released with { CashIrt = CashIrt - totalIrt, Grams750 = Grams750 + grams };
        return next.IsValid ? next : null;
    }
}
