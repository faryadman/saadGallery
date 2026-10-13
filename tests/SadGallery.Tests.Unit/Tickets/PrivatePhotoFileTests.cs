using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SadGallery.Application.Media;
using SadGallery.Domain.Enums;
using SadGallery.Infrastructure.Media;
using Xunit;
namespace SadGallery.Tests.Unit.Tickets;

public sealed class PrivatePhotoFileTests
{
    private sealed class Host : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "PrivatePhotoTests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    [Theory]
    [InlineData("public", "public/private")]
    [InlineData("private/public", "private")]
    public void NestedPublicAndPrivateRootsFailClosed(string publicPath,string privatePath)
    {
        var root=Path.Combine(Path.GetTempPath(),"photo-roots-"+Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidOperationException>(()=>new LocalMediaStore(new MediaOptions
            { UploadsRoot=root,PublicSubPath=publicPath,PrivateSubPath=privatePath },new Host()));
        Assert.False(Directory.Exists(root));
    }
    [Fact]
    public async Task CancelledWriteRemovesOnlyItsOwnPartialFile()
    {
        var root=Path.Combine(Path.GetTempPath(),"photo-write-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new LocalMediaStore(new MediaOptions { UploadsRoot=root },new Host());
            var name=MediaFileNaming.NewStoredName(SadGallery.Application.Abstractions.ImageFormat.WebP);
            using var cancelled=new CancellationTokenSource();await cancelled.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.SaveAsync(MediaKind.PrivateFile,name,new byte[1024],cancelled.Token));
            Assert.False(store.Exists(MediaKind.PrivateFile,name));
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
    [Fact]
    public async Task NamingCollisionPreservesOriginalFileBytes()
    {
        var root=Path.Combine(Path.GetTempPath(),"photo-collision-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new LocalMediaStore(new MediaOptions { UploadsRoot=root },new Host());
            var name=MediaFileNaming.NewStoredName(SadGallery.Application.Abstractions.ImageFormat.WebP);
            var ct=TestContext.Current.CancellationToken;
            await store.SaveAsync(MediaKind.PrivateFile,name,new byte[] { 1,2,3 },ct);
            await Assert.ThrowsAsync<IOException>(()=>store.SaveAsync(MediaKind.PrivateFile,name,new byte[] { 4,5,6 },ct));
            Assert.Equal(new byte[] { 1,2,3 },await File.ReadAllBytesAsync(store.GetPhysicalPath(MediaKind.PrivateFile,name),ct));
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
}
