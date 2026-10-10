using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Catalog;
using SadGallery.Application.Media;
using SadGallery.Application.Text;
using SadGallery.Domain.Catalog;
using SadGallery.Domain.Enums;
using SadGallery.Web.Models.Catalog;
using SadGallery.Web.Security;

namespace SadGallery.Web.Areas.Operator.Controllers;

/// <summary>
/// مدیریت کالاها (ناحیهٔ اپراتور).
/// </summary>
/// <remarks>
/// <para>
/// <b>کنترل دسترسی سرور-محور است:</b> ویژگیِ <c>[Authorize(Policy = Policies.OperatorArea)]</c>
/// روی کلِ کنترلر اعمال شده؛ بنابراین حتی درخواست مستقیمِ HTTP (بدون رفتن به صفحه) توسط
/// کاربرِ غیرِ اپراتور/ادمین پذیرفته نمی‌شود. پنهان‌بودنِ دکمه در رابط کاربری کنترل
/// امنیتی به‌شمار نمی‌آید (مستند در docs/SECURITY.md و ADR-0014).
/// </para>
/// </remarks>
[Area("Operator")]
// پیشوندِ مسیر صراحتاً نوشته شده: مسیرهای صفتی (مانند HttpGet("create")) پیشوندِ area را
// به‌طور خودکار نمی‌گیرند؛ بدون این خط، نشانی‌ها به‌جای /Operator/Products/create
// به‌صورت /create در می‌آمدند و با مسیرِ قراردادیِ area ناهماهنگ می‌شدند.
[Route("Operator/Products")]
[Authorize(Policy = Policies.OperatorArea)]
public sealed class ProductsController : Controller
{
    /// <summary>
    /// سقفِ اندازهٔ کلِ درخواست. کمی بیشتر از سقفِ هر فایل است چون بدنهٔ چندبخشی
    /// (multipart) کمی حجمِ اضافه دارد؛ محدود کردن در اینجا مانعِ ارسالِ فایلِ بسیار
    /// بزرگ پیش از خوانده‌شدن در حافظه می‌شود.
    /// </summary>
    private const int MaxUploadRequestBytes = 10 * 1024 * 1024;

    private readonly ProductService _products;
    private readonly MediaOptions _mediaOptions;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        ProductService products,
        MediaOptions mediaOptions,
        ILogger<ProductsController> logger)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(mediaOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _products = products;
        _mediaOptions = mediaOptions;
        _logger = logger;
    }

    // ================= فهرست =================

    [HttpGet]
    public async Task<IActionResult> Index(bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        ViewData["IncludeDeleted"] = includeDeleted;

        var rows = await _products.GetListAsync(includeDeleted, cancellationToken);

        return View(rows);
    }

    // ================= ایجاد =================

    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildBlankForm(cancellationToken));
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormModel form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);

        if (!TryBuildDraft(form, out var draft, out var errors))
        {
            return await Redisplay(form, errors, cancellationToken);
        }

        var actorId = RequireUserId();

        if (actorId is null)
        {
            return Forbid();
        }

        var outcome = await _products.CreateAsync(draft, actorId.Value, cancellationToken);

        if (!outcome.Succeeded)
        {
            return await Redisplay(form, outcome.Errors, cancellationToken);
        }

        TempData["Notice"] = "کالا ایجاد شد. اکنون می‌توانید تصویر بارگذاری کنید.";

        return RedirectToAction(nameof(Images), new { id = outcome.ProductId });
    }

    // ================= ویرایش =================

    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var product = await _products.GetForEditAsync(id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        var form = new ProductFormModel
        {
            Id = product.Id,
            Title = product.Title,
            Summary = product.Summary,
            Description = product.Description,
            CategoryId = product.CategoryId,
            PricePolicy = product.PricePolicy,
            FixedPriceIrt = product.FixedPriceIrt?.ToString("0.##"),
            WeightGrams = product.WeightGrams?.ToString("0.###"),
            Karat = product.Karat?.ToString("0.##"),
            MakingChargePercent = product.MakingChargePercent.ToString("0.###"),
            ProfitPercent = product.ProfitPercent.ToString("0.###"),
            TaxPercent = product.TaxPercent.ToString("0.###"),
            IsInStock = product.IsInStock,
            IsPublished = product.IsPublished,
            Categories = await LoadCategories(cancellationToken),
        };

        return View(form);
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductFormModel form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);

        if (!TryBuildDraft(form, out var draft, out var errors))
        {
            return await Redisplay(form, errors, cancellationToken);
        }

        var actorId = RequireUserId();

        if (actorId is null)
        {
            return Forbid();
        }

        var outcome = await _products.UpdateAsync(id, draft, actorId.Value, cancellationToken);

        if (!outcome.Succeeded)
        {
            return await Redisplay(form, outcome.Errors, cancellationToken);
        }

        TempData["Notice"] = "تغییرات کالا ذخیره شد.";

        return RedirectToAction(nameof(Edit), new { id });
    }

    // ================= انتشار / حذف / به‌روزرسانی قیمت =================

    /// <summary>انتشار یا غیرفعال‌سازی.</summary>
    [HttpPost("publish/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id, bool published, CancellationToken cancellationToken)
    {
        var actorId = RequireUserId();

        if (actorId is null)
        {
            return Forbid();
        }

        await _products.SetPublishedAsync(id, published, actorId.Value, cancellationToken);

        TempData["Notice"] = published ? "کالا منتشر شد." : "کالا از ویترین خارج شد (غیرفعال شد).";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>حذفِ نرم (داده برای سوابق باقی می‌ماند).</summary>
    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var actorId = RequireUserId();

        if (actorId is null)
        {
            return Forbid();
        }

        await _products.SoftDeleteAsync(id, actorId.Value, cancellationToken);

        TempData["Notice"] = "کالا حذف شد (حذفِ نرم: رکورد برای سوابق باقی می‌ماند).";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>محاسبهٔ دوبارهٔ قیمتِ محاسبه‌شده با نرخِ جاری.</summary>
    [HttpPost("refresh-price/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshPrice(int id, CancellationToken cancellationToken)
    {
        var actorId = RequireUserId();

        if (actorId is null)
        {
            return Forbid();
        }

        var outcome = await _products.RefreshPriceAsync(id, actorId.Value, cancellationToken);

        TempData["Notice"] = outcome.Succeeded
            ? "قیمت با نرخِ جاری بازار دوباره محاسبه و ذخیره شد."
            : "قیمت به‌روز نشد: " + string.Join(" ", outcome.Errors);

        return RedirectToAction(nameof(Edit), new { id });
    }

    // ================= تصاویر =================

    [HttpGet("images/{id:int}")]
    public async Task<IActionResult> Images(int id, CancellationToken cancellationToken)
    {
        var product = await _products.GetForEditAsync(id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        return View(new ProductImagesViewModel
        {
            ProductId = product.Id,
            ProductTitle = product.Title,
            Images = await _products.GetImagesAsync(id, cancellationToken),
            MaxImages = _mediaOptions.MaxImagesPerProduct,
            MaxUploadBytes = _mediaOptions.MaxUploadBytes,
            AcceptedLabels = _mediaOptions.AcceptedLabels,
            Notice = TempData["Notice"] as string,
        });
    }

    [HttpPost("images/{id:int}/upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    public async Task<IActionResult> UploadImage(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        var model = await BuildImagesViewModel(id, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            model.Error = "هیچ فایلی انتخاب نشده است.";
            return View(nameof(Images), model);
        }

        var actorId = RequireUserId();

        if (actorId is null)
        {
            return Forbid();
        }

        // خواندنِ امن: ابتدا به حافظه با سقفِ مشخص (سقفِ اصلی در اعتبارسنج هم اعمال می‌شود).
        byte[] content;

        await using (var buffer = new MemoryStream())
        {
            await file.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        if (content.Length > _mediaOptions.MaxUploadBytes)
        {
            model.Error = $"حجم فایل بیش از مجاز است (بیشینهٔ مجاز: {_mediaOptions.MaxUploadBytes / 1024d / 1024d:0.#} مگابایت).";
            return View(nameof(Images), model);
        }

        var candidate = new UploadCandidate(
            FileName: file.FileName,
            ContentType: file.ContentType,
            Length: content.Length,
            Content: content);

        var outcome = await _products.AddImageAsync(
            id,
            candidate,
            actorId.Value,
            cancellationToken);

        if (!outcome.Succeeded)
        {
            // نام و نوعِ ادعایی برای بررسیِ امنیتی ثبت می‌شود؛ محتوای فایل هرگز لاگ نمی‌شود.
            _logger.LogWarning(
                "بارگذاری تصویر برای کالای {ProductId} رد شد. نام اعلامی: {FileName}، نوع اعلامی: {ContentType}، دلیل: {Reason}",
                id,
                file.FileName,
                file.ContentType,
                outcome.Error);

            model.Error = outcome.Error;
            return View(nameof(Images), model);
        }

        return RedirectToAction(nameof(Images), new { id });
    }

    [HttpPost("images/delete/{imageId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int imageId, int productId, CancellationToken cancellationToken)
    {
        var actorId = RequireUserId();

        if (actorId is null)
        {
            return Forbid();
        }

        var outcome = await _products.RemoveImageAsync(productId, imageId, actorId.Value, cancellationToken);

        TempData["Notice"] = outcome.Succeeded
            ? "تصویر حذف شد."
            : "حذف تصویر انجام نشد: " + outcome.Error;

        return RedirectToAction(nameof(Images), new { id = productId });
    }

    // ================= کمکی =================

    private int? RequireUserId() => User.CurrentUserId();

    private async Task<ProductFormModel> BuildBlankForm(CancellationToken cancellationToken) => new()
    {
        // پیش‌فرض‌ها مطابق عرفِ بازارِ ایران (سقفِ سود ۷٪ و مالیاتِ ۱۰٪ در سال ۱۴۰۵)؛
        // اپراتور می‌تواند برای هر کالا تغییر دهد. هیچ عددی پنهان نیست.
        Karat = "18",
        MakingChargePercent = "0",
        ProfitPercent = ProductPriceFormula.DefaultProfitPercent.ToString("0.##"),
        TaxPercent = ProductPriceFormula.DefaultTaxPercent.ToString("0.##"),
        IsInStock = true,
        IsPublished = false,
        Categories = await LoadCategories(cancellationToken),
    };

    private async Task<IReadOnlyList<CategoryOption>> LoadCategories(CancellationToken cancellationToken)
    {
        var categories = await _products.GetCategoriesAsync(cancellationToken);

        return categories
            .Select(category => new CategoryOption(category.Id, category.Name))
            .ToList();
    }

    private async Task<IActionResult> Redisplay(
        ProductFormModel form,
        IReadOnlyList<string> errors,
        CancellationToken cancellationToken)
    {
        form.Errors = errors;
        form.Categories = await LoadCategories(cancellationToken);

        return View(form.Id == 0 ? nameof(Create) : nameof(Edit), form);
    }

    private async Task<ProductImagesViewModel?> BuildImagesViewModel(int id, CancellationToken cancellationToken)
    {
        var product = await _products.GetForEditAsync(id, cancellationToken);

        if (product is null)
        {
            return null;
        }

        return new ProductImagesViewModel
        {
            ProductId = product.Id,
            ProductTitle = product.Title,
            Images = await _products.GetImagesAsync(id, cancellationToken),
            MaxImages = _mediaOptions.MaxImagesPerProduct,
            MaxUploadBytes = _mediaOptions.MaxUploadBytes,
            AcceptedLabels = _mediaOptions.AcceptedLabels,
        };
    }

    /// <summary>
    /// تبدیل فرم به ورودیِ لایه Application. همه اعداد با پارسرِ فارسی خوانده می‌شوند.
    /// </summary>
    private static bool TryBuildDraft(ProductFormModel form, out ProductDraft draft, out IReadOnlyList<string> errors)
    {
        var problems = new List<string>();
        var ok = true;

        decimal? fixedPrice = null;
        decimal? weight = null;
        decimal? karat = null;

        if (form.PricePolicy == PricePolicy.Fixed)
        {
            ok &= ReadNumber(form.FixedPriceIrt, "قیمت ثابت", required: true, value => fixedPrice = value, problems);
        }

        if (form.PricePolicy == PricePolicy.Computed)
        {
            ok &= ReadNumber(form.WeightGrams, "وزن", required: true, value => weight = value, problems);
            ok &= ReadNumber(form.Karat, "عیار", required: true, value => karat = value, problems);
        }
        else if (!string.IsNullOrWhiteSpace(form.WeightGrams))
        {
            // وزن برای کالاهای غیرِ محاسبه‌شده هم می‌تواند ثبت شود (اطلاعاتِ فنی)، اما اجباری نیست.
            ok &= ReadNumber(form.WeightGrams, "وزن", required: false, value => weight = value, problems);
        }

        if (!string.IsNullOrWhiteSpace(form.Karat) && karat is null)
        {
            ok &= ReadNumber(form.Karat, "عیار", required: false, value => karat = value, problems);
        }

        var making = 0m;
        var profit = ProductPriceFormula.DefaultProfitPercent;
        var tax = ProductPriceFormula.DefaultTaxPercent;

        ok &= ReadNumber(form.MakingChargePercent, "درصد اجرت", required: false, value => making = value, problems);
        ok &= ReadNumber(form.ProfitPercent, "درصد سود", required: false, value => profit = value, problems);
        ok &= ReadNumber(form.TaxPercent, "درصد مالیات", required: false, value => tax = value, problems);

        draft = new ProductDraft(
            Title: form.Title?.Trim() ?? string.Empty,
            Summary: form.Summary,
            Description: form.Description,
            CategoryId: form.CategoryId,
            PricePolicy: form.PricePolicy,
            FixedPriceIrt: fixedPrice,
            WeightGrams: weight,
            Karat: karat,
            MakingChargePercent: making,
            ProfitPercent: profit,
            TaxPercent: tax,
            IsInStock: form.IsInStock,
            IsPublished: form.IsPublished);

        errors = problems;

        return ok;
    }

    private static bool ReadNumber(
        string? raw,
        string label,
        bool required,
        Action<decimal> assign,
        List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (required)
            {
                problems.Add($"وارد کردنِ «{label}» الزامی است.");
                return false;
            }

            return true;
        }

        if (!PersianNumber.TryParse(raw, out var value, out var error))
        {
            problems.Add($"مقدار «{label}» قابل خواندن نیست" + (error is null ? "." : $": {error}"));
            return false;
        }

        assign(value);

        return true;
    }
}
