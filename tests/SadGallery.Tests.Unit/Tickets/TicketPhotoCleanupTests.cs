using Microsoft.Extensions.Logging.Abstractions;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Media;
using SadGallery.Application.Tickets;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Tickets;
using SadGallery.Infrastructure.Media;
using SkiaSharp;
using Xunit;
namespace SadGallery.Tests.Unit.Tickets;

public sealed class TicketPhotoCleanupTests
{
    private sealed class Files : IMediaStore
    {
        public HashSet<string> Names { get; } = [];
        public bool FailWrite { get; init; }
        public Task SaveAsync(MediaKind kind,string name,ReadOnlyMemory<byte> content,CancellationToken ct)
        {
            Assert.Equal(MediaKind.PrivateFile,kind); Names.Add(name);
            return FailWrite ? Task.FromException(new IOException("partial write")) : Task.CompletedTask;
        }
        public Task DeleteAsync(MediaKind kind,string name,CancellationToken ct) { Names.Remove(name);return Task.CompletedTask; }
        public string GetPhysicalPath(MediaKind kind,string name) => name;
        public string GetPublicUrl(string name) => throw new NotSupportedException();
        public bool Exists(MediaKind kind,string name) => Names.Contains(name);
    }
    private sealed class UncertainStore(string mode) : ITicketStore
    {
        public Task<Guid?> CreateAsync(TicketActor actor,NewTicket input,CancellationToken ct) => Task.FromException<Guid?>(new IOException("unknown commit"));
        public Task<bool> IsPhotoReferencedAsync(Guid id,CancellationToken ct) => mode=="unavailable" ? Task.FromException<bool>(new IOException("database unavailable")) : Task.FromResult(mode=="referenced");
        public Task<IReadOnlyList<TicketSummary>> ListAsync(TicketActor a,TicketFilter f,CancellationToken ct) => throw new NotSupportedException();
        public Task<CustomerTicket?> GetCustomerAsync(int user,Guid id,CancellationToken ct) => throw new NotSupportedException();
        public Task<OperatorTicket?> GetOperatorAsync(Guid id,CancellationToken ct) => throw new NotSupportedException();
        public Task<TicketWriteResult> ChangeAsync(TicketActor a,Guid id,TicketChange c,CancellationToken ct) => throw new NotSupportedException();
        public Task<TicketWriteResult> FinishAsync(TicketActor a,Guid id,byte[] version,CancellationToken ct) => throw new NotSupportedException();
        public Task<TicketPhotoFile?> GetPhotoAsync(TicketActor a,Guid id,CancellationToken ct) => throw new NotSupportedException();
        public Task RecordSecurityAsync(int? user,string kind,CancellationToken ct) => Task.CompletedTask;
    }
    private static UploadCandidate Candidate()
    {
        using var bitmap=new SKBitmap(40,40); using var canvas=new SKCanvas(bitmap); canvas.Clear(SKColors.White);
        using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,90);var bytes=data.ToArray();
        return new("image.png","image/png",bytes.Length,bytes);
    }
    [Theory]
    [InlineData("unreferenced",0)] [InlineData("referenced",1)] [InlineData("unavailable",1)]
    public async Task UnknownCommitNeverDeletesReferencedOrUnverifiablePhotos(string mode,int remaining)
    {
        var files=new Files();var store=new UncertainStore(mode);
        var media=new MediaUploadService(new SkiaImageProcessor(),files,new MediaOptions());
        var service=new TicketPhotoService(new(store),store,media,files,NullLogger<TicketPhotoService>.Instance);
        await Assert.ThrowsAsync<IOException>(()=>service.CreateAsync(new(1,false),new("subject",TicketType.Other,"body"),[Candidate()],TestContext.Current.CancellationToken));
        Assert.Equal(remaining,files.Names.Count);
    }
    [Fact]
    public async Task FailedSaveDoesNotDeleteAnotherWritersExistingArtifact()
    {
        var files=new Files { FailWrite=true };
        var media=new MediaUploadService(new SkiaImageProcessor(),files,new MediaOptions());
        await Assert.ThrowsAsync<IOException>(()=>media.SavePrivateImageAsync(Candidate(),TestContext.Current.CancellationToken));
        Assert.Single(files.Names);
    }
}
