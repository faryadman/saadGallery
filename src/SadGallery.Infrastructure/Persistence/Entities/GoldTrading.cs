using SadGallery.Domain.Trading;
namespace SadGallery.Infrastructure.Persistence.Entities;

public sealed class GoldWallet
{
    public int UserId { get; set; }
    public bool Enabled { get; set; }
    public decimal CashIrt { get; set; }
    public decimal ReservedIrt { get; set; }
    public decimal Grams750 { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class GoldTradingSetting
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public decimal? FeePercent { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class GoldOrder
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public GoldOrderStatus Status { get; set; }
    public long RateSnapshotId { get; set; }
    public decimal Grams750 { get; set; }
    public decimal RateIrt { get; set; }
    public decimal FeePercent { get; set; }
    public decimal PrincipalIrt { get; set; }
    public decimal FeeIrt { get; set; }
    public decimal TotalIrt { get; set; }
    public int Fineness { get; set; } = GoldPurchaseMath.Fineness;
    public string FormulaVersion { get; set; } = GoldPurchaseMath.Version;
    public DateTimeOffset QuotedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RequestedAtUtc { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public int? ReviewerId { get; set; }
    public string? ReviewNote { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class GoldLedgerEntry
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public int ActorId { get; set; }
    public Guid? OrderId { get; set; }
    public GoldLedgerKind Kind { get; set; }
    public decimal CashDeltaIrt { get; set; }
    public decimal GoldDeltaGrams { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset AtUtc { get; set; }
}
