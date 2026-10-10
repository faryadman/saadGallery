using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using SadGallery.Application.Media;
using SadGallery.Domain.Constants;
using SadGallery.Domain.Enums;

namespace SadGallery.Application.Catalog;

/// <summary>نتیجه یک عملیات نوشتن روی محصول.</summary>
public sealed record ProductWriteOutcome(bool Succeeded, int? ProductId, IReadOnlyList<string> Errors)
{
    public static ProductWriteOutcome Ok(int id) => new(true, id, []);

    public static ProductWriteOutcome Fail(params string[] errors) => new(false, null, errors);
}

/// <summary>نتیجهٔ افزودن/حذف تصویر.</summary>
public sealed record ImageOperationOutcome(bool Succeeded, string? Error)
{
    public static ImageOperationOutcome Ok() => new(true, null);

    public static ImageOperationOutcome Fail(string error) => new(false, error);
}

/// <summary>
/// منطق کاتالوگ محصول. تصمیم‌های «چه چیزی نمایش داده شود / چه چیزی مجاز است» اینجا است،
/// نه در کنترلر (ADR-0003 §قاعده جبرانی).
/// </summary>
public sealed class ProductService
{
    private readonly IProductStore _store;
    private readonly IProductPriceCalculator _prices;
    private readonly MediaUploadService _uploads;
    private readonly IMediaStore _media;
    private readonly IClock _clock;
    private readonly ProductOptions _productOptions;
    private readonly MediaOptions _mediaOptions;

    public ProductService(
        IProductStore store,
        IProductPriceCalculator prices,
        MediaUploadService uploads,
        IMediaStore media,
        IClock clock,
        ProductOptions productOptions,
        MediaOptions mediaOptions)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(uploads);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(productOptions);
        ArgumentNullException.ThrowIfNull(mediaOptions);

        _store = store;
        _prices = prices;
        _uploads = uploads;
        _media = media;
        _clock = clock;
        _productOptions = productOptions;
        _mediaOptions = mediaOptions;
    }

    // ================= نمای عمومی =================

    /// <summary>آخرین محصول‌های منتشرشده (صفحهٔ اصلی و ویترین).</summary>
    public async Task<IReadOnlyList<PublicProduct>> GetPublishedAsync(
        int? take,
        CancellationToken cancellationToken)
    {
        var count = Math.Clamp(take ?? _productOptions.HomeLatestCount, 1, 50);
        var records = await _store.GetPublishedAsync(count, cancellationToken);
        var now = _clock.UtcNow;

        // تصاویرِ همهٔ محصول‌ها در یک کوئری گرفته می‌شود (نه یک کوئری به‌ازای هر محصول).
        var imagesByProduct = await _store.GetImagesForProductsAsync(
            records.Select(record => record.Id).ToList(),
            cancellationToken);

        var result = new List<PublicProduct>(records.Count);

        foreach (var record in records)
        {
            var images = imagesByProduct.TryGetValue(record.Id, out var found)
                ? found
                : [];

            result.Add(new PublicProduct(
                Id: record.Id,
                Title: record.Title,
                Summary: record.Summary,
                Description: record.Description,
                CategoryName: record.CategoryName,
                Price: PresentPrice(record, now),
                IsInStock: record.IsInStock,
                Images: images
                    .OrderBy(image => image.DisplayOrder)
                    .Select(image => new PublicProductImage(
                        Url: _media.GetPublicUrl(image.StoredFileName),
                        ThumbnailUrl: _media.GetPublicUrl(image.ThumbnailFileName),
                        DisplayOrder: image.DisplayOrder))
                    .ToList()));
        }

        return result;
    }

    /// <summary>
    /// جزئیات یک محصول برای عموم. محصولِ منتشرنشده یا حذف‌شده ⇒ <c>null</c>
    /// (فراخوان باید ۴۰۴ بدهد — معیار پذیرش فاز ۴).
    /// </summary>
    public async Task<PublicProduct?> GetPublishedDetailAsync(int id, CancellationToken cancellationToken)
    {
        var record = await _store.GetPublishedByIdAsync(id, cancellationToken);

        if (record is null)
        {
            return null;
        }

        var images = await _store.GetImagesAsync(id, cancellationToken);

        return new PublicProduct(
            Id: record.Id,
            Title: record.Title,
            Summary: record.Summary,
            Description: record.Description,
            CategoryName: record.CategoryName,
            Price: PresentPrice(record, _clock.UtcNow),
            IsInStock: record.IsInStock,
            Images: images
                .OrderBy(image => image.DisplayOrder)
                .Select(image => new PublicProductImage(
                    Url: _media.GetPublicUrl(image.StoredFileName),
                    ThumbnailUrl: _media.GetPublicUrl(image.ThumbnailFileName),
                    DisplayOrder: image.DisplayOrder))
                .ToList());
    }

    public Task<IReadOnlyList<CategoryRecord>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        _store.GetCategoriesAsync(cancellationToken);

    /// <summary>تصاویر یک کالا برای مدیریت (همراه نامِ بارگذار جهت ممیزی).</summary>
    public Task<IReadOnlyList<ProductImageRecord>> GetImagesAsync(int productId, CancellationToken cancellationToken) =>
        _store.GetImagesAsync(productId, cancellationToken);

    // ================= مدیریت =================

    public Task<IReadOnlyList<ProductListRow>> GetListAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        _store.GetListAsync(includeDeleted, cancellationToken);

    public Task<ProductRecord?> GetForEditAsync(int id, CancellationToken cancellationToken) =>
        _store.GetByIdAsync(id, cancellationToken);

    /// <summary>اعتبارسنجی ورودی محصول. پیام‌ها به فارسی و قابل نمایش مستقیم هستند.</summary>
    public IReadOnlyList<string> ValidateDraft(ProductDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            errors.Add("عنوان کالا الزامی است.");
        }
        else if (draft.Title.Trim().Length > _productOptions.MaxTitleLength)
        {
            errors.Add($"عنوان کالا نمی‌تواند بلندتر از {_productOptions.MaxTitleLength} نویسه باشد.");
        }

        if (draft.Summary is { Length: > 0 } && draft.Summary.Length > _productOptions.MaxSummaryLength)
        {
            errors.Add($"خلاصه نمی‌تواند بلندتر از {_productOptions.MaxSummaryLength} نویسه باشد.");
        }

        if (draft.Description is { Length: > 0 } && draft.Description.Length > _productOptions.MaxDescriptionLength)
        {
            errors.Add($"توضیحات نمی‌تواند بلندتر از {_productOptions.MaxDescriptionLength} نویسه باشد.");
        }

        switch (draft.PricePolicy)
        {
            case PricePolicy.Fixed:
                if (draft.FixedPriceIrt is null)
                {
                    errors.Add("برای سیاست «قیمت ثابت» باید مبلغ را وارد کنید.");
                }
                else if (draft.FixedPriceIrt <= 0m)
                {
                    errors.Add("مبلغ قیمت ثابت باید بزرگ‌تر از صفر باشد.");
                }

                break;

            case PricePolicy.Computed:
                if (draft.WeightGrams is null)
                {
                    errors.Add("برای سیاست «محاسبه‌شده» وزن کالا الزامی است.");
                }
                else if (draft.WeightGrams <= 0m)
                {
                    errors.Add("وزن باید بزرگ‌تر از صفر باشد.");
                }

                if (draft.Karat is null)
                {
                    errors.Add("برای سیاست «محاسبه‌شده» عیار کالا الزامی است.");
                }
                else if (draft.Karat <= 0m || draft.Karat > Measurements.PureKarat)
                {
                    errors.Add($"عیار باید بزرگ‌تر از صفر و حداکثر {Measurements.PureKarat} باشد.");
                }

                break;

            case PricePolicy.QuoteOnly:
                // نیازی به مبلغ یا وزن ندارد؛ عمداً هیچ عددی روی سایت نمایش داده نمی‌شود.
                break;

            default:
                errors.Add("سیاست قیمت نامعتبر است.");
                break;
        }

        foreach (var (percent, label) in new[]
                 {
                     (draft.MakingChargePercent, "درصد اجرت"),
                     (draft.ProfitPercent, "درصد سود"),
                     (draft.TaxPercent, "درصد مالیات"),
                 })
        {
            if (percent < 0m || percent > 100m)
            {
                errors.Add($"{label} باید بین ۰ و ۱۰۰ باشد.");
            }
        }

        return errors;
    }

    public async Task<ProductWriteOutcome> CreateAsync(
        ProductDraft draft,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateDraft(draft);

        if (errors.Count > 0)
        {
            return ProductWriteOutcome.Fail([.. errors]);
        }

        var now = _clock.UtcNow;
        var id = await _store.CreateAsync(draft, actorUserId, now, cancellationToken);

        if (draft.PricePolicy == PricePolicy.Computed)
        {
            await RefreshPriceAsync(id, actorUserId, cancellationToken);
        }

        return ProductWriteOutcome.Ok(id);
    }

    public async Task<ProductWriteOutcome> UpdateAsync(
        int id,
        ProductDraft draft,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateDraft(draft);

        if (errors.Count > 0)
        {
            return ProductWriteOutcome.Fail([.. errors]);
        }

        var existing = await _store.GetByIdAsync(id, cancellationToken);

        if (existing is null || existing.IsDeleted)
        {
            return ProductWriteOutcome.Fail("کالای مورد نظر یافت نشد.");
        }

        await _store.UpdateAsync(id, draft, actorUserId, _clock.UtcNow, cancellationToken);

        if (draft.PricePolicy == PricePolicy.Computed)
        {
            await RefreshPriceAsync(id, actorUserId, cancellationToken);
        }

        return ProductWriteOutcome.Ok(id);
    }

    /// <summary>انتشار یا غیرفعال‌سازی (فقط اپراتور/ادمین؛ کنترل دسترسی در کنترلر).</summary>
    public Task SetPublishedAsync(int id, bool published, int actorUserId, CancellationToken cancellationToken) =>
        _store.SetPublishedAsync(id, published, actorUserId, _clock.UtcNow, cancellationToken);

    /// <summary>حذف نرم.</summary>
    public Task SoftDeleteAsync(int id, int actorUserId, CancellationToken cancellationToken) =>
        _store.SoftDeleteAsync(id, actorUserId, _clock.UtcNow, cancellationToken);

    /// <summary>
    /// محاسبه و ذخیرهٔ دوبارهٔ قیمتِ محاسبه‌شده (با ثبت مبنا و زمان).
    /// </summary>
    public async Task<ProductWriteOutcome> RefreshPriceAsync(
        int id,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        var product = await _store.GetByIdAsync(id, cancellationToken);

        if (product is null || product.IsDeleted)
        {
            return ProductWriteOutcome.Fail("کالای مورد نظر یافت نشد.");
        }

        if (product.PricePolicy != PricePolicy.Computed)
        {
            return ProductWriteOutcome.Fail("این کالا سیاست «محاسبه‌شده» ندارد.");
        }

        var snapshot = _prices.Compute(product, _clock.UtcNow);
        await _store.SavePriceSnapshotAsync(id, snapshot, cancellationToken);

        return snapshot.Reason is null
            ? ProductWriteOutcome.Ok(id)
            : ProductWriteOutcome.Fail(snapshot.Reason);
    }

    // ================= تصاویر =================

    /// <summary>
    /// افزودن تصویر به محصول: اعتبارسنجی، پردازش، ذخیره و ثبتِ ممیزی (چه کسی و کِی).
    /// </summary>
    public async Task<ImageOperationOutcome> AddImageAsync(
        int productId,
        UploadCandidate candidate,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        var product = await _store.GetByIdAsync(productId, cancellationToken);

        if (product is null || product.IsDeleted)
        {
            return ImageOperationOutcome.Fail("کالای مورد نظر یافت نشد.");
        }

        var currentCount = await _store.CountImagesAsync(productId, cancellationToken);

        if (currentCount >= _mediaOptions.MaxImagesPerProduct)
        {
            return ImageOperationOutcome.Fail(
                $"بیشینهٔ تعداد تصویر برای هر کالا {_mediaOptions.MaxImagesPerProduct} عدد است.");
        }

        var outcome = await _uploads.SaveProductImageAsync(candidate, cancellationToken);

        if (!outcome.Succeeded || outcome.Stored is null)
        {
            return ImageOperationOutcome.Fail(outcome.Error ?? "بارگذاری تصویر ناموفق بود.");
        }

        await _store.AddImageAsync(new ProductImageInput(
            ProductId: productId,
            StoredFileName: outcome.Stored.StoredFileName,
            ThumbnailFileName: outcome.Stored.ThumbnailFileName,
            DisplayOrder: currentCount,
            SizeBytes: outcome.Stored.SizeBytes,
            Width: outcome.Stored.Width,
            Height: outcome.Stored.Height,
            DetectedFormat: outcome.Stored.Format.ToString(),
            OriginalFileName: TrimForAudit(candidate.FileName, 200),
            ClaimedContentType: TrimForAudit(candidate.ContentType, 100),
            UploadedByUserId: actorUserId,
            UploadedAtUtc: _clock.UtcNow), cancellationToken);

        return ImageOperationOutcome.Ok();
    }

    /// <summary>
    /// حذف تصویر: هم ردیف دیتابیس و هم دو فایلِ روی دیسک.
    /// </summary>
    /// <remarks>
    /// حذفِ فایلِ روی دیسک پیش از حذفِ ردیف انجام نمی‌شود: اگر حذفِ ردیف شکست بخورد،
    /// فایلِ بی‌صاحب روی دیسک می‌ماند (نشتِ فایل). ابتدا ردیف حذف می‌شود، سپس فایل‌ها.
    /// </remarks>
    public async Task<ImageOperationOutcome> RemoveImageAsync(
        int productId,
        int imageId,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        var image = await _store.GetImageAsync(imageId, cancellationToken);

        if (image is null || image.ProductId != productId)
        {
            return ImageOperationOutcome.Fail("تصویر مورد نظر یافت نشد.");
        }

        var storedName = image.StoredFileName;
        var thumbName = image.ThumbnailFileName;

        await _store.RemoveImageAsync(imageId, cancellationToken);

        // تنها پس از موفقیتِ حذفِ ردیف، فایل‌ها پاک می‌شوند.
        if (MediaFileNaming.IsWellFormed(storedName))
        {
            await _media.DeleteAsync(MediaKind.PublicImage, storedName, cancellationToken);
        }

        if (MediaFileNaming.IsWellFormed(thumbName))
        {
            await _media.DeleteAsync(MediaKind.PublicImage, thumbName, cancellationToken);
        }

        return ImageOperationOutcome.Ok();
    }

    // ================= نمایش قیمت =================

    /// <summary>
    /// تبدیل رکورد محصول به «نمایش قیمت» برای عموم.
    /// </summary>
    /// <remarks>
    /// قاعدهٔ مهم: <b>عددِ کهنه هرگز بی‌هشدار نمایش داده نمی‌شود.</b>
    /// اگر عکسِ فوریِ قیمت کهنه باشد، قیمت با نرخِ جاری دوباره محاسبه می‌شود و هشدارِ
    /// کهنگی همراه آن نمایش می‌یابد. اگر نرخ در دسترس نباشد، هیچ عددی نشان داده نمی‌شود.
    /// </remarks>
    public PricePresentation PresentPrice(ProductRecord product, DateTimeOffset nowUtc)
    {
        var outOfStockNote = product.IsInStock ? null : "این کالا در حال حاضر موجود نیست.";

        switch (product.PricePolicy)
        {
            case PricePolicy.Fixed:
                return new PricePresentation(
                    Policy: PricePolicy.Fixed,
                    HasAmount: product.FixedPriceIrt is > 0m,
                    AmountIrt: product.FixedPriceIrt is > 0m ? product.FixedPriceIrt : null,
                    AmountText: product.FixedPriceIrt is > 0m
                        ? RateFormat.Money(product.FixedPriceIrt!.Value, CurrencyUnit.Irt)
                        : null,
                    BasisText: null,
                    ComputedAtText: null,
                    FormulaVersion: null,
                    IsStale: false,
                    WarningText: product.FixedPriceIrt is > 0m
                        ? outOfStockNote
                        : JoinNotes("قیمت این کالا ثبت نشده است.", outOfStockNote));

            case PricePolicy.QuoteOnly:
                return new PricePresentation(
                    Policy: PricePolicy.QuoteOnly,
                    HasAmount: false,
                    AmountIrt: null,
                    AmountText: null,
                    BasisText: null,
                    ComputedAtText: null,
                    FormulaVersion: null,
                    IsStale: false,
                    WarningText: JoinNotes("قیمت این کالا پس از استعلام اعلام می‌شود.", outOfStockNote));

            case PricePolicy.Computed:
                return PresentComputed(product, nowUtc, outOfStockNote);

            default:
                return new PricePresentation(
                    Policy: product.PricePolicy,
                    HasAmount: false,
                    AmountIrt: null,
                    AmountText: null,
                    BasisText: null,
                    ComputedAtText: null,
                    FormulaVersion: null,
                    IsStale: false,
                    WarningText: JoinNotes("سیاست قیمت این کالا نامعتبر است.", outOfStockNote));
        }
    }

    private PricePresentation PresentComputed(
        ProductRecord product,
        DateTimeOffset nowUtc,
        string? outOfStockNote)
    {
        var snapshot = product.Price;
        var staleAfter = TimeSpan.FromMinutes(_productOptions.PriceStaleAfterMinutes);
        var isStale = snapshot?.ComputedAtUtc is null || nowUtc - snapshot.ComputedAtUtc.Value > staleAfter;

        // عکسِ فوری کهنه است (یا اصلاً وجود ندارد) ⇒ با نرخِ جاری دوباره حساب می‌کنیم،
        // تا عددی که نمایش می‌دهیم منسوخ نباشد. خروجیِ GET در دیتابیس نوشته نمی‌شود.
        if (isStale)
        {
            var fresh = _prices.Compute(product, nowUtc);

            if (fresh.TotalIrt is null)
            {
                return new PricePresentation(
                    Policy: PricePolicy.Computed,
                    HasAmount: false,
                    AmountIrt: null,
                    AmountText: null,
                    BasisText: null,
                    ComputedAtText: null,
                    FormulaVersion: fresh.FormulaVersion,
                    IsStale: snapshot?.ComputedAtUtc is not null,
                    WarningText: JoinNotes(fresh.Reason ?? "قیمت قابل محاسبه نیست.", outOfStockNote));
            }

            return new PricePresentation(
                Policy: PricePolicy.Computed,
                HasAmount: true,
                AmountIrt: fresh.TotalIrt,
                AmountText: RateFormat.Money(fresh.TotalIrt.Value, CurrencyUnit.Irt),
                BasisText: BuildBasisText(fresh),
                ComputedAtText: "همین لحظه",
                FormulaVersion: fresh.FormulaVersion,
                IsStale: snapshot?.ComputedAtUtc is not null,
                WarningText: JoinNotes("این مبلغ در همین لحظه بر اساس نرخِ فعلی بازار محاسبه شده است.", outOfStockNote));
        }

        // عکسِ فوری معتبر و تازه است
        if (snapshot!.TotalIrt is null)
        {
            return new PricePresentation(
                Policy: PricePolicy.Computed,
                HasAmount: false,
                AmountIrt: null,
                AmountText: null,
                BasisText: null,
                ComputedAtText: null,
                FormulaVersion: snapshot.FormulaVersion,
                IsStale: false,
                WarningText: JoinNotes(snapshot.Reason ?? "قیمت قابل محاسبه نیست.", outOfStockNote));
        }

        return new PricePresentation(
            Policy: PricePolicy.Computed,
            HasAmount: true,
            AmountIrt: snapshot.TotalIrt,
            AmountText: RateFormat.Money(snapshot.TotalIrt.Value, CurrencyUnit.Irt),
            BasisText: BuildBasisText(snapshot),
            ComputedAtText: snapshot.ComputedAtUtc is { } at
                ? SadGallery.Application.Text.PersianDate.ToJalaliDateTimeText(at)
                : null,
            FormulaVersion: snapshot.FormulaVersion,
            IsStale: false,
            WarningText: outOfStockNote);
    }

    /// <summary>متنِ مبنای محاسبه (شفاف، برای مشتری و برای ممیزی).</summary>
    public static string BuildBasisText(PriceSnapshotRecord snapshot)
    {
        var parts = new List<string>();

        if (snapshot.WeightGrams is { } weight)
        {
            parts.Add($"وزن {RateFormat.Amount(weight)} گرم");
        }

        if (snapshot.Karat is { } karat)
        {
            parts.Add($"عیار {RateFormat.Amount(karat)}");
        }

        if (snapshot.RateAmountIrt is { } rate)
        {
            parts.Add($"نرخ هر گرم طلای ۱۸ عیار {RateFormat.Money(rate, CurrencyUnit.Irt)}");
        }

        parts.Add($"اجرت {RateFormat.Percent(snapshot.MakingPercent ?? 0)}");
        parts.Add($"سود {RateFormat.Percent(snapshot.ProfitPercent ?? 0)}");
        parts.Add($"مالیات {RateFormat.Percent(snapshot.TaxPercent ?? 0)}");

        return string.Join(" · ", parts);
    }

    private static string? JoinNotes(params string?[] notes)
    {
        var present = notes.Where(note => !string.IsNullOrWhiteSpace(note)).ToList();

        return present.Count == 0 ? null : string.Join(" ", present);
    }

    /// <summary>
    /// کوتاه و پاک‌سازیِ نام فایل برای ثبت در گزارش.
    /// نام اصلیِ کاربر هرگز برای ذخیره به‌کار نمی‌رود؛ فقط برای ممیزی و نمایش نگه داشته می‌شود.
    /// </summary>
    private static string? TrimForAudit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            trimmed = trimmed[..maxLength];
        }

        // نویسه‌های کنترلی می‌توانند گزارش/صفحه را خراب کنند.
        return new string([.. trimmed.Where(character => !char.IsControl(character))]);
    }
}
