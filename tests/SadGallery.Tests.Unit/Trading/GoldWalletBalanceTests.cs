using SadGallery.Domain.Trading;
using Xunit;
namespace SadGallery.Tests.Unit.Trading;
public sealed class GoldWalletBalanceTests
{
    [Fact] public void ReserveDoesNotPostAndReleaseRestoresSpendableCash()
    {
        var original = new GoldWalletBalance(10000, 0, 1.250m);
        var reserved = original.Reserve(7000)!;
        Assert.Equal(10000, reserved.CashIrt); Assert.Equal(3000, reserved.AvailableIrt); Assert.Equal(1.250m, reserved.Grams750);
        Assert.Null(reserved.Reserve(4000)); Assert.Equal(original, reserved.Release(7000)); Assert.Null(reserved.Release(7001));
    }
    [Fact] public void PurchaseConsumesOnlyReservedMoneyAndCreditsExactGold()
    {
        var wallet = new GoldWalletBalance(10000, 7000, 1.250m);
        Assert.Equal(new GoldWalletBalance(3000, 0, 1.75m), wallet.Purchase(7000, .5m));
        Assert.Null(wallet.Purchase(7001, .5m)); Assert.Null(wallet.Purchase(7000, .0001m));
        Assert.Null((wallet with { Grams750 = GoldPurchaseMath.MaxGrams }).Purchase(7000, .001m));
    }
    [Fact] public void CreditRejectsInvalidKindFractionalMoneyAndOverflow()
    {
        var wallet = new GoldWalletBalance(10000, 7000, 1.250m);
        Assert.Equal(wallet with { CashIrt = 11000 }, wallet.Credit(GoldLedgerKind.TomanCredit, 1000));
        Assert.Equal(wallet with { Grams750 = 1.251m }, wallet.Credit(GoldLedgerKind.GoldCredit, .001m));
        Assert.Null(wallet.Credit(GoldLedgerKind.TomanCredit, .1m)); Assert.Null(wallet.Credit(GoldLedgerKind.Purchase, 100));
        Assert.Null(wallet.Credit(GoldLedgerKind.TomanCredit, GoldPurchaseMath.MaxMoney));
        Assert.Null(new GoldWalletBalance(100, 101, 0).Reserve(1));
    }
}
