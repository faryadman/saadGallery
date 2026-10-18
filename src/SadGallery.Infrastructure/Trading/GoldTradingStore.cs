using System.Data;
using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Administration;
using SadGallery.Application.Market;
using SadGallery.Application.Trading;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Trading;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;
namespace SadGallery.Infrastructure.Trading;

public sealed class GoldTradingStore(SadGalleryDbContext db, IClock clock, RateOptions options, RateFreshnessPolicy freshness) : IGoldTradingStore
{
    private IQueryable<int> RoleIds(params string[] roles) => from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where roles.Contains(r.Name!) select ur.UserId;
    private Task<bool> Active(int id, CancellationToken ct) => db.Users.AnyAsync(x => x.Id == id && x.IsActive, ct);
    private Task<bool> Staff(int id, bool admin, CancellationToken ct)
    {
        var ids = RoleIds(admin ? [RoleNames.Admin] : [RoleNames.Admin, RoleNames.Operator]);
        return db.Users.AnyAsync(x => x.Id == id && x.IsActive && ids.Contains(id), ct);
    }
    private Task<bool> Customer(int id, CancellationToken ct)
    {
        var customers = RoleIds(RoleNames.Customer); var staff = RoleIds(RoleNames.Admin, RoleNames.Operator);
        return db.Users.AnyAsync(x => x.Id == id && x.IsActive && customers.Contains(id) && !staff.Contains(id), ct);
    }
    private IQueryable<GoldOrderView> OrderQuery(IQueryable<GoldOrder> orders) => from o in orders.AsNoTracking() join u in db.Users on o.UserId equals u.Id
        select new GoldOrderView(o.Id, o.UserId, u.DisplayName ?? "", o.Status, o.Grams750, o.RateIrt, o.FeePercent, o.PrincipalIrt, o.FeeIrt, o.TotalIrt,
            o.QuotedAtUtc, o.ExpiresAtUtc, o.RequestedAtUtc, o.ReviewedAtUtc, o.ReviewerId, o.ReviewNote, o.RowVersion);
    public async Task<GoldSettings> SettingsAsync(CancellationToken ct)
    {
        var s = await db.Set<GoldTradingSetting>().AsNoTracking().SingleAsync(ct);
        return new(s.Enabled, s.FeePercent, s.RowVersion);
    }
    public async Task<GoldRate?> RateAsync(CancellationToken ct)
    {
        // No display cache, no conversion, no older valid row hidden behind a newer invalid row.
        if (!options.IsTgnProvider) return null;
        var runtime = await db.Set<StoreSettings>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (runtime == null || runtime.RatesPaused) return null;
        var rate = await db.MarketRates.AsNoTracking().Where(x => x.AssetCode == "GOLD_GRAM_18").OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        return rate != null && Eligible(rate) ? new(rate.Id, rate.Amount, rate.QuotedAtUtc, rate.FetchedAtUtc, rate.ProviderId) : null;
    }
    private bool Eligible(MarketRateSnapshot r) => GoldRateRules.Eligible(new(new(r.Id, r.Amount, r.QuotedAtUtc, r.FetchedAtUtc, r.ProviderId), r.AssetCode, r.QuoteUnit, r.Quality, r.IsAnomalySuspected), clock.UtcNow, freshness);
    private static GoldWalletBalance Balance(GoldWallet w) => new(w.CashIrt, w.ReservedIrt, w.Grams750);
    private static void Apply(GoldWallet w, GoldWalletBalance balance) { w.CashIrt = balance.CashIrt; w.ReservedIrt = balance.ReservedIrt; w.Grams750 = balance.Grams750; }
    public async Task<GoldWalletPage> WalletAsync(int userId, int page, CancellationToken ct)
    {
        page = AdminRules.PageNumber(page);
        var wallet = await (from w in db.Set<GoldWallet>().AsNoTracking() join u in db.Users on w.UserId equals u.Id where w.UserId == userId
            select new GoldWalletView(w.UserId, u.DisplayName ?? "", w.Enabled, w.CashIrt, w.ReservedIrt, w.Grams750, w.RowVersion)).SingleOrDefaultAsync(ct);
        var orders = db.Set<GoldOrder>().Where(x => x.UserId == userId && x.Status != GoldOrderStatus.Quote);
        var ledger = db.Set<GoldLedgerEntry>().AsNoTracking().Where(x => x.UserId == userId);
        return new(wallet, await SettingsAsync(ct), await RateAsync(ct),
            new(await OrderQuery(orders.OrderByDescending(x => x.RequestedAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * 25).Take(25)).ToListAsync(ct), page, await orders.CountAsync(ct)),
            new(await ledger.OrderByDescending(x => x.AtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * 25).Take(25)
                .Select(x => new GoldLedgerView(x.Id, x.Kind, x.CashDeltaIrt, x.GoldDeltaGrams, x.Reason, x.ActorId, x.AtUtc, x.OrderId)).ToListAsync(ct), page, await ledger.CountAsync(ct)));
    }
    public async Task<GoldOrderView?> OrderAsync(int actorId, Guid id, bool staff, CancellationToken ct)
    {
        if (!await Active(actorId, ct) || (staff && !await Staff(actorId, false, ct))) return null;
        return await OrderQuery(db.Set<GoldOrder>().Where(x => x.Id == id && (staff || x.UserId == actorId))).SingleOrDefaultAsync(ct);
    }
    public async Task<Page<GoldOrderView>> OrdersAsync(int actorId, GoldOrderStatus? status, int? customerId, int page, CancellationToken ct)
    {
        page = AdminRules.PageNumber(page);
        if (!await Staff(actorId, false, ct)) return new([], page, 0);
        var query = db.Set<GoldOrder>().Where(x => x.Status != GoldOrderStatus.Quote);
        if (status is {} filter) query = query.Where(x => x.Status == filter);
        if (customerId is {} customer) query = query.Where(x => x.UserId == customer);
        return new(await OrderQuery(query.OrderByDescending(x => x.RequestedAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * 25).Take(25)).ToListAsync(ct), page, await query.CountAsync(ct));
    }
    private async Task<GoldResult> Write(Func<Task<GoldResult>> operation, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            // Same first lock as access administration: role revocation and financial writes cannot interleave.
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sp_getapplock @Resource='SadGallery.AdminUserAccess', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000, 'Access lock unavailable', 1; EXEC @r=sp_getapplock @Resource='SadGallery.GoldTrading', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000, 'Trading lock unavailable', 1;", ct);
            var result = await operation();
            if (result != GoldResult.Saved) return result;
            try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result; }
            catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return GoldResult.Conflict; }
        });
    }
    public Task<GoldResult> SettingsAsync(int actorId, bool enabled, decimal fee, byte[] version, CancellationToken ct) => Write(async () => {
        if (!await Staff(actorId, true, ct)) return GoldResult.Forbidden;
        if (!GoldPurchaseMath.ValidFee(fee) || version.Length != 8) return GoldResult.Invalid;
        var setting = await db.Set<GoldTradingSetting>().SingleAsync(ct);
        if (!setting.RowVersion.SequenceEqual(version)) return GoldResult.Conflict;
        setting.Enabled = enabled; setting.FeePercent = fee; return GoldResult.Saved;
    }, ct);
    public Task<GoldResult> EnableWalletAsync(int actorId, int userId, bool enabled, byte[]? version, CancellationToken ct) => Write(async () => {
        if (!await Staff(actorId, true, ct)) return GoldResult.Forbidden;
        var wallet = await db.Set<GoldWallet>().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (wallet == null && !enabled) return GoldResult.Invalid;
        if (enabled && !await Customer(userId, ct)) return GoldResult.Invalid;
        if (wallet == null)
        {
            if (version is { Length: > 0 }) return GoldResult.Conflict;
            db.Add(new GoldWallet { UserId = userId, Enabled = true });
        }
        else
        {
            if (version is not { Length: 8 } || !wallet.RowVersion.SequenceEqual(version)) return GoldResult.Conflict;
            wallet.Enabled = enabled;
        }
        return GoldResult.Saved;
    }, ct);
    public Task<GoldResult> CreditAsync(int actorId, int userId, Guid operationId, GoldLedgerKind kind, decimal amount, string reason, CancellationToken ct) => Write(async () => {
        if (!await Staff(actorId, true, ct)) return GoldResult.Forbidden;
        reason = reason?.Trim() ?? "";
        if (operationId == Guid.Empty || reason.Length is < 3 or > 300 || (kind != GoldLedgerKind.TomanCredit && kind != GoldLedgerKind.GoldCredit)
            || (kind == GoldLedgerKind.TomanCredit ? !GoldPurchaseMath.ValidMoney(amount) : !GoldPurchaseMath.ValidGrams(amount))) return GoldResult.Invalid;
        var prior = await db.Set<GoldLedgerEntry>().SingleOrDefaultAsync(x => x.Id == operationId, ct);
        var cash = kind == GoldLedgerKind.TomanCredit ? amount : 0; var gold = kind == GoldLedgerKind.GoldCredit ? amount : 0;
        if (prior != null) return prior.UserId == userId && prior.ActorId == actorId && prior.Kind == kind && prior.CashDeltaIrt == cash && prior.GoldDeltaGrams == gold && prior.Reason == reason ? GoldResult.Saved : GoldResult.Conflict;
        var wallet = await db.Set<GoldWallet>().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (wallet == null) return GoldResult.NotFound;
        if (!wallet.Enabled || !await Customer(userId, ct)) return GoldResult.Invalid;
        if (Balance(wallet).Credit(kind, amount) is not {} credited) return GoldResult.Invalid;
        Apply(wallet, credited);
        db.Add(new GoldLedgerEntry { Id = operationId, UserId = userId, ActorId = actorId, Kind = kind, CashDeltaIrt = cash, GoldDeltaGrams = gold, Reason = reason, AtUtc = clock.UtcNow });
        return GoldResult.Saved;
    }, ct);
    public async Task<GoldOutcome> QuoteAsync(int actorId, Guid operationId, decimal grams, CancellationToken ct)
    {
        var result = await Write(async () => {
            if (!await Customer(actorId, ct)) return GoldResult.Forbidden;
            if (operationId == Guid.Empty || !GoldPurchaseMath.ValidGrams(grams)) return GoldResult.Invalid;
            var prior = await db.Set<GoldOrder>().SingleOrDefaultAsync(x => x.Id == operationId, ct);
            if (prior != null) return prior.UserId == actorId && prior.Grams750 == grams ? GoldResult.Saved : GoldResult.Conflict;
            var wallet = await db.Set<GoldWallet>().SingleOrDefaultAsync(x => x.UserId == actorId && x.Enabled, ct);
            if (wallet == null) return GoldResult.Forbidden;
            var setting = await SettingsAsync(ct); var rate = await RateAsync(ct);
            if (!setting.Enabled || setting.FeePercent is not {} fee || rate == null) return GoldResult.Unavailable;
            var cost = GoldPurchaseMath.Calculate(grams, rate.AmountIrt, fee);
            if (cost == null || wallet.Grams750 + grams > GoldPurchaseMath.MaxGrams) return GoldResult.Invalid;
            if (wallet.CashIrt - wallet.ReservedIrt < cost.TotalIrt) return GoldResult.InsufficientFunds;
            var maxAge = GoldRateRules.MaximumAge(freshness);
            var expiry = new[] { clock.UtcNow.AddMinutes(2), rate.QuotedAtUtc.Add(maxAge), rate.FetchedAtUtc.Add(maxAge) }.Min();
            db.Add(new GoldOrder { Id = operationId, UserId = actorId, RateSnapshotId = rate.SnapshotId, Grams750 = grams,
                RateIrt = rate.AmountIrt, FeePercent = fee, PrincipalIrt = cost.PrincipalIrt, FeeIrt = cost.FeeIrt, TotalIrt = cost.TotalIrt, QuotedAtUtc = rate.QuotedAtUtc, ExpiresAtUtc = expiry });
            return GoldResult.Saved;
        }, ct);
        return new(result, result == GoldResult.Saved ? operationId : null);
    }
    public Task<GoldResult> SubmitAsync(int actorId, Guid orderId, CancellationToken ct) => Write(async () => {
        if (!await Customer(actorId, ct)) return GoldResult.Forbidden;
        var order = await db.Set<GoldOrder>().SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == actorId, ct);
        if (order == null) return GoldResult.NotFound;
        // The immutable order id is the idempotency key even after review or an uncertain HTTP response.
        if (order.Status != GoldOrderStatus.Quote) return GoldResult.Saved;
        var wallet = await db.Set<GoldWallet>().SingleAsync(x => x.UserId == actorId, ct);
        if (!wallet.Enabled) return GoldResult.Forbidden;
        var settings = await SettingsAsync(ct);
        var snapshot = await db.MarketRates.AsNoTracking().SingleAsync(x => x.Id == order.RateSnapshotId, ct);
        if (!settings.Enabled || order.ExpiresAtUtc <= clock.UtcNow || !Eligible(snapshot) || await RateAsync(ct) == null) return GoldResult.Unavailable;
        if (Balance(wallet).Reserve(order.TotalIrt) is not {} reserved) return GoldResult.InsufficientFunds;
        Apply(wallet, reserved);
        order.Status = GoldOrderStatus.Pending; order.RequestedAtUtc = clock.UtcNow;
        return GoldResult.Saved;
    }, ct);
    public Task<GoldResult> DecideAsync(int actorId, Guid orderId, GoldOrderStatus decision, byte[] version, string? note, CancellationToken ct) => Write(async () => {
        if (!await Active(actorId, ct)) return GoldResult.Forbidden;
        if (decision is not (GoldOrderStatus.Approved or GoldOrderStatus.Rejected or GoldOrderStatus.Cancelled) || version.Length != 8 || (note?.Trim().Length ?? 0) > 300) return GoldResult.Invalid;
        var staff = await Staff(actorId, false, ct);
        var order = await db.Set<GoldOrder>().SingleOrDefaultAsync(x => x.Id == orderId && (decision == GoldOrderStatus.Cancelled ? x.UserId == actorId : staff), ct);
        if (order == null) return GoldResult.NotFound;
        if (decision != GoldOrderStatus.Cancelled && (!staff || order.UserId == actorId)) return GoldResult.Forbidden;
        if (order.Status == decision) return GoldResult.Saved;
        if (order.Status != GoldOrderStatus.Pending || !order.RowVersion.SequenceEqual(version)) return GoldResult.Conflict;
        if (decision == GoldOrderStatus.Rejected && string.IsNullOrWhiteSpace(note)) return GoldResult.Invalid;
        var wallet = await db.Set<GoldWallet>().SingleAsync(x => x.UserId == order.UserId, ct);
        if (Balance(wallet).Release(order.TotalIrt) is not {} released) return GoldResult.Conflict;
        if (decision == GoldOrderStatus.Approved)
        {
            if (!wallet.Enabled || !await Customer(order.UserId, ct)) return GoldResult.Forbidden;
            if (Balance(wallet).Purchase(order.TotalIrt, order.Grams750) is not {} purchased) return GoldResult.Invalid;
            Apply(wallet, purchased);
            db.Add(new GoldLedgerEntry { Id = order.Id, UserId = order.UserId, ActorId = actorId, OrderId = order.Id, Kind = GoldLedgerKind.Purchase,
                CashDeltaIrt = -order.TotalIrt, GoldDeltaGrams = order.Grams750, Reason = "خرید تأییدشده با نرخ و کارمزد زمان درخواست", AtUtc = clock.UtcNow });
        }
        else Apply(wallet, released);
        order.Status = decision; order.ReviewedAtUtc = clock.UtcNow; order.ReviewerId = actorId; order.ReviewNote = note?.Trim();
        return GoldResult.Saved;
    }, ct);
}
