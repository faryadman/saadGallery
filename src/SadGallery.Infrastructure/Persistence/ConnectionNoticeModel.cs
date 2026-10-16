using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Settings;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence.Entities;

namespace SadGallery.Infrastructure.Persistence;

internal static class ConnectionNoticeModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<ConnectionNoticeSetting>(e =>
        {
            e.ToTable("ConnectionNoticeSettings", t => t.HasCheckConstraint("CK_ConnectionNoticeSettings_Singleton", "[Id] = 1"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.OfflineTitle).HasMaxLength(ConnectionNoticeService.TitleLimit).IsRequired();
            e.Property(x => x.OnlineTitle).HasMaxLength(ConnectionNoticeService.TitleLimit).IsRequired();
            e.Property(x => x.OfflineDescription).HasMaxLength(ConnectionNoticeService.DescriptionLimit).IsRequired();
            e.Property(x => x.OnlineDescription).HasMaxLength(ConnectionNoticeService.DescriptionLimit).IsRequired();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
            var defaults = ConnectionNoticeText.Default;
            e.HasData(new { Id = 1, defaults.OfflineTitle, defaults.OfflineDescription, defaults.OnlineTitle, defaults.OnlineDescription });
        });
        builder.Entity<ConnectionNoticeEdit>(e =>
        {
            e.ToTable("ConnectionNoticeEdits"); e.HasKey(x => x.Id);
            e.Property(x => x.Fields).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
