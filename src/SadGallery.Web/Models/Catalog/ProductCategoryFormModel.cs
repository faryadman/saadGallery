namespace SadGallery.Web.Models.Catalog;

/// <summary>فرم ساخت/ویرایش دسته‌بندی.</summary>
public sealed class ProductCategoryFormModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public int PublishedProductCount { get; set; }

    public IReadOnlyList<string> Errors { get; set; } = [];
}
