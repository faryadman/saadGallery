using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SadGallery.Application.Settings;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;
using SadGallery.Tests.Integration.Infrastructure;
using SadGallery.Web.Services;
using Xunit;

namespace SadGallery.Tests.Integration.Settings;

public sealed class ConnectionNoticeSqlTests
{
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value);
    [RequiresSqlServerFact]
    public async Task AdminCanPersistPublicTexts_XssIsEncoded_ConflictsAndValidationPreserveDraft_ChangesAreAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CatalogWebFactory(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SadGalleryDbContext>(); await db.Database.MigrateAsync(ct);
        var service = scope.ServiceProvider.GetRequiredService<ConnectionNoticeService>();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        foreach (var role in RoleNames.All) if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
        var password = "TestOnly!a1-" + Guid.NewGuid().ToString("N");
        async Task<(int Id, HttpClient Client)> User(string role)
        {
            var email = "notice-" + Guid.NewGuid().ToString("N") + "@test.invalid";
            var user = new ApplicationUser { UserName = email, Email = email, IsActive = true };
            Assert.True((await manager.CreateAsync(user, password)).Succeeded); Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Identifier"] = email, ["Password"] = password, ["__RequestVerificationToken"] = Token(await client.GetStringAsync("/Account/Login", ct)) }), ct);
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode); return (user.Id, client);
        }
        var admin = await User(RoleNames.Admin); using var a = admin.Client;
        var admin2 = await User(RoleNames.Admin); using var b = admin2.Client;
        var op = await User(RoleNames.Operator); using var o = op.Client;
        var customer = await User(RoleNames.Customer); using var c = customer.Client;
        var original = (await service.GetAsync(ct))!; Assert.NotNull(original); Assert.Equal(8, original.Version.Length);
        async Task<HttpResponseMessage> Post(HttpClient client, ConnectionNoticeText text, byte[] version)
        {
            var page = await client.GetStringAsync("/admin/connection-notice", ct);
            return await client.PostAsync("/admin/connection-notice", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["OfflineTitle"] = text.OfflineTitle, ["OfflineDescription"] = text.OfflineDescription, ["OnlineTitle"] = text.OnlineTitle,
                ["OnlineDescription"] = text.OnlineDescription, ["RowVersion"] = Convert.ToBase64String(version), ["__RequestVerificationToken"] = Token(page) }), ct);
        }
        try
        {
            using (var denied = await o.GetAsync("/admin/connection-notice", ct)) Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            using (var denied = await c.GetAsync("/admin/connection-notice", ct)) Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            foreach (var deniedClient in new[] { o, c })
            {
                var publicForm = await deniedClient.GetStringAsync("/market/gold", ct);
                using var deniedWrite = await deniedClient.PostAsync("/admin/connection-notice", new FormUrlEncodedContent(new Dictionary<string, string>
                    { ["__RequestVerificationToken"] = Token(publicForm) }), ct);
                Assert.Equal(HttpStatusCode.Redirect, deniedWrite.StatusCode);
            }
            var publicBefore = await c.GetStringAsync("/market/gold", ct); Assert.Contains("data-offline-title", publicBefore, StringComparison.Ordinal);
            var text = new ConnectionNoticeText("قطع <script>alert(1)</script>", "متن \"آزمایشی\" <img src=x onerror=alert(1)>", "اتصال دوباره برقرار شد", "برای تازه‌سازی اطلاعات اقدام کنید.");
            using (var saved = await Post(a, text, original.Version)) Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            var stored = (await service.GetAsync(ct))!; Assert.Equal(text, stored.Text); Assert.Equal(admin.Id, stored.UpdatedByUserId); Assert.NotNull(stored.UpdatedAtUtc);
            Assert.NotEqual(Convert.ToBase64String(original.Version), Convert.ToBase64String(stored.Version));
            var edit = Assert.Single(await db.Set<ConnectionNoticeEdit>().AsNoTracking().Where(x => x.ActorUserId == admin.Id).ToListAsync(ct));
            Assert.Contains(nameof(text.OfflineTitle), edit.Fields, StringComparison.Ordinal); Assert.DoesNotContain(text.OfflineTitle, edit.Fields, StringComparison.Ordinal);
            using (var publicPage = await c.GetAsync("/market/gold", ct))
            {
                var html = await publicPage.Content.ReadAsStringAsync(ct);
                Assert.DoesNotContain(text.OfflineTitle, html, StringComparison.Ordinal); Assert.DoesNotContain(text.OfflineDescription, html, StringComparison.Ordinal);
                Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal); Assert.Contains("&lt;img", html, StringComparison.Ordinal);
                Assert.Contains(text.OnlineTitle, html, StringComparison.Ordinal);
            }
            var draft = text with { OnlineTitle = "پیش‌نویس مدیر دوم" };
            using (var stale = await Post(b, draft, original.Version))
            {
                Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); var html = WebUtility.HtmlDecode(await stale.Content.ReadAsStringAsync(ct));
                Assert.Contains(draft.OnlineTitle, html, StringComparison.Ordinal); Assert.Contains(Convert.ToBase64String(stored.Version), html, StringComparison.Ordinal);
                Assert.Contains("alert alert-danger", html, StringComparison.Ordinal);
            }
            using (var invalid = await Post(a, text with { OfflineTitle = "", OnlineDescription = "پیش‌نویس حفظ شود" }, stored.Version))
            {
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); Assert.Contains("پیش‌نویس حفظ شود", await invalid.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
            }
            using (var tooLong = await Post(a, text with { OnlineTitle = new('a', 81) }, stored.Version)) Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
            Assert.Equal(NoticeSaveResult.Forbidden, await service.SaveAsync(op.Id, draft, stored.Version, ct));
            Assert.Equal(NoticeSaveResult.Unchanged, await service.SaveAsync(admin.Id, text, stored.Version, ct));
            Assert.Single(await db.Set<ConnectionNoticeEdit>().Where(x => x.ActorUserId == admin.Id).ToListAsync(ct));
            Assert.Empty(await db.Set<ConnectionNoticeEdit>().Where(x => x.ActorUserId == admin2.Id).ToListAsync(ct));
            Assert.Equal(text, (await service.GetAsync(ct))!.Text);
            using (var corrected = await Post(b, draft, stored.Version)) Assert.Equal(HttpStatusCode.Redirect, corrected.StatusCode);
            Assert.Equal(draft, (await service.GetAsync(ct))!.Text);
            using var editor = await a.GetAsync("/admin/connection-notice", ct); Assert.True(editor.Headers.CacheControl!.NoStore);
        }
        finally
        {
            var latest = (await service.GetAsync(ct))!;
            var restored = await service.SaveAsync(admin.Id, original.Text, latest.Version, ct);
            Assert.True(restored is NoticeSaveResult.Saved or NoticeSaveResult.Unchanged);
            scope.ServiceProvider.GetRequiredService<ConnectionNoticeDisplay>().Invalidate();
        }
    }
    private sealed class SlowStore : IConnectionNoticeStore
    {
        public async Task<ConnectionNoticeSettings?> GetAsync(CancellationToken ct)
        { await Task.Delay(Timeout.Infinite, ct); return null; }
        public Task<NoticeSaveResult> SaveAsync(int actorUserId, ConnectionNoticeText text, byte[] version, CancellationToken ct) => throw new NotSupportedException();
    }
    [Fact]
    public async Task UnavailableDatabaseDoesNotBreakPublicPageOrExposeDetails()
    {
        using var baseFactory = new CatalogWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        { s.RemoveAll<IConnectionNoticeStore>(); s.AddScoped<IConnectionNoticeStore, SlowStore>(); }));
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/Account/Login", TestContext.Current.CancellationToken);
        Assert.Contains(ConnectionNoticeText.Default.OfflineTitle, html, StringComparison.Ordinal);
        Assert.Contains(ConnectionNoticeText.Default.OnlineTitle, html, StringComparison.Ordinal);
        Assert.DoesNotContain("TaskCanceledException", html, StringComparison.Ordinal);
    }
}
