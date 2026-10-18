using SadGallery.Application.Administration;
using SadGallery.Domain.Trading;

namespace SadGallery.Application.Trading;

public sealed record GoldSettings(bool Enabled, decimal? FeePercent, byte[] Version);
public sealed record GoldRate(long SnapshotId, decimal AmountIrt, DateTimeOffset QuotedAtUtc, DateTimeOffset FetchedAtUtc, string ProviderId);
public sealed record GoldWalletView(int UserId, string Name, bool Enabled, decimal CashIrt, decimal ReservedIrt, decimal Grams, byte[] Version)
{ public decimal AvailableIrt => CashIrt - ReservedIrt; }
public sealed record GoldOrderView(Guid Id, int UserId, string CustomerName, GoldOrderStatus Status, decimal Grams, decimal RateIrt,
    decimal FeePercent, decimal PrincipalIrt, decimal FeeIrt, decimal TotalIrt, DateTimeOffset QuotedAtUtc,
    DateTimeOffset ExpiresAtUtc, DateTimeOffset? RequestedAtUtc, DateTimeOffset? ReviewedAtUtc, int? ReviewerId, string? Note, byte[] Version);
public sealed record GoldLedgerView(Guid Id, GoldLedgerKind Kind, decimal CashDeltaIrt, decimal GoldDeltaGrams, string Reason, int ActorId, DateTimeOffset AtUtc, Guid? OrderId);
public sealed record GoldWalletPage(GoldWalletView? Wallet, GoldSettings Settings, GoldRate? Rate, Page<GoldOrderView> Orders, Page<GoldLedgerView> Ledger);
public enum GoldResult { Saved, Invalid, Forbidden, NotFound, Conflict, InsufficientFunds, Unavailable }
public sealed record GoldOutcome(GoldResult Result, Guid? OrderId = null);
public interface IGoldTradingStore
{
    Task<GoldWalletPage> WalletAsync(int userId, int page, CancellationToken ct);
    Task<GoldSettings> SettingsAsync(CancellationToken ct);
    Task<GoldRate?> RateAsync(CancellationToken ct);
    Task<GoldOrderView?> OrderAsync(int actorId, Guid id, bool staff, CancellationToken ct);
    Task<Page<GoldOrderView>> OrdersAsync(int actorId, GoldOrderStatus? status, int? customerId, int page, CancellationToken ct);
    Task<GoldResult> SettingsAsync(int actorId, bool enabled, decimal fee, byte[] version, CancellationToken ct);
    Task<GoldResult> EnableWalletAsync(int actorId, int userId, bool enabled, byte[]? version, CancellationToken ct);
    Task<GoldResult> CreditAsync(int actorId, int userId, Guid operationId, GoldLedgerKind kind, decimal amount, string reason, CancellationToken ct);
    Task<GoldOutcome> QuoteAsync(int actorId, Guid operationId, decimal grams, CancellationToken ct);
    Task<GoldResult> SubmitAsync(int actorId, Guid orderId, CancellationToken ct);
    Task<GoldResult> DecideAsync(int actorId, Guid orderId, GoldOrderStatus decision, byte[] version, string? note, CancellationToken ct);
}
public static class GoldTradingLabels
{
    public static string Status(GoldOrderStatus status) => status switch { GoldOrderStatus.Quote => "پیش‌نمایش؛ ثبت نشده", GoldOrderStatus.Pending => "در انتظار تأیید", GoldOrderStatus.Approved => "تأیید و ثبت نهایی", GoldOrderStatus.Rejected => "رد شده", GoldOrderStatus.Cancelled => "لغو شده", _ => "نامعتبر" };
    public static string Kind(GoldLedgerKind kind) => kind switch { GoldLedgerKind.TomanCredit => "شارژ تومان توسط مدیر", GoldLedgerKind.GoldCredit => "افزایش طلا توسط مدیر", _ => "خرید طلا" };
    public static string Error(GoldResult result) => result switch {
        GoldResult.Conflict => "این درخواست یا تنظیمات تغییر کرده است؛ اطلاعات تازه را بررسی کنید.",
        GoldResult.InsufficientFunds => "اعتبار قابل خرید کافی نیست؛ مبلغ درخواست‌های در انتظار، رزرو شده است.",
        GoldResult.Unavailable => "خرید موقتاً فعال نیست یا نرخ آنلاین تازه و معتبر در دسترس نیست. هیچ مبلغی کسر نشد.",
        GoldResult.Forbidden => "این عملیات برای حساب شما مجاز نیست.",
        _ => "اطلاعات معتبر نیست؛ مبلغ، وزن، توضیح و وضعیت درخواست را بررسی کنید." };
}
