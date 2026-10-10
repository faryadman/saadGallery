using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Catalog;
using SadGallery.Domain.Enums;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;

namespace SadGallery.Infrastructure.Catalog;

/// <summary>
/// پیاده‌سازی دسترسی دادهٔ کاتالوگ با EF Core.
/// </summary>
/// <remarks>
/// کوئری‌ها در اینجا می‌مانند و در کنترلر پراکنده نمی‌شوند (قاعدهٔ جبرانی ADR-0003).
/// </remarks>
public sealed class ProductStore : IProductStore
{
    private readonly SadGalleryDbContext _db;

    public ProductStore(SadGalleryDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    // ================= نمای عمومی =================

    public async Task<IReadOnlyList<ProductRecord>> GetPublishedAsync(int take, CancellationToken cancellationToken)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Include(product => product.Category)
            // فیلترِ انتشار در خودِ کوئری است، نه در حافظه: محصولِ منتشرنشده
            // حتی یک بار هم از دیتابیس بیرون نمی‌آید.
            .Where(product => product.IsPublished && !product.IsDeleted)
            .OrderByDescending(product => product.PublishedAtUtc ?? product.CreatedAtUtc)
            .ThenByDescending(product => product.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        return products.Select(Map).ToList();
    }

    public async Task<ProductRecord?> GetPublishedByIdAsync(int id, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(item => item.Category)
            .FirstOrDefaultAsync(item => item.Id == id && item.IsPublished && !item.IsDeleted, cancellationToken);

        return product is null ? null : Map(product);
    }

    public Task<IReadOnlyList<ProductImageRecord>> GetImagesAsync(int productId, CancellationToken cancellationToken) =>
        QueryImages(image => image.ProductId == productId).ToListAsync(cancellationToken)
            .ContinueWith(task => (IReadOnlyList<ProductImageRecord>)task.Result, cancellationToken);

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<ProductImageRecord>>> GetImagesForProductsAsync(
        IReadOnlyList<int> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<ProductImageRecord>>();
        }

        var distinct = productIds.Distinct().ToList();

        var images = await QueryImages(image => distinct.Contains(image.ProductId))
            .ToListAsync(cancellationToken);

        return images
            .GroupBy(image => image.ProductId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProductImageRecord>)group.ToList());
    }

    public async Task<IReadOnlyList<CategoryRecord>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await _db.ProductCategories
            .AsNoTracking()
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => new CategoryRecord(
                category.Id,
                category.Name,
                category.Description,
                category.Products.Count(product => product.IsPublished && !product.IsDeleted)))
            .ToListAsync(cancellationToken);

        return categories;
    }

    // ================= مدیریت =================

    public async Task<IReadOnlyList<ProductListRow>> GetListAsync(bool includeDeleted, CancellationToken cancellationToken)
    {
        var query = _db.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .AsQueryable();

        if (!includeDeleted)
        {
            query = query.Where(product => !product.IsDeleted);
        }

        var rows = await query
            .OrderByDescending(product => product.CreatedAtUtc)
            .Select(product => new ProductListRow(
                product.Id,
                product.Title,
                product.Category == null ? null : product.Category.Name,
                product.PricePolicy,
                product.IsPublished,
                product.IsInStock,
                product.IsDeleted,
                product.Images.Count,
                product.CreatedAtUtc,
                product.PublishedAtUtc,
                product.PriceComputedAtUtc,
                product.PriceTotalIrt))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<ProductRecord?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(item => item.Category)
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

        return product is null ? null : Map(product);
    }

    public async Task<int> CreateAsync(
        ProductDraft draft,
        int actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var entity = new Product
        {
            Title = draft.Title.Trim(),
            Summary = NullIfWhiteSpace(draft.Summary),
            Description = NullIfWhiteSpace(draft.Description),
            CategoryId = draft.CategoryId,
            PricePolicy = draft.PricePolicy,
            FixedPriceIrt = draft.PricePolicy == PricePolicy.Fixed ? draft.FixedPriceIrt : null,
            WeightGrams = draft.PricePolicy == PricePolicy.Computed ? draft.WeightGrams : draft.WeightGrams,
            Karat = draft.Karat,
            MakingChargePercent = draft.MakingChargePercent,
            ProfitPercent = draft.ProfitPercent,
            TaxPercent = draft.TaxPercent,
            IsInStock = draft.IsInStock,
            IsPublished = draft.IsPublished,
            PublishedAtUtc = draft.IsPublished ? nowUtc : null,
            CreatedAtUtc = nowUtc,
            CreatedByUserId = actorUserId,
        };

        _db.Products.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    public async Task UpdateAsync(
        int id,
        ProductDraft draft,
        int actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var entity = await _db.Products.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.Title = draft.Title.Trim();
        entity.Summary = NullIfWhiteSpace(draft.Summary);
        entity.Description = NullIfWhiteSpace(draft.Description);
        entity.CategoryId = draft.CategoryId;
        entity.PricePolicy = draft.PricePolicy;
        entity.FixedPriceIrt = draft.PricePolicy == PricePolicy.Fixed ? draft.FixedPriceIrt : null;
        entity.WeightGrams = draft.WeightGrams;
        entity.Karat = draft.Karat;
        entity.MakingChargePercent = draft.MakingChargePercent;
        entity.ProfitPercent = draft.ProfitPercent;
        entity.TaxPercent = draft.TaxPercent;
        entity.IsInStock = draft.IsInStock;
        entity.UpdatedAtUtc = nowUtc;
        entity.UpdatedByUserId = actorUserId;

        // اگر سیاست از «محاسبه‌شده» به چیز دیگری تغییر کرده، عکسِ فوریِ قبلی
        // نباید بماند و بعداً به‌عنوان قیمتِ معتبر نمایش داده شود.
        if (entity.PricePolicy != PricePolicy.Computed)
        {
            ClearPriceSnapshot(entity);
        }

        // انتشار/عدم انتشار در ویرایش هم اعمال می‌شود (با ثبت زمانِ نخستین انتشار)
        if (draft.IsPublished && !entity.IsPublished)
        {
            entity.IsPublished = true;
            entity.PublishedAtUtc = nowUtc;
        }
        else if (!draft.IsPublished && entity.IsPublished)
        {
            entity.IsPublished = false;
            entity.PublishedAtUtc = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SavePriceSnapshotAsync(int id, PriceSnapshotRecord price, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(price);

        var entity = await _db.Products.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.PriceTotalIrt = price.TotalIrt;
        entity.PriceGoldValueIrt = price.GoldValueIrt;
        entity.PriceMakingIrt = price.MakingIrt;
        entity.PriceProfitIrt = price.ProfitIrt;
        entity.PriceTaxIrt = price.TaxIrt;
        entity.PriceRateAmountIrt = price.RateAmountIrt;
        entity.PriceRateQuotedAtUtc = price.RateQuotedAtUtc;
        entity.PriceWeightGrams = price.WeightGrams;
        entity.PriceKarat = price.Karat;
        entity.PriceMakingPercent = price.MakingPercent;
        entity.PriceProfitPercent = price.ProfitPercent;
        entity.PriceTaxPercent = price.TaxPercent;
        entity.PriceComputedAtUtc = price.ComputedAtUtc;
        entity.PriceFormulaVersion = price.FormulaVersion;
        entity.PriceReason = price.Reason;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPublishedAsync(
        int id,
        bool published,
        int actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return;
        }

        if (published && !entity.IsPublished)
        {
            entity.IsPublished = true;
            entity.PublishedAtUtc = nowUtc;
        }
        else if (!published && entity.IsPublished)
        {
            entity.IsPublished = false;
            entity.PublishedAtUtc = null;
        }

        entity.UpdatedAtUtc = nowUtc;
        entity.UpdatedByUserId = actorUserId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(
        int id,
        int actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.IsDeleted = true;
        entity.IsPublished = false;
        entity.DeletedAtUtc = nowUtc;
        entity.DeletedByUserId = actorUserId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ================= تصاویر =================

    public Task<int> CountImagesAsync(int productId, CancellationToken cancellationToken) =>
        _db.ProductImages.CountAsync(image => image.ProductId == productId, cancellationToken);

    public async Task AddImageAsync(ProductImageInput image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);

        _db.ProductImages.Add(new ProductImage
        {
            ProductId = image.ProductId,
            StoredFileName = image.StoredFileName,
            ThumbnailFileName = image.ThumbnailFileName,
            DisplayOrder = image.DisplayOrder,
            SizeBytes = image.SizeBytes,
            Width = image.Width,
            Height = image.Height,
            DetectedFormat = image.DetectedFormat,
            OriginalFileName = image.OriginalFileName,
            ClaimedContentType = image.ClaimedContentType,
            UploadedByUserId = image.UploadedByUserId,
            UploadedAtUtc = image.UploadedAtUtc,
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductImageRecord?> GetImageAsync(int imageId, CancellationToken cancellationToken) =>
        await QueryImages(image => image.Id == imageId).FirstOrDefaultAsync(cancellationToken);

    public async Task RemoveImageAsync(int imageId, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductImages.FirstOrDefaultAsync(image => image.Id == imageId, cancellationToken);

        if (entity is null)
        {
            return;
        }

        _db.ProductImages.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ================= نگاشت =================

    private IQueryable<ProductImageRecord> QueryImages(System.Linq.Expressions.Expression<Func<ProductImage, bool>> predicate) =>
        _db.ProductImages
            .AsNoTracking()
            .Where(predicate)
            .OrderBy(image => image.DisplayOrder)
            .ThenBy(image => image.Id)
            // نامِ بارگذار فقط برای نمایش در گزارش است (Left Join: حذفِ حساب، تصویر را بی‌صاحب نمی‌کند)
            .Select(image => new ProductImageRecord(
                image.Id,
                image.ProductId,
                image.StoredFileName,
                image.ThumbnailFileName,
                image.DisplayOrder,
                image.SizeBytes,
                image.Width,
                image.Height,
                image.DetectedFormat,
                image.OriginalFileName,
                image.ClaimedContentType,
                image.UploadedByUserId == 0 ? null : _db.Users
                    .Where(user => user.Id == image.UploadedByUserId)
                    .Select(user => user.DisplayName ?? user.UserName)
                    .FirstOrDefault(),
                image.UploadedAtUtc));

    private static ProductRecord Map(Product product) => new(
        Id: product.Id,
        Title: product.Title,
        Summary: product.Summary,
        Description: product.Description,
        CategoryId: product.CategoryId,
        CategoryName: product.Category?.Name,
        PricePolicy: product.PricePolicy,
        FixedPriceIrt: product.FixedPriceIrt,
        WeightGrams: product.WeightGrams,
        Karat: product.Karat,
        MakingChargePercent: product.MakingChargePercent,
        ProfitPercent: product.ProfitPercent,
        TaxPercent: product.TaxPercent,
        IsInStock: product.IsInStock,
        IsPublished: product.IsPublished,
        IsDeleted: product.IsDeleted,
        CreatedAtUtc: product.CreatedAtUtc,
        UpdatedAtUtc: product.UpdatedAtUtc,
        PublishedAtUtc: product.PublishedAtUtc,
        CreatedByUserId: product.CreatedByUserId,
        Price: product.PriceComputedAtUtc is null && product.PriceReason is null
            ? null
            : new PriceSnapshotRecord(
                TotalIrt: product.PriceTotalIrt,
                GoldValueIrt: product.PriceGoldValueIrt,
                MakingIrt: product.PriceMakingIrt,
                ProfitIrt: product.PriceProfitIrt,
                TaxIrt: product.PriceTaxIrt,
                RateAmountIrt: product.PriceRateAmountIrt,
                RateQuotedAtUtc: product.PriceRateQuotedAtUtc,
                WeightGrams: product.PriceWeightGrams,
                Karat: product.PriceKarat,
                MakingPercent: product.PriceMakingPercent,
                ProfitPercent: product.PriceProfitPercent,
                TaxPercent: product.PriceTaxPercent,
                ComputedAtUtc: product.PriceComputedAtUtc,
                FormulaVersion: product.PriceFormulaVersion,
                Reason: product.PriceReason));

    private static void ClearPriceSnapshot(Product entity)
    {
        entity.PriceTotalIrt = null;
        entity.PriceGoldValueIrt = null;
        entity.PriceMakingIrt = null;
        entity.PriceProfitIrt = null;
        entity.PriceTaxIrt = null;
        entity.PriceRateAmountIrt = null;
        entity.PriceRateQuotedAtUtc = null;
        entity.PriceWeightGrams = null;
        entity.PriceKarat = null;
        entity.PriceMakingPercent = null;
        entity.PriceProfitPercent = null;
        entity.PriceTaxPercent = null;
        entity.PriceComputedAtUtc = null;
        entity.PriceFormulaVersion = null;
        entity.PriceReason = null;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
