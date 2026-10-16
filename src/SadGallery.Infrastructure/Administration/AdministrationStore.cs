using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Administration;
using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Tickets;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;

namespace SadGallery.Infrastructure.Administration;

public sealed class AdministrationStore(SadGalleryDbContext db, IClock clock, RateOptions options,
    IRateProvider provider, RateFreshnessPolicy freshness) : IAdministrationStore
{
    private IQueryable<int> AdminIds() => from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
        where role.Name == RoleNames.Admin select ur.UserId;
    private Task<bool> IsAdmin(int id, CancellationToken ct) => db.Users.AnyAsync(u => u.Id == id && u.IsActive && AdminIds().Contains(u.Id), ct);
    private int EffectiveInterval(int? value) => Math.Clamp(Math.Max(options.FetchIntervalSeconds, value ?? options.FetchIntervalSeconds), 10, 3600);
    public async Task<RuntimeRateSettings?> GetAsync(CancellationToken ct)
    {
        var s = await db.Set<StoreSettings>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
        return s == null ? null : new(s.RatesPaused, EffectiveInterval(s.FetchIntervalSeconds), s.RowVersion, s.UpdatedAtUtc, s.UpdatedByUserId);
    }
    public async Task<AdminSaveResult> SaveSettingsAsync(int actorId, bool paused, int interval, byte[] version, CancellationToken ct)
    {
        if (!await IsAdmin(actorId, ct)) return AdminSaveResult.Forbidden;
        if (!AdminRules.ValidInterval(interval, options.FetchIntervalSeconds) || version.Length != 8) return AdminSaveResult.Invalid;
        var s = await db.Set<StoreSettings>().SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (s == null) return AdminSaveResult.NotFound;
        await db.Entry(s).ReloadAsync(ct);
        if (!version.SequenceEqual(s.RowVersion)) return AdminSaveResult.Conflict;
        if (s.RatesPaused == paused && EffectiveInterval(s.FetchIntervalSeconds) == interval) return AdminSaveResult.Unchanged;
        s.RatesPaused = paused; s.FetchIntervalSeconds = interval; s.UpdatedAtUtc = clock.UtcNow; s.UpdatedByUserId = actorId;
        db.Entry(s).Property(x => x.RowVersion).OriginalValue = version;
        try { await db.SaveChangesAsync(ct); return AdminSaveResult.Saved; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return AdminSaveResult.Conflict; }
    }
    private IQueryable<ManagedUser> UserQuery(IQueryable<ApplicationUser>? users = null) => (users ?? db.Users).AsNoTracking().Select(u => new ManagedUser(u.Id,
        u.DisplayName ?? "", u.IsActive,
        (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == u.Id select r.Name!).Count() == 1
            ? (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == u.Id select r.Name!).First() : "Multiple",
        u.ConcurrencyStamp!, u.CreatedAtUtc, u.LastLoginAtUtc));
    public async Task<Page<ManagedUser>> UsersAsync(int page, string? search, CancellationToken ct)
    {
        page = AdminRules.PageNumber(page);
        IQueryable<ApplicationUser> query = db.Users; search = search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            if (search.Length > 100) search = search[..100];
            var term = search;
            if (int.TryParse(term, out var id)) query = query.Where(x => x.Id == id);
            else query = query.Where(x => x.DisplayName != null && x.DisplayName.Contains(term));
        }
        var selected = query.OrderByDescending(x => x.Id).Skip((page - 1) * 25).Take(25);
        return new(await UserQuery(selected).ToListAsync(ct), page, await query.CountAsync(ct));
    }
    public Task<ManagedUser?> UserAsync(int id, CancellationToken ct) => UserQuery(db.Users.Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
    public async Task<AdminSaveResult> SaveUserAsync(int actorId, int id, string role, bool active, string version, CancellationToken ct)
    {
        if (!AdminRules.ValidRole(role) || string.IsNullOrWhiteSpace(version) || version.Length > 64) return AdminSaveResult.Invalid;
        // All user-access mutations share a transaction-owned SQL lock, including cross-instance requests.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'SadGallery.AdminUserAccess', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51000, 'User access lock unavailable', 1;", ct);
            if (!await IsAdmin(actorId, ct)) return AdminSaveResult.Forbidden;
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (user == null) return AdminSaveResult.NotFound;
            if (user.ConcurrencyStamp != version) return AdminSaveResult.Conflict;
            var joins = await db.UserRoles.Where(x => x.UserId == id).ToListAsync(ct);
            var desired = await db.Roles.SingleOrDefaultAsync(x => x.Name == role, ct);
            if (desired == null) return AdminSaveResult.Invalid;
            if (user.IsActive == active && joins.Count == 1 && joins[0].RoleId == desired.Id) return AdminSaveResult.Unchanged;
            var isAdmin = await AdminIds().ContainsAsync(id, ct);
            var adminCount = isAdmin ? await db.Users.CountAsync(u => u.IsActive && AdminIds().Contains(u.Id), ct) : 0;
            var guard = AdminRules.AccessChangeGuard(actorId, id, user.IsActive, isAdmin, active, role, adminCount);
            if (guard != null) return guard.Value;
            db.UserRoles.RemoveRange(joins.Where(x => x.RoleId != desired.Id));
            if (joins.All(x => x.RoleId != desired.Id)) db.UserRoles.Add(new IdentityUserRole<int> { UserId = id, RoleId = desired.Id });
            user.IsActive = active;
            // Same stamp mechanism as Identity.UserManager.UpdateSecurityStampAsync.
            user.SecurityStamp = Guid.NewGuid().ToString(); user.ConcurrencyStamp = Guid.NewGuid().ToString();
            db.Entry(user).Property(x => x.ConcurrencyStamp).OriginalValue = version;
            try
            {
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return AdminSaveResult.Saved;
            }
            catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return AdminSaveResult.Conflict; }
        });
    }
    public async Task<OperationsDashboard> DashboardAsync(CancellationToken ct)
    {
        var settings = await GetAsync(ct) ?? throw new InvalidOperationException("Administration migration required.");
        // Highest Id per asset, including anomalous latest samples, bounded independently of history length.
        var ids = db.MarketRates.GroupBy(x => x.AssetCode).Select(x => x.Max(y => y.Id));
        var rows = await db.MarketRates.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.AssetCode)
            .Select(x => new { x.AssetCode, x.QuotedAtUtc, x.FetchedAtUtc, x.IsAnomalySuspected, x.Quality }).Take(100).ToListAsync(ct);
        var now = clock.UtcNow;
        var health = rows.Select(x => new RateHealth(x.AssetCode, x.QuotedAtUtc, x.FetchedAtUtc,
            x.IsAnomalySuspected || x.Quality == RateQuality.Invalid ? RateQuality.Invalid : freshness.Evaluate(x.QuotedAtUtc, now), x.IsAnomalySuspected)).ToList();
        var last = await db.MarketPriceFetchRuns.AsNoTracking().OrderByDescending(x => x.Id)
            .Select(x => new JobHealth(x.StartedAtUtc, x.FinishedAtUtc, x.Outcome, x.AcceptedCount, x.FlaggedCount, x.RejectedCount)).FirstOrDefaultAsync(ct);
        return new(await db.Set<SupportTicket>().CountAsync(x => x.Status != TicketStatus.Closed, ct),
            await db.Set<SupportTicket>().CountAsync(x => x.Status == TicketStatus.AwaitingCustomer, ct),
            await db.Products.CountAsync(x => !x.IsDeleted && x.IsPublished, ct), await db.Products.CountAsync(x => !x.IsDeleted && !x.IsPublished, ct),
            AdminRules.SourceTitle(options.Provider), provider.IsConfigured, settings.Paused,
            await db.MarketRates.Select(x => (DateTimeOffset?)x.FetchedAtUtc).MaxAsync(ct), last, health);
    }
    private IQueryable<AuditRow> AuditQuery() => db.Set<AuditEvent>().AsNoTracking().OrderByDescending(x => x.Id)
        .Select(x => new AuditRow(x.Id, x.OccurredAtUtc, x.ActorUserId, x.Action, x.EntityType, x.EntityId, x.Fields, x.IpAddress, x.CorrelationId));
    public async Task<Page<AuditRow>> AuditAsync(int page, CancellationToken ct)
    {
        page = AdminRules.PageNumber(page);
        return new(await AuditQuery().Skip((page - 1) * 25).Take(25).ToListAsync(ct), page, await db.Set<AuditEvent>().CountAsync(ct));
    }
    public async Task<IReadOnlyList<AuditRow>> AuditExportAsync(CancellationToken ct) => await AuditQuery().Take(500).ToListAsync(ct);
}
