using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Settings;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;

namespace SadGallery.Infrastructure.Settings;

public sealed class ConnectionNoticeStore(SadGalleryDbContext db, IClock clock) : IConnectionNoticeStore
{
    private static ConnectionNoticeText Text(ConnectionNoticeSetting x) => new(x.OfflineTitle, x.OfflineDescription, x.OnlineTitle, x.OnlineDescription);
    public async Task<ConnectionNoticeSettings?> GetAsync(CancellationToken ct)
    {
        var x = await db.Set<ConnectionNoticeSetting>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
        return x == null ? null : new(Text(x), x.RowVersion, x.UpdatedAtUtc, x.UpdatedByUserId);
    }
    public async Task<NoticeSaveResult> SaveAsync(int actorUserId, ConnectionNoticeText text, byte[] version, CancellationToken ct)
    {
        var allowed = await (from user in db.Users join ur in db.UserRoles on user.Id equals ur.UserId
            join role in db.Roles on ur.RoleId equals role.Id
            where user.Id == actorUserId && user.IsActive && role.Name == RoleNames.Admin select user.Id).AnyAsync(ct);
        if (!allowed) return NoticeSaveResult.Forbidden;
        var row = await db.Set<ConnectionNoticeSetting>().SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (row == null) return NoticeSaveResult.Unavailable;
        await db.Entry(row).ReloadAsync(ct);
        if (db.Entry(row).State == EntityState.Detached) return NoticeSaveResult.Unavailable;
        if (!row.RowVersion.SequenceEqual(version)) return NoticeSaveResult.Conflict;
        var previous = Text(row);
        if (previous == text) return NoticeSaveResult.Unchanged;
        List<string> fields = [];
        if (row.OfflineTitle != text.OfflineTitle) fields.Add(nameof(text.OfflineTitle));
        if (row.OfflineDescription != text.OfflineDescription) fields.Add(nameof(text.OfflineDescription));
        if (row.OnlineTitle != text.OnlineTitle) fields.Add(nameof(text.OnlineTitle));
        if (row.OnlineDescription != text.OnlineDescription) fields.Add(nameof(text.OnlineDescription));
        row.OfflineTitle = text.OfflineTitle; row.OfflineDescription = text.OfflineDescription;
        row.OnlineTitle = text.OnlineTitle; row.OnlineDescription = text.OnlineDescription;
        var now = clock.UtcNow;
        row.UpdatedAtUtc = now; row.UpdatedByUserId = actorUserId;
        db.Entry(row).Property(x => x.RowVersion).OriginalValue = version;
        db.Add(new ConnectionNoticeEdit { ActorUserId = actorUserId, CreatedAtUtc = now, Fields = string.Join(",", fields) });
        try { await db.SaveChangesAsync(ct); return NoticeSaveResult.Saved; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return NoticeSaveResult.Conflict; }
    }
}
