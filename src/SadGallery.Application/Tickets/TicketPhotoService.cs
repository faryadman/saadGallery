using SadGallery.Application.Abstractions;
using Microsoft.Extensions.Logging;
using SadGallery.Application.Media;
using SadGallery.Domain.Enums;
namespace SadGallery.Application.Tickets;

public sealed record TicketSubmissionOutcome(Guid? TicketId, TicketWriteResult Result, string? Error = null);

/// <summary>Private photo uploads are committed with the message; failed writes clean up their files.</summary>
public sealed class TicketPhotoService(TicketService tickets, ITicketStore store, MediaUploadService upload, IMediaStore files, ILogger<TicketPhotoService> logger)
{
    public const int MaxPhotos = 3;
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxRequestBytes = 18 * 1024 * 1024;

    public Task RecordAccessDeniedAsync(int userId, CancellationToken ct) => store.RecordSecurityAsync(userId, "AccessDenied", ct);
    public Task<TicketPhotoFile?> GetAsync(TicketActor actor, Guid photoId, CancellationToken ct) => store.GetPhotoAsync(actor, photoId, ct);
    public Task<TicketSubmissionOutcome> CreateAsync(TicketActor actor, NewTicket input, IReadOnlyList<UploadCandidate> photos, CancellationToken ct) =>
        SubmitAsync(photos, async saved =>
        {
            var id = await tickets.CreateAsync(actor, input with { Photos = saved }, ct);
            return new(id, id.HasValue ? TicketWriteResult.Saved : TicketWriteResult.Invalid,
                id.HasValue ? null : "اطلاعات تیکت یا کالای انتخاب‌شده معتبر نیست.");
        }, ct);

    public async Task<TicketSubmissionOutcome> ReplyAsync(TicketActor actor, Guid id, TicketChange change,
        IReadOnlyList<UploadCandidate> photos, CancellationToken ct)
    {
        // Authorize before processing or writing any bytes.
        if (await tickets.GetCustomerAsync(actor.UserId, id, ct) == null)
        {
            await RecordAccessDeniedAsync(actor.UserId, ct);
            return new(null, TicketWriteResult.NotFound);
        }
        return await SubmitAsync(photos, async saved =>
            new(id, await tickets.ChangeAsync(actor, id, change with { Photos = saved }, ct)), ct);
    }
    private async Task<TicketSubmissionOutcome> SubmitAsync(IReadOnlyList<UploadCandidate> photos,
        Func<IReadOnlyList<StoredTicketPhoto>, Task<TicketSubmissionOutcome>> commit, CancellationToken ct)
    {
        if (photos.Count > MaxPhotos) return new(null, TicketWriteResult.Invalid, "در هر پیام حداکثر ۳ عکس انتخاب کنید.");
        if (photos.Any(p => p.Length <= 0 || p.Length > MaxFileBytes || p.Content.Length != p.Length))
            return new(null, TicketWriteResult.Invalid, "هر عکس باید معتبر و حداکثر ۵ مگابایت باشد.");
        var saved = new List<StoredTicketPhoto>();
        var committed = false;
        var commitAttempted = false;
        var commitReturned = false;
        try
        {
            foreach (var photo in photos)
            {
                var outcome = await upload.SavePrivateImageAsync(photo, ct);
                if (!outcome.Succeeded || outcome.Stored is not { } image)
                    return new(null, TicketWriteResult.Invalid, outcome.Error ?? "عکس انتخاب‌شده معتبر نیست.");
                saved.Add(new(Guid.NewGuid(), image.StoredFileName, image.Format, image.Width, image.Height, image.SizeBytes));
            }
            commitAttempted = true;
            var result = await commit(saved);
            commitReturned = true;
            committed = result.Result == TicketWriteResult.Saved;
            return result;
        }
        finally
        {
            if (!committed)
                foreach (var photo in saved)
                {
                    // A connection error can leave the DB commit result unknown. Do not delete a referenced photo.
                    if (commitAttempted && !commitReturned)
                    {
                        try { if (await store.IsPhotoReferencedAsync(photo.Id, CancellationToken.None)) continue; }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            logger.LogWarning("Private ticket photo cleanup deferred: database outcome unavailable ({ErrorType})", exception.GetType().Name);
                            continue;
                        }
                    }
                    await files.DeleteAsync(MediaKind.PrivateFile, photo.StoredFileName, CancellationToken.None);
                }
        }
    }
}
