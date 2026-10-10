using SadGallery.Application.Catalog;

namespace SadGallery.Web.Models.Catalog;

/// <summary>دادهٔ صفحهٔ ویترین و فیلترهای عمومی.</summary>
public sealed class PublicCatalogViewModel
{
    public IReadOnlyList<PublicProduct> Products { get; init; } = [];

    public IReadOnlyList<CategoryRecord> Categories { get; init; } = [];

    public string? BudgetInput { get; init; }

    public decimal? BudgetToman { get; init; }

    public int? CategoryId { get; init; }

    public bool InStockOnly { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public bool CatalogUnavailable { get; init; }

    public string? FilterError { get; init; }

    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool HasBudget => BudgetToman.HasValue;
}
