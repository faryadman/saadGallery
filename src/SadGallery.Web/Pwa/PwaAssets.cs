using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace SadGallery.Web.Pwa;

public sealed record PwaAsset(string Path, string Integrity, string Hash);
public sealed class PwaAssets
{
    public const string Version = "0.7.0";
    public static readonly IReadOnlyList<string> Paths = [
        "/offline.html", "/manifest.webmanifest", "/css/site.css",
        "/lib/bootstrap/bootstrap.rtl.min.css", "/lib/bootstrap/bootstrap.bundle.min.js",
        "/fonts/Vazirmatn-Regular.woff2", "/fonts/Vazirmatn-Medium.woff2", "/fonts/Vazirmatn-Bold.woff2",
        "/js/site.js", "/js/mobile-navigation.js", "/js/responsive-filters.js", "/js/offline-status.js",
        "/js/pwa.js", "/js/offline-page.js", "/pwa/icon.svg", "/pwa/icon-192.png",
        "/pwa/icon-512.png", "/pwa/icon-180.png", "/pwa/icon-maskable-512.png"
    ];
    public string BuildId { get; }
    public string Worker { get; }
    public IReadOnlyList<PwaAsset> Assets { get; }
    public PwaAssets(IWebHostEnvironment environment)
    {
        var assets = new List<PwaAsset>();
        foreach (var path in Paths)
        {
            var file = environment.WebRootFileProvider.GetFileInfo(path);
            using var input = file.CreateReadStream();
            var digest = SHA256.HashData(input);
            assets.Add(new(path, "sha256-" + Convert.ToBase64String(digest), WebEncoders.Base64UrlEncode(digest)));
        }
        Assets = assets;
        var content = typeof(PwaAssets).Module.ModuleVersionId + string.Join('|', assets.Select(x => x.Path + x.Integrity));
        BuildId = Version + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))[..16].ToLowerInvariant();
        using var stream = typeof(PwaAssets).Assembly.GetManifestResourceStream("SadGallery.Web.Pwa.service-worker.js")
            ?? throw new InvalidOperationException("PWA worker resource missing.");
        using var reader = new StreamReader(stream);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Worker = reader.ReadToEnd().Replace("__SG_BUILD__", JsonSerializer.Serialize(BuildId), StringComparison.Ordinal)
            .Replace("__SG_ASSETS__", JsonSerializer.Serialize(assets, jsonOptions), StringComparison.Ordinal);
    }
}
