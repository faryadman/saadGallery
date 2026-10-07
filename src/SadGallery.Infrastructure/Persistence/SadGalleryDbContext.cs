using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence.Entities;

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

    /// <summary>تاریخچه نرخ‌های بازار (فاز ۲ — افزودنی و تغییرناپذیر).</summary>
    public DbSet<MarketRateSnapshot> MarketRates => Set<MarketRateSnapshot>();

    /// <summary>رکورد هر اجرای دریافت نرخ (پایش سلامت منبع).</summary>
    public DbSet<MarketPriceFetchRun> MarketPriceFetchRuns => Set<MarketPriceFetchRun>();

    /// <summary>اجاره تک‌ردیفی جلوگیری از اجرای هم‌زمان چند نمونه‌ای.</summary>
    public DbSet<MarketFetchLease> MarketFetchLeases => Set<MarketFetchLease>();

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

        builder.Entity<MarketRateSnapshot>(entity =>
        {
            entity.ToTable("MarketRates");
            entity.HasKey(rate => rate.Id);

            entity.Property(rate => rate.AssetCode).HasMaxLength(64).IsRequired();
            entity.Property(rate => rate.ProviderId).HasMaxLength(64).IsRequired();
            entity.Property(rate => rate.Amount).HasPrecision(18, 4);
            entity.Property(rate => rate.ProviderRawValue).HasPrecision(18, 4);
            entity.Property(rate => rate.ScaleApplied).HasPrecision(18, 6);
            entity.Property(rate => rate.QuoteUnit).IsRequired();
            entity.Property(rate => rate.Quality).IsRequired();
            entity.Property(rate => rate.QuotedAtUtc).IsRequired();
            entity.Property(rate => rate.FetchedAtUtc).IsRequired();
            entity.Property(rate => rate.CreatedAtUtc).IsRequired();

            // «آخرین نرخ هر دارایی» = بزرگ‌ترین شناسه؛ این ایندکس کوئری تاریخچه را هم پوشش می‌دهد.
            entity.HasIndex(rate => new { rate.AssetCode, rate.Id });
            entity.HasIndex(rate => rate.FetchedAtUtc);

            entity.HasOne<MarketPriceFetchRun>()
                .WithMany()
                .HasForeignKey(rate => rate.FetchRunId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MarketPriceFetchRun>(entity =>
        {
            entity.ToTable("MarketRateFetchRuns");
            entity.HasKey(run => run.Id);

            entity.Property(run => run.ProviderId).HasMaxLength(64).IsRequired();
            entity.Property(run => run.Trigger).HasMaxLength(32).IsRequired();
            entity.Property(run => run.StartedAtUtc).IsRequired();
            entity.Property(run => run.ErrorCode).HasMaxLength(64);
            entity.Property(run => run.Notes).HasMaxLength(512);

            entity.HasIndex(run => run.StartedAtUtc);
        });

        builder.Entity<MarketFetchLease>(entity =>
        {
            entity.ToTable("MarketFetchLease");
            entity.HasKey(lease => lease.Id);

            entity.Property(lease => lease.OwnerId).HasMaxLength(64);

            // رکورد تک‌تایی با شناسه ۱ — به‌صورت داده اولیه در مهاجرت ایجاد می‌شود.
            entity.HasData(new MarketFetchLease
            {
                Id = 1,
                OwnerId = null,
                AcquiredAtUtc = DateTimeOffset.UnixEpoch,
                ExpiresAtUtc = DateTimeOffset.UnixEpoch,
            });
        });

        // همه زمان‌ها UTC هستند (docs/DATA_MODEL.md §1). Collation فارسی روی دیتابیس تنظیم می‌شود، نه ستون‌ها.
    }
}
