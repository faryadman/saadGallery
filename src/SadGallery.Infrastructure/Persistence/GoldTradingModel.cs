using Microsoft.EntityFrameworkCore;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence.Entities;
namespace SadGallery.Infrastructure.Persistence;

internal static class GoldTradingModel
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<GoldWallet>(e => {
            e.ToTable("GoldWallets", t => t.HasCheckConstraint("CK_GoldWallet_Balances", "[CashIrt] >= 0 AND [CashIrt] <= 999999999999999 AND [ReservedIrt] >= 0 AND [ReservedIrt] <= [CashIrt] AND [Grams750] >= 0 AND [Grams750] <= 999999"));
            e.HasKey(x => x.UserId); e.Property(x => x.UserId).ValueGeneratedNever();
            e.Property(x => x.CashIrt).HasPrecision(18, 0); e.Property(x => x.ReservedIrt).HasPrecision(18, 0); e.Property(x => x.Grams750).HasPrecision(18, 3);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<GoldTradingSetting>(e => {
            e.ToTable("GoldTradingSettings", t => { t.HasCheckConstraint("CK_GoldSettings_Singleton", "[Id] = 1"); t.HasCheckConstraint("CK_GoldSettings_Fee", "([FeePercent] IS NULL AND [Enabled] = 0) OR ([FeePercent] IS NOT NULL AND [FeePercent] BETWEEN 0 AND 100)"); });
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever(); e.Property(x => x.FeePercent).HasPrecision(6, 3); e.Property(x => x.RowVersion).IsRowVersion();
            e.HasData(new { Id = 1, Enabled = false });
        });
        builder.Entity<GoldOrder>(e => {
            e.ToTable("GoldOrders", t => {
                t.HasCheckConstraint("CK_GoldOrder_Amounts", "[Grams750] > 0 AND [Grams750] <= 999999 AND [RateIrt] > 0 AND [RateIrt] <= 999999999999999 AND [FeePercent] BETWEEN 0 AND 100 AND [PrincipalIrt] > 0 AND [FeeIrt] >= 0 AND [TotalIrt] = [PrincipalIrt] + [FeeIrt] AND [TotalIrt] <= 999999999999999 AND [Fineness] = 750");
                t.HasCheckConstraint("CK_GoldOrder_Status", "([Status] = 0 AND [RequestedAtUtc] IS NULL AND [ReviewedAtUtc] IS NULL AND [ReviewerId] IS NULL) OR ([Status] = 1 AND [RequestedAtUtc] IS NOT NULL AND [ReviewedAtUtc] IS NULL AND [ReviewerId] IS NULL) OR ([Status] IN (2,3,4) AND [RequestedAtUtc] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL AND [ReviewerId] IS NOT NULL)");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever(); e.Property(x => x.Grams750).HasPrecision(18, 3); e.Property(x => x.RateIrt).HasPrecision(18, 4); e.Property(x => x.FeePercent).HasPrecision(6, 3);
            e.Property(x => x.PrincipalIrt).HasPrecision(18, 0); e.Property(x => x.FeeIrt).HasPrecision(18, 0); e.Property(x => x.TotalIrt).HasPrecision(18, 0);
            e.Property(x => x.FormulaVersion).HasMaxLength(32).IsRequired(); e.Property(x => x.ReviewNote).HasMaxLength(300); e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<GoldWallet>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MarketRateSnapshot>().WithMany().HasForeignKey(x => x.RateSnapshotId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.RequestedAtUtc }); e.HasIndex(x => new { x.Status, x.RequestedAtUtc });
        });
        builder.Entity<GoldLedgerEntry>(e => {
            e.ToTable("GoldLedger", t => { t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_GoldLedger_Kind", "([Kind] = 1 AND [CashDeltaIrt] > 0 AND [GoldDeltaGrams] = 0 AND [OrderId] IS NULL) OR ([Kind] = 2 AND [CashDeltaIrt] = 0 AND [GoldDeltaGrams] > 0 AND [OrderId] IS NULL) OR ([Kind] = 3 AND [CashDeltaIrt] < 0 AND [GoldDeltaGrams] > 0 AND [OrderId] IS NOT NULL)"); });
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever(); e.Property(x => x.CashDeltaIrt).HasPrecision(18, 0); e.Property(x => x.GoldDeltaGrams).HasPrecision(18, 3); e.Property(x => x.Reason).HasMaxLength(300).IsRequired();
            e.HasOne<GoldWallet>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<GoldOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.OrderId).IsUnique().HasFilter("[OrderId] IS NOT NULL"); e.HasIndex(x => new { x.UserId, x.AtUtc });
        });
    }
    internal static void Guard(SadGalleryDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        if (db.ChangeTracker.Entries<GoldLedgerEntry>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)) throw new InvalidOperationException("Gold ledger is append-only.");
        if (db.ChangeTracker.Entries<GoldWallet>().Any(x => x.State == EntityState.Deleted)) throw new InvalidOperationException("Gold wallets cannot be deleted.");
        foreach (var e in db.ChangeTracker.Entries<GoldOrder>())
        {
            if (e.State == EntityState.Deleted) throw new InvalidOperationException("Gold orders cannot be deleted.");
            if (e.State != EntityState.Modified) continue;
            string[] mutable = ["Status", "RequestedAtUtc", "ReviewedAtUtc", "ReviewerId", "ReviewNote", "RowVersion"];
            if (e.Properties.Any(p => p.IsModified && !mutable.Contains(p.Metadata.Name))) throw new InvalidOperationException("Gold pricing snapshots are immutable.");
            var before = (SadGallery.Domain.Trading.GoldOrderStatus)e.Property(x => x.Status).OriginalValue;
            if (before is not (SadGallery.Domain.Trading.GoldOrderStatus.Quote or SadGallery.Domain.Trading.GoldOrderStatus.Pending)) throw new InvalidOperationException("Final gold orders are immutable.");
        }
    }
}
