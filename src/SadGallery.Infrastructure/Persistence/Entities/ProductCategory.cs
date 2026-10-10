namespace SadGallery.Infrastructure.Persistence.Entities;

/// <summary>دسته‌بندی کالاها (مانند انگشتر، دستبند، سکه، نیم‌ست).</summary>
public class ProductCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>ترتیب نمایش در فهرست.</summary>
    public int DisplayOrder { get; set; }

    public List<Product> Products { get; set; } = [];
}
