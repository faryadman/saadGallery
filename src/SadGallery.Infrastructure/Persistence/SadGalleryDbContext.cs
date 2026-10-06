using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SadGallery.Infrastructure.Identity;

namespace SadGallery.Infrastructure.Persistence;

/// <summary>
/// DbContext واحد سامانه.
/// تصمیم (ADR-0003): بدون لایه Repository عمومی؛ سرویس‌های Application مستقیماً از این DbContext
/// استفاده می‌کنند. Controllerها هرگز به آن دسترسی ندارند.
/// جدول‌های بازار/محصول/تیکت در فازهای ۲، ۴ و ۵ با Migration افزوده می‌شوند.
/// </summary>
public class SadGalleryDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public SadGalleryDbContext(DbContextOptions<SadGalleryDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.DisplayName).HasMaxLength(100);
            entity.Property(u => u.CreatedAtUtc).IsRequired();
            entity.Property(u => u.IsActive).HasDefaultValue(true).IsRequired();

            // کوئری پنل ادمین: کاربران فعال/غیرفعال
            entity.HasIndex(u => u.IsActive);
        });

        // همه زمان‌ها UTC هستند (docs/DATA_MODEL.md §1). Collation فارسی روی دیتابیس تنظیم می‌شود، نه ستون‌ها.
    }
}
