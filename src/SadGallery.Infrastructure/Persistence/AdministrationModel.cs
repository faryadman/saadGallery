using Microsoft.EntityFrameworkCore;
using SadGallery.Infrastructure.Persistence.Entities;

namespace SadGallery.Infrastructure.Persistence;

internal static class AdministrationModel
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<StoreSettings>(e =>
        {
            e.ToTable("StoreSettings", t =>
            {
                t.HasCheckConstraint("CK_StoreSettings_Singleton", "[Id] = 1");
                t.HasCheckConstraint("CK_StoreSettings_Interval", "[FetchIntervalSeconds] IS NULL OR [FetchIntervalSeconds] BETWEEN 10 AND 3600");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<SadGallery.Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasData(new { Id = 1, RatesPaused = false });
        });
        builder.Entity<AuditEvent>(e =>
        {
            e.ToTable("AuditEvents"); e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(80).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Fields).HasMaxLength(1000).IsRequired();
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.OccurredAtUtc); e.HasIndex(x => new { x.ActorUserId, x.OccurredAtUtc });
        });
    }
}
