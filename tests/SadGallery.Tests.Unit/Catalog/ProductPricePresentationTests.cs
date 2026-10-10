using SadGallery.Application.Abstractions;
using SadGallery.Application.Catalog;
using SadGallery.Application.Market;
using SadGallery.Application.Media;
using SadGallery.Domain.Catalog;
using SadGallery.Domain.Enums;
using Xunit;

namespace SadGallery.Tests.Unit.Catalog;

/// <summary>
/// تست‌های «نمایش قیمت» برای عموم (فاز ۴).
/// </summary>
/// <remarks>
/// معیارِ حساس: <b>عددِ کهنه هرگز بی‌هشدار نمایش داده نمی‌شود.</b>
/// این تست‌ها قفل می‌کنند که یا عدد تازه است، یا هشدار دارد، یا اصلاً عددی نیست.
/// </remarks>
public sealed class ProductPricePresentationTests
{
    private const string GoldCode = "GOLD_GRAM_18";

    private static readonly DateTimeOffset Now = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private static RateSnapshotCache CacheWith(decimal rate, CurrencyUnit unit = CurrencyUnit.Irt)
    {
        var cache = new RateSnapshotCache();
        cache.Set(
            providerId: "test",
            publishable:
            [
                new NormalizedRate
                {
                    AssetCode = GoldCode,
                    Title = "طلای ۱۸ عیار (هر گرم)",
                    Amount = rate,
                    QuoteUnit = unit,
                    ProviderId = "test",
                    QuotedAtUtc = Now,
                    FetchedAtUtc = Now,
                    Quality = RateQuality.Live,
                },
            ],
            flagged: [],
            fetchedAtUtc: Now);

        return cache;
    }

    private static ProductService Service(
        RateSnapshotCache cache,
        DateTimeOffset now,
        int staleAfterMinutes = 60) =>
        new(
            store: new StubProductStore(),
            prices: new ProductPriceCalculator(cache),
            uploads: new MediaUploadService(new StubImageProcessor(), new StubMediaStore(), new MediaOptions()),
            media: new StubMediaStore(),
            clock: new FixedClock(now),
            productOptions: new ProductOptions { PriceStaleAfterMinutes = staleAfterMinutes },
            mediaOptions: new MediaOptions());

    private static ProductRecord Product(
        PricePolicy policy,
        decimal? fixedPrice = null,
        decimal? weight = null,
        decimal? karat = null,
        bool inStock = true,
        PriceSnapshotRecord? price = null) =>
        new(
            Id: 1,
            Title: "انگشتر نمونه",
            Summary: null,
            Description: null,
            CategoryId: null,
            CategoryName: null,
            PricePolicy: policy,
            FixedPriceIrt: fixedPrice,
            WeightGrams: weight,
            Karat: karat,
            MakingChargePercent: 10m,
            ProfitPercent: 7m,
            TaxPercent: 10m,
            IsInStock: inStock,
            IsPublished: true,
            IsDeleted: false,
            CreatedAtUtc: Now,
            UpdatedAtUtc: null,
            PublishedAtUtc: Now,
            CreatedByUserId: 1,
            Price: price);

    private static PriceSnapshotRecord SnapshotAt(DateTimeOffset computedAt, decimal? total = 11_947_000m) =>
        new(
            TotalIrt: total,
            GoldValueIrt: 10_000_000m,
            MakingIrt: 1_000_000m,
            ProfitIrt: 770_000m,
            TaxIrt: 177_000m,
            RateAmountIrt: 1_000_000m,
            RateQuotedAtUtc: computedAt,
            WeightGrams: 10m,
            Karat: 18m,
            MakingPercent: 10m,
            ProfitPercent: 7m,
            TaxPercent: 10m,
            ComputedAtUtc: computedAt,
            FormulaVersion: ProductPriceFormula.Version,
            Reason: null);

    // ─────────── سیاست «قیمت ثابت» ───────────

    [Fact]
    public void Fixed_ShowsAmountWithUnit()
    {
        var presentation = Service(CacheWith(1_000_000m), Now)
            .PresentPrice(Product(PricePolicy.Fixed, fixedPrice: 15_000_000m), Now);

        Assert.True(presentation.HasAmount);
        Assert.NotNull(presentation.AmountText);
        Assert.Contains("تومان", presentation.AmountText, StringComparison.Ordinal);
        // عدد باید با ارقام فارسی نمایش داده شود (قاعدهٔ سراسریِ پروژه)
        Assert.Contains("۱۵", presentation.AmountText, StringComparison.Ordinal);
        Assert.False(presentation.IsStale);
    }

    [Fact]
    public void Fixed_WithoutAmount_ShowsNoNumber()
    {
        var presentation = Service(CacheWith(1_000_000m), Now)
            .PresentPrice(Product(PricePolicy.Fixed, fixedPrice: null), Now);

        Assert.False(presentation.HasAmount);
        Assert.Null(presentation.AmountText);
        Assert.Contains("ثبت نشده", presentation.WarningText!, StringComparison.Ordinal);
    }

    // ─────────── سیاست «استعلامی» ───────────

    [Fact]
    public void QuoteOnly_NeverShowsAnyNumber()
    {
        var presentation = Service(CacheWith(1_000_000m), Now)
            .PresentPrice(Product(PricePolicy.QuoteOnly), Now);

        Assert.False(presentation.HasAmount);
        Assert.Null(presentation.AmountText);
        Assert.Contains("استعلام", presentation.WarningText!, StringComparison.Ordinal);
    }

    // ─────────── سیاست «محاسبه‌شده» ───────────

    [Fact]
    public void Computed_WithFreshSnapshot_ShowsAmountBasisAndTime()
    {
        var presentation = Service(CacheWith(1_000_000m), Now)
            .PresentPrice(Product(PricePolicy.Computed, weight: 10m, karat: 18m, price: SnapshotAt(Now)), Now);

        Assert.True(presentation.HasAmount);
        Assert.False(presentation.IsStale);
        Assert.NotNull(presentation.BasisText);
        Assert.Contains("نرخ هر گرم طلای ۱۸ عیار", presentation.BasisText, StringComparison.Ordinal);
        Assert.NotNull(presentation.ComputedAtText);
        Assert.Equal(ProductPriceFormula.Version, presentation.FormulaVersion);
        Assert.Null(presentation.WarningText);
    }

    [Fact]
    public void Computed_WithStaleSnapshot_DoesNotShowTheOldNumber()
    {
        // عکسِ فوری مربوط به ۳ ساعت پیش است؛ نمایشِ همان عدد بدون هشدار ممنوع است.
        var staleTime = Now.AddHours(-3);
        var presentation = Service(CacheWith(1_000_000m), Now)
            .PresentPrice(Product(PricePolicy.Computed, weight: 10m, karat: 18m, price: SnapshotAt(staleTime, total: 1_000m)), Now);

        Assert.True(presentation.IsStale);
        Assert.NotNull(presentation.WarningText);
        Assert.Contains("همین لحظه", presentation.WarningText, StringComparison.Ordinal);

        // و مهم‌تر: عدد نشان‌داده‌شده همان عددِ کهنه نیست
        Assert.NotEqual(1_000m, presentation.AmountIrt);
        Assert.True(presentation.AmountIrt > 1_000m);
    }

    [Fact]
    public void Computed_WithoutAnyRate_ShowsNoNumberButAReason()
    {
        var emptyCache = new RateSnapshotCache();
        var presentation = Service(emptyCache, Now)
            .PresentPrice(Product(PricePolicy.Computed, weight: 10m, karat: 18m), Now);

        Assert.False(presentation.HasAmount);
        Assert.Null(presentation.AmountText);
        Assert.Contains("نرخ", presentation.WarningText!, StringComparison.Ordinal);
    }

    [Fact]
    public void Computed_WithRateInAnotherUnit_RefusesToConvert()
    {
        // قاعدهٔ ADR-0009: تبدیلِ پنهانِ ریال/تومان ممنوع؛ نبودِ عدد بهتر از عددِ غلط است.
        var presentation = Service(CacheWith(10_000_000m, CurrencyUnit.Irr), Now)
            .PresentPrice(Product(PricePolicy.Computed, weight: 10m, karat: 18m), Now);

        Assert.False(presentation.HasAmount);
        Assert.Contains("تومان نیست", presentation.WarningText!, StringComparison.Ordinal);
    }

    [Fact]
    public void Computed_WithoutWeightOrKarat_ShowsNoNumber()
    {
        var presentation = Service(CacheWith(1_000_000m), Now)
            .PresentPrice(Product(PricePolicy.Computed, weight: null, karat: null), Now);

        Assert.False(presentation.HasAmount);
        Assert.Contains("وزن", presentation.WarningText!, StringComparison.Ordinal);
    }

    [Fact]
    public void Computed_FreshBoundary_ExactlyAtThresholdIsStillFresh()
    {
        var atThreshold = Now.AddMinutes(-60);
        var presentation = Service(CacheWith(1_000_000m), Now, staleAfterMinutes: 60)
            .PresentPrice(Product(PricePolicy.Computed, weight: 10m, karat: 18m, price: SnapshotAt(atThreshold)), Now);

        Assert.False(presentation.IsStale);
    }

    // ─────────── موجودی ───────────

    [Fact]
    public void OutOfStock_AlwaysCarriesAWarning()
    {
        var presentation = Service(CacheWith(1_000_000m), Now)
            .PresentPrice(Product(PricePolicy.Fixed, fixedPrice: 15_000_000m, inStock: false), Now);

        Assert.True(presentation.HasAmount); // قیمت نمایش داده می‌شود…
        Assert.Contains("موجود نیست", presentation.WarningText!, StringComparison.Ordinal); // …اما با هشدار
    }

    [Fact]
    public void OutOfStock_ForQuoteOnly_CombinesBothNotes()
    {
        var presentation = Service(CacheWith(1_000_000m), Now)
            .PresentPrice(Product(PricePolicy.QuoteOnly, inStock: false), Now);

        Assert.Contains("استعلام", presentation.WarningText!, StringComparison.Ordinal);
        Assert.Contains("موجود نیست", presentation.WarningText!, StringComparison.Ordinal);
    }

    // ─────────── اعتبارسنجیِ ورودی ───────────

    [Fact]
    public void ValidateDraft_RequiresAmountForFixedPolicy()
    {
        var service = Service(CacheWith(1_000_000m), Now);
        var errors = service.ValidateDraft(new ProductDraft(
            "کالا", null, null, null, PricePolicy.Fixed, null, null, null, 0, 7, 10, true, false));

        Assert.Contains(errors, error => error.Contains("مبلغ"));
    }

    [Fact]
    public void ValidateDraft_RequiresWeightAndKaratForComputedPolicy()
    {
        var service = Service(CacheWith(1_000_000m), Now);
        var errors = service.ValidateDraft(new ProductDraft(
            "کالا", null, null, null, PricePolicy.Computed, null, null, null, 0, 7, 10, true, false));

        Assert.Contains(errors, error => error.Contains("وزن"));
        Assert.Contains(errors, error => error.Contains("عیار"));
    }

    [Fact]
    public void ValidateDraft_RejectsOutOfRangeKaratAndPercents()
    {
        var service = Service(CacheWith(1_000_000m), Now);
        var errors = service.ValidateDraft(new ProductDraft(
            "کالا", null, null, null, PricePolicy.Computed, null, 1m, 30m, -5m, 200m, 101m, true, false));

        Assert.Contains(errors, error => error.Contains("عیار"));
        Assert.Contains(errors, error => error.Contains("اجرت"));
        Assert.Contains(errors, error => error.Contains("سود"));
        Assert.Contains(errors, error => error.Contains("مالیات"));
    }

    [Fact]
    public void ValidateDraft_AcceptsValidComputedProduct()
    {
        var service = Service(CacheWith(1_000_000m), Now);
        var errors = service.ValidateDraft(new ProductDraft(
            "انگشتر", null, null, null, PricePolicy.Computed, null, 8.133m, 18m, 10m, 7m, 10m, true, true));

        Assert.Empty(errors);
    }

    // ─────────── ساختگی‌ها ───────────

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class StubMediaStore : IMediaStore
    {
        public Task DeleteAsync(MediaKind kind, string storedFileName, CancellationToken cancellationToken) => Task.CompletedTask;

        public bool Exists(MediaKind kind, string storedFileName) => false;

        public string GetPhysicalPath(MediaKind kind, string storedFileName) => storedFileName;

        public string GetPublicUrl(string storedFileName) => $"/media/products/{storedFileName}";

        public Task SaveAsync(MediaKind kind, string storedFileName, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubImageProcessor : IImageProcessor
    {
        public ImageInspection? Inspect(ReadOnlySpan<byte> content) => null;

        public ProcessedImage? Process(ReadOnlySpan<byte> content, ImageProcessingSettings settings) => null;
    }

    /// <summary>
    /// مخزنِ ساختگی. این کلاس فقط برای ساختِ <see cref="ProductService"/> لازم است؛
    /// متدهایش در این تست‌ها (که فقط منطقِ نمایش را می‌سنجند) فراخوانی نمی‌شوند.
    /// </summary>
    private sealed class StubProductStore : IProductStore
    {
        public Task AddImageAsync(ProductImageInput image, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> CountImagesAsync(int productId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> CreateAsync(ProductDraft draft, int actorUserId, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<CategoryRecord>> GetCategoriesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CategoryRecord>>([]);

        public Task<ProductImageRecord?> GetImageAsync(int imageId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProductRecord?> GetByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ProductImageRecord>> GetImagesAsync(int productId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductImageRecord>>([]);

        public Task<IReadOnlyDictionary<int, IReadOnlyList<ProductImageRecord>>> GetImagesForProductsAsync(IReadOnlyList<int> productIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<ProductImageRecord>>>(new Dictionary<int, IReadOnlyList<ProductImageRecord>>());

        public Task<IReadOnlyList<ProductListRow>> GetListAsync(bool includeDeleted, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProductRecord?> GetPublishedByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ProductRecord>> GetPublishedAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductRecord>>([]);

        public Task RemoveImageAsync(int imageId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SavePriceSnapshotAsync(int id, PriceSnapshotRecord price, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetPublishedAsync(int id, bool published, int actorUserId, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SoftDeleteAsync(int id, int actorUserId, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UpdateAsync(int id, ProductDraft draft, int actorUserId, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
