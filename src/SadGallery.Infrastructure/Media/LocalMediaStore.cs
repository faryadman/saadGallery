using Microsoft.Extensions.Hosting;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Media;
using SadGallery.Domain.Enums;

namespace SadGallery.Infrastructure.Media;

/// <summary>
/// نگهداری فایل‌ها روی دیسکِ محلی، در دو ریشهٔ کاملاً جدا برای عمومی و خصوصی.
/// </summary>
/// <remarks>
/// <para>
/// <b>تفکیک عمومی/خصوصی:</b> تنها ریشهٔ عمومی است که در لایه وب به یک مسیر ایستا
/// (<c>/media/products</c>) نقشه می‌شود. ریشهٔ خصوصی هیچ مسیر ایستایی ندارد؛ دسترسی به آن
/// فقط از راه یک کنترلرِ دارای مجوز ممکن است (که در فاز ۵ برای پیوست‌ها استفاده خواهد شد).
/// </para>
/// <para>
/// <b>سدهای امنیتی (لایه‌لایه):</b>
/// (۱) نام فایل باید دقیقاً با الگوی هگزِ تولیدشده توسط سامانه مطابقت کند
/// (<see cref="MediaFileNaming.IsWellFormed"/>)؛
/// (۲) مسیرِ نهایی دوباره حل و بررسی می‌شود که بیرون از ریشهٔ مجاز نرود
/// (جلوگیری از «عبور از مسیر» حتی اگر لایهٔ اول خطا کند).
/// </para>
/// </remarks>
public sealed class LocalMediaStore : IMediaStore
{
    private readonly string _publicRoot;
    private readonly string _privateRoot;
    private readonly string _publicRequestPath;

    public LocalMediaStore(MediaOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        // RootPath می‌تواند مطلق باشد؛ در این صورت Path.Combine همان مسیر مطلق را برمی‌گرداند.
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.UploadsRoot));

        _publicRoot = Path.GetFullPath(Path.Combine(root, options.PublicSubPath));
        _privateRoot = Path.GetFullPath(Path.Combine(root, options.PrivateSubPath));
        _publicRequestPath = options.PublicRequestPath.TrimEnd('/');

        if (string.Equals(_publicRoot, _privateRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "پوشهٔ فایل‌های عمومی و خصوصی نمی‌تواند یکی باشد (تفکیک یک الزام امنیتی است).");
        }

        // ساختِ پوشه‌ها در آغاز: اگر ممکن نباشد، برنامه همان ابتدا با پیامی روشن
        // متوقف می‌شود. این «سخت‌گیریِ آگاهانه» است؛ در غیر این صورت هر بارگذاری
        // بعداً با خطای مبهم روبه‌رو می‌شد و ردیابیِ آن دشوار بود.
        try
        {
            Directory.CreateDirectory(_publicRoot);
            Directory.CreateDirectory(_privateRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "ایجاد پوشهٔ نگهداریِ فایل‌ها ممکن نشد. " +
                "مقدار StorageOptions:UploadsRoot را بررسی کنید و مطمئن شوید حسابی که " +
                "برنامه با آن اجرا می‌شود، اجازهٔ نوشتن در آن مسیر را دارد " +
                "(راهنما: docs/DEPLOYMENT.md §ذخیره‌سازی تصاویر).",
                exception);
        }
    }

    /// <summary>ریشهٔ فایل‌های عمومی (برای بررسی و پیکربندی مسیر ایستا در لایه وب).</summary>
    public string PublicRoot => _publicRoot;

    /// <summary>ریشهٔ فایل‌های خصوصی.</summary>
    public string PrivateRoot => _privateRoot;

    public async Task SaveAsync(
        MediaKind kind,
        string storedFileName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (!MediaFileNaming.IsWellFormed(storedFileName))
        {
            throw new ArgumentException(
                "نام فایل با الگوی امنِ سامانه مطابقت ندارد؛ ذخیره انجام نشد.",
                nameof(storedFileName));
        }

        if (content.IsEmpty)
        {
            throw new ArgumentException("محتوای فایل خالی است.", nameof(content));
        }

        var path = Resolve(kind, storedFileName);
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // FileMode.CreateNew: اگر فایلی با همین نام وجود داشت، خطا می‌دهد.
        // با نام‌های تصادفیِ ۱۲۸ بیتی تصادم عملاً ناممکن است، اما در صورت وقوع،
        // «شکست» بسیار امن‌تر از بازنویسیِ فایلِ دیگری است (fail-closed).
        await using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
                BufferSize = 81920,
            });

        await stream.WriteAsync(content, cancellationToken);
    }

    public Task DeleteAsync(MediaKind kind, string storedFileName, CancellationToken cancellationToken)
    {
        if (!MediaFileNaming.IsWellFormed(storedFileName))
        {
            return Task.CompletedTask;
        }

        var path = Resolve(kind, storedFileName);

        // حذفِ فایلِ ناموجود خطا نیست: عملیاتِ حذف باید بایدن‌پتنت (Idempotent) باشد.
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public string GetPhysicalPath(MediaKind kind, string storedFileName) => Resolve(kind, storedFileName);

    public string GetPublicUrl(string storedFileName)
    {
        if (!MediaFileNaming.IsWellFormed(storedFileName))
        {
            throw new ArgumentException(
                "نام فایل با الگوی امنِ سامانه مطابقت ندارد.",
                nameof(storedFileName));
        }

        return $"{_publicRequestPath}/{storedFileName}";
    }

    public bool Exists(MediaKind kind, string storedFileName) =>
        MediaFileNaming.IsWellFormed(storedFileName) && File.Exists(Resolve(kind, storedFileName));

    /// <summary>
    /// حل و <b>مهار</b> مسیر: خروجی تضمین می‌شود که درون ریشهٔ همان نوع فایل باشد.
    /// </summary>
    private string Resolve(MediaKind kind, string storedFileName)
    {
        if (!MediaFileNaming.IsWellFormed(storedFileName))
        {
            throw new ArgumentException(
                "نام فایل با الگوی امنِ سامانه مطابقت ندارد.",
                nameof(storedFileName));
        }

        var root = kind switch
        {
            MediaKind.PublicImage => _publicRoot,
            MediaKind.PrivateFile => _privateRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported media kind."),
        };

        var fullPath = Path.GetFullPath(Path.Combine(root, storedFileName));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        // مقایسهٔ حساس به بزرگی/کوچکیِ نویسه‌ها: در ویندوز نام فایل‌ها حساس نیست،
        // اما در لینوکس حساس است؛ سخت‌گیرانه‌ترین حالت را در نظر می‌گیریم.
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "مسیر فایل خارج از پوشهٔ مجاز است؛ عملیات متوقف شد (جلوگیری از عبور از مسیر).");
        }

        return fullPath;
    }
}
