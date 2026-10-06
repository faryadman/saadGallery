namespace SadGallery.Application.Abstractions;

/// <summary>
/// منبع زمان. تزریق این سرویس (به‌جای <c>DateTimeOffset.UtcNow</c> مستقیم) تست‌پذیری
/// محاسبات وابسته به «الان» را ممکن می‌کند. همه زمان‌ها UTC هستند.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
