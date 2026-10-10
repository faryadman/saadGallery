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

    // ---- فاز ۴: ویترین محصولات ----

    /// <summary>کالاها (شامل پیش‌نویس‌ها و حذف‌شده‌های نرم).</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>دسته‌بندی کالاها.</summary>
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    /// <summary>تصاویر کالاها (هر تصویر دو فایل دارد: کامل و بندانگشتی).</summary>
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

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

        // ---- فاز ۴: کاتالوگ محصول ----

        builder.Entity<ProductCategory>(entity =>
        {
            entity.ToTable("ProductCategories");
            entity.HasKey(category => category.Id);

            entity.Property(category => category.Name).HasMaxLength(100).IsRequired();
            entity.Property(category => category.Description).HasMaxLength(500);
        });

        builder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(product => product.Id);

            entity.Property(product => product.Title).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Summary).HasMaxLength(400);
            // توضیحاتِ بلند مجاز است؛ طول دقیق در لایه Application محدود می‌شود (ProductOptions).
            entity.Property(product => product.Description).HasMaxLength(8000);

            // مبالغ به تومان (Irt) با دو رقم اعشار کافی است؛ محاسباتِ خودکار
            // از پیش روی «تومانِ کامل» گرد می‌شوند (ProductPriceMath).
            entity.Property(product => product.FixedPriceIrt).HasPrecision(18, 2);
            entity.Property(product => product.PriceTotalIrt).HasPrecision(18, 2);
            entity.Property(product => product.PriceGoldValueIrt).HasPrecision(18, 2);
            entity.Property(product => product.PriceMakingIrt).HasPrecision(18, 2);
            entity.Property(product => product.PriceProfitIrt).HasPrecision(18, 2);
            entity.Property(product => product.PriceTaxIrt).HasPrecision(18, 2);
            entity.Property(product => product.PriceRateAmountIrt).HasPrecision(18, 2);

            entity.Property(product => product.WeightGrams).HasPrecision(10, 3);
            entity.Property(product => product.PriceWeightGrams).HasPrecision(10, 3);
            entity.Property(product => product.Karat).HasPrecision(5, 2);
            entity.Property(product => product.PriceKarat).HasPrecision(5, 2);

            entity.Property(product => product.MakingChargePercent).HasPrecision(6, 3);
            entity.Property(product => product.ProfitPercent).HasPrecision(6, 3);
            entity.Property(product => product.TaxPercent).HasPrecision(6, 3);
            entity.Property(product => product.PriceMakingPercent).HasPrecision(6, 3);
            entity.Property(product => product.PriceProfitPercent).HasPrecision(6, 3);
            entity.Property(product => product.PriceTaxPercent).HasPrecision(6, 3);

            entity.Property(product => product.PriceFormulaVersion).HasMaxLength(32);
            entity.Property(product => product.PriceReason).HasMaxLength(500);

            // پرتکرارترین کوئریٔ سایت: «آخرین کالاهای منتشرشده»
            entity.HasIndex(product => new { product.IsPublished, product.IsDeleted, product.PublishedAtUtc });
            entity.HasIndex(product => product.CreatedAtUtc);

            entity.HasOne(product => product.Category)
                .WithMany(category => category.Products)
                .HasForeignKey(product => product.CategoryId)
                // حذفِ دسته نباید کالاها را نابود کند
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("ProductImages");
            entity.HasKey(image => image.Id);

            entity.Property(image => image.StoredFileName).HasMaxLength(64).IsRequired();
            entity.Property(image => image.ThumbnailFileName).HasMaxLength(64).IsRequired();
            entity.Property(image => image.DetectedFormat).HasMaxLength(16).IsRequired();
            entity.Property(image => image.OriginalFileName).HasMaxLength(200);
            entity.Property(image => image.ClaimedContentType).HasMaxLength(100);

            entity.HasIndex(image => image.ProductId);

            entity.HasOne(image => image.Product)
                .WithMany(product => product.Images)
                .HasForeignKey(image => image.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // همه زمان‌ها UTC هستند (docs/DATA_MODEL.md §1). Collation فارسی روی دیتابیس تنظیم می‌شود، نه ستون‌ها.
    }
}
