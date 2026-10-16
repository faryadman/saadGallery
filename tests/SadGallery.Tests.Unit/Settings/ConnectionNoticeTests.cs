using SadGallery.Application.Settings;
using Xunit;

namespace SadGallery.Tests.Unit.Settings;

public sealed class ConnectionNoticeTests
{
    private sealed class Store : IConnectionNoticeStore
    {
        public ConnectionNoticeText? Saved { get; private set; }
        public Task<ConnectionNoticeSettings?> GetAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<NoticeSaveResult> SaveAsync(int actorUserId, ConnectionNoticeText text, byte[] version, CancellationToken ct)
        { Saved = text; return Task.FromResult(NoticeSaveResult.Saved); }
    }
    [Fact]
    public async Task InvalidTextActorAndVersionNeverReachTheStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new Store(); var service = new ConnectionNoticeService(store); var good = ConnectionNoticeText.Default;
        ConnectionNoticeText[] invalid = [good with { OfflineTitle = " " }, good with { OnlineTitle = new('x', 81) },
            good with { OfflineDescription = new('x', 201) }, good with { OnlineDescription = "\0invalid" },
            good with { OfflineTitle = new('x', 81) }, good with { OnlineDescription = new('x', 201) }];
        foreach (var text in invalid) Assert.Equal(NoticeSaveResult.Invalid, await service.SaveAsync(1, text, new byte[8], ct));
        Assert.Equal(NoticeSaveResult.Invalid, await service.SaveAsync(0, good, new byte[8], ct));
        Assert.Equal(NoticeSaveResult.Invalid, await service.SaveAsync(1, good, new byte[7], ct));
        Assert.Null(store.Saved);
    }
    [Fact]
    public async Task ValidBoundaryTextIsTrimmedButStoredAsPlainText()
    {
        var store = new Store(); var service = new ConnectionNoticeService(store);
        var text = new ConnectionNoticeText("  عنوان <script>  ", new('a', 200), new('b', 80), " توضیح\nدوباره ");
        Assert.Equal(NoticeSaveResult.Saved, await service.SaveAsync(1, text, new byte[8], TestContext.Current.CancellationToken));
        Assert.NotNull(store.Saved); Assert.Equal("عنوان <script>", store.Saved.OfflineTitle);
        Assert.Equal(text.OfflineDescription, store.Saved.OfflineDescription); Assert.Equal(text.OnlineTitle, store.Saved.OnlineTitle);
        Assert.Equal("توضیح\nدوباره", store.Saved.OnlineDescription);
    }
}
