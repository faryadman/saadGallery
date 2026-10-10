using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Catalog;
using SadGallery.Web.Models.Catalog;
using SadGallery.Web.Security;

namespace SadGallery.Web.Areas.Operator.Controllers;

/// <summary>ساخت و ویرایش گروه‌های کالا (فقط اپراتور/مدیر).</summary>
[Area("Operator")]
[Route("Operator/Categories")]
[Authorize(Policy = Policies.OperatorArea)]
public sealed class CategoriesController : Controller
{
    private readonly ProductService _products;

    public CategoriesController(ProductService products)
    {
        ArgumentNullException.ThrowIfNull(products);
        _products = products;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var categories = await _products.GetCategoriesAsync(cancellationToken);
        return View(categories);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var categories = await _products.GetCategoriesAsync(cancellationToken);

        return View(new ProductCategoryFormModel
        {
            DisplayOrder = categories.Count == 0
                ? 10
                : (int)Math.Clamp((long)categories.Max(category => category.DisplayOrder) + 10L, 0L, 10000L),
        });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCategoryFormModel form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);

        var outcome = await _products.CreateCategoryAsync(ToDraft(form), cancellationToken);

        if (!outcome.Succeeded)
        {
            form.Errors = outcome.Errors;
            return View(form);
        }

        TempData["Notice"] = "گروه محصول ایجاد شد. اکنون می‌توانید آن را به کالاها اختصاص دهید.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var category = (await _products.GetCategoriesAsync(cancellationToken))
            .FirstOrDefault(item => item.Id == id);

        if (category is null)
        {
            return NotFound();
        }

        return View(new ProductCategoryFormModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder,
            PublishedProductCount = category.ProductCount,
        });
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductCategoryFormModel form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        form.Id = id;

        var outcome = await _products.UpdateCategoryAsync(id, ToDraft(form), cancellationToken);

        if (!outcome.Succeeded)
        {
            form.Errors = outcome.Errors;
            return View(form);
        }

        TempData["Notice"] = "گروه محصول به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    private static ProductCategoryDraft ToDraft(ProductCategoryFormModel form) =>
        new(form.Name, form.Description, form.DisplayOrder);
}
