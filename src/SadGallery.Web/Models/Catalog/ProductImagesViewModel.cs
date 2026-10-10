using SadGallery.Application.Catalog;

namespace SadGallery.Web.Models.Catalog;

/// <summary>صفحهٔ مدیریت تصاویر یک کالا (ناحیهٔ اپراتور).</summary>
public sealed class ProductImagesViewModel
{
    public int ProductId { get; set; }

    public string ProductTitle { get; set; } = string.Empty;

    public IReadOnlyList<ProductImageRecord> Images { get; set; } = [];

    public int MaxImages { get; set; }

    public int MaxUploadBytes { get; set; }

    /// <summary>قالب‌های پذیرفته‌شده برای نمایشِ راهنما.</summary>
    public IReadOnlyList<string> AcceptedLabels { get; set; } = [];

    public string? Error { get; set; }

    public string? Notice { get; set; }
}
