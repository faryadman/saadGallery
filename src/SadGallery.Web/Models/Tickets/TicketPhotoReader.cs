using SadGallery.Application.Media;
using SadGallery.Application.Tickets;
namespace SadGallery.Web.Models.Tickets;

internal static class TicketPhotoReader
{
    public static async Task<(IReadOnlyList<UploadCandidate> Photos, string? Error)> ReadAsync(IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        if (files.Count > TicketPhotoService.MaxPhotos) return ([], "در هر پیام حداکثر ۳ عکس انتخاب کنید.");
        var photos = new List<UploadCandidate>();
        foreach (var file in files)
        {
            if (file.Length <= 0 || file.Length > TicketPhotoService.MaxFileBytes)
                return ([], "هر عکس باید حداکثر ۵ مگابایت و غیرخالی باشد.");
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            var chunk = new byte[64 * 1024];
            int count;
            while ((count = await stream.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > TicketPhotoService.MaxFileBytes)
                    return ([], "حجم عکس بیش از ۵ مگابایت است.");
                await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
            }
            photos.Add(new(file.FileName, file.ContentType, buffer.Length, buffer.ToArray()));
        }
        return (photos, null);
    }
}
