using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Administration;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence.Entities;

namespace SadGallery.Infrastructure.Persistence;

internal static class AdministrationAudit
{
    internal static void Capture(SadGalleryDbContext db, AuditActor? actor)
    {
        db.ChangeTracker.DetectChanges();
        if (db.ChangeTracker.Entries<AuditEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit events are append-only.");
        if (actor == null) return; // Background jobs and migrations do not impersonate a user.
        List<AuditEvent> events = [];
        foreach (var e in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
        {
            string? action = null; string[] allowed = [];
            switch (e.Entity)
            {
                case StoreSettings: action = "Settings.Changed"; allowed = ["RatesPaused", "FetchIntervalSeconds"]; break;
                case ConnectionNoticeSetting: action = "ConnectionNotice.Changed"; allowed = ["OfflineTitle", "OfflineDescription", "OnlineTitle", "OnlineDescription"]; break;
                case ApplicationUser: action = "User.AccessChanged"; allowed = ["IsActive"]; break;
                case IdentityUserRole<int>: action = "User.RoleChanged"; allowed = ["UserId", "RoleId"]; break;
                case Product product when e.State == EntityState.Modified:
                    action = e.Property("IsDeleted").IsModified && product.IsDeleted ? "Product.Deleted" : "Product.Changed";
                    allowed = ["Title", "Summary", "Description", "CategoryId", "PricePolicy", "FixedPriceIrt", "WeightGrams", "Karat", "MakingChargePercent", "ProfitPercent", "TaxPercent", "IsInStock", "IsPublished", "IsDeleted", "PriceTotalIrt"];
                    break;
                case ProductCategory when e.State == EntityState.Modified: action = "Category.Changed"; allowed = [nameof(ProductCategory.Name), nameof(ProductCategory.Description), nameof(ProductCategory.DisplayOrder)]; break;
                case ProductImage when e.State == EntityState.Deleted: action = "ProductImage.Deleted"; allowed = ["ProductId"]; break;
                case SupportTicket when e.State == EntityState.Modified:
                    action = e.Property("Status").IsModified ? "Ticket.StatusChanged" : "Ticket.AssignmentChanged"; allowed = ["Status", "AssignedOperatorUserId"]; break;
            }
            if (action == null) continue;
            var fields = allowed.Where(name => e.State != EntityState.Modified || (e.Property(name).IsModified && !Equals(e.Property(name).OriginalValue, e.Property(name).CurrentValue))).ToArray();
            if (fields.Length == 0) continue;
            if (e.Entity is SupportTicket && e.State != EntityState.Modified) continue;
            var key = string.Join(':', e.Metadata.FindPrimaryKey()!.Properties.Select(p => e.Property(p.Name).CurrentValue));
            // Only field names are stored. Values can contain secrets, messages or personal information.
            events.Add(new AuditEvent { ActorUserId = actor.UserId, OccurredAtUtc = DateTimeOffset.UtcNow,
                Action = action, EntityType = e.Metadata.ClrType.Name, EntityId = key,
                Fields = string.Join(',', fields), IpAddress = actor.IpAddress, CorrelationId = actor.CorrelationId });
        }
        db.AddRange(events);
    }
}
