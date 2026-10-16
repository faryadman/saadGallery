using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Application.Administration;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Administration;

public sealed class AdministrationSqlTests
{
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value);
    private const string SecretCanary = "NeverExposeThisProviderCredential-Canary";
    [RequiresSqlServerFact]
    public async Task RealRolesSettingsAuditsConcurrencyAndSessionRevocation()
    {
        var ct = TestContext.Current.CancellationToken;
        using var baseFactory = new CatalogWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(b => { b.UseSetting("RateOptions:Username", SecretCanary); b.UseSetting("RateOptions:Password", SecretCanary); b.UseSetting("RateOptions:EndpointTemplate", "https://canary.invalid/" + SecretCanary); });
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SadGalleryDbContext>(); await db.Database.MigrateAsync(ct);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        foreach (var role in RoleNames.All) if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
        var password = "TestOnly!1-" + Guid.NewGuid().ToString("N");
        async Task<(ApplicationUser User, HttpClient Client)> NewUser(string role)
        {
            var user = new ApplicationUser { UserName = "admin6-" + Guid.NewGuid().ToString("N") + "@test.invalid", DisplayName = "آزمون پنل " + Guid.NewGuid().ToString("N"), IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow };
            Assert.True((await manager.CreateAsync(user, password)).Succeeded); Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = user.UserName, ["Password"] = password, ["__RequestVerificationToken"] = Token(await client.GetStringAsync("/Account/Login", ct)) }), ct);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            if (role is RoleNames.Admin or RoleNames.Operator)
            {
                var cookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("SadGallery.Auth=", StringComparison.Ordinal)).Split(';')[0]["SadGallery.Auth=".Length..];
                var ticket = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme).TicketDataFormat.Unprotect(cookie);
                Assert.NotNull(ticket);
                Assert.InRange((ticket.Properties.ExpiresUtc!.Value - ticket.Properties.IssuedUtc!.Value).TotalMinutes, 29.9, 30.1);
            }
            return (user, client);
        }
        var admin = await NewUser(RoleNames.Admin); using var a = admin.Client;
        var other = await NewUser(RoleNames.Admin); using var b = other.Client;
        var op = await NewUser(RoleNames.Operator); using var o = op.Client;
        var customer = await NewUser(RoleNames.Customer); using var c = customer.Client;
        async Task<HttpResponseMessage> Post(HttpClient client, string path, Dictionary<string, string> values)
        { values["__RequestVerificationToken"] = Token(await client.GetStringAsync(path, ct)); return await client.PostAsync(path, new FormUrlEncodedContent(values), ct); }
        var store = scope.ServiceProvider.GetRequiredService<IAdministrationStore>();
        var original = (await store.GetAsync(ct))!; Assert.NotNull(original);
        try
        {
            foreach (var path in new[] { "/Admin/Dashboard", "/Operator/Dashboard", "/operator/reports", "/admin/users", "/admin/users?search=" + admin.User.Id, "/admin/users/" + op.User.Id, "/admin/settings", "/admin/audit", "/admin/audit.csv" })
            {
                using var response = await a.GetAsync(path, ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var html = await response.Content.ReadAsStringAsync(ct); Assert.DoesNotContain(SecretCanary, html, StringComparison.Ordinal); Assert.DoesNotContain(password, html, StringComparison.Ordinal);
                Assert.True(response.Headers.CacheControl!.NoStore);
            }
            var exactUser = await store.UsersAsync(1, op.User.Id.ToString(), ct); Assert.Equal(op.User.Id, Assert.Single(exactUser.Items).Id);
            using (var report = await o.GetAsync("/operator/reports", ct)) Assert.Equal(HttpStatusCode.OK, report.StatusCode);
            using (var denied = await o.GetAsync("/admin/users", ct)) Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            using (var denied = await c.GetAsync("/Operator/Dashboard", ct)) Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            using (var saved = await Post(a, "/admin/settings", new() { ["RatesPaused"] = "true", ["IntervalSeconds"] = "120", ["RowVersion"] = Convert.ToBase64String(original.Version) })) Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            var current = (await store.GetAsync(ct))!; Assert.True(current.Paused); Assert.Equal(120, current.IntervalSeconds);
            using (var stale = await Post(b, "/admin/settings", new() { ["RatesPaused"] = "false", ["IntervalSeconds"] = "180", ["RowVersion"] = Convert.ToBase64String(original.Version) }))
            { Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Contains("value=\"180\"", await stale.Content.ReadAsStringAsync(ct), StringComparison.Ordinal); }
            using (var invalid = await Post(a, "/admin/settings", new() { ["IntervalSeconds"] = "1", ["RowVersion"] = Convert.ToBase64String(current.Version) })) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var product = new Product { Title = "محصول آزمایشی ممیزی", CreatedAtUtc = DateTimeOffset.UtcNow, CreatedByUserId = admin.User.Id };
            var ticket = new SupportTicket { Id = Guid.NewGuid(), CustomerUserId = customer.User.Id, Subject = "ممیزی وضعیت", CreatedAtUtc = DateTimeOffset.UtcNow, LastActivityAtUtc = DateTimeOffset.UtcNow };
            db.AddRange(product, ticket); await db.SaveChangesAsync(ct);
            using (var deleted = await a.PostAsync("/Operator/Products/delete/" + product.Id, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(await a.GetStringAsync("/Operator/Products", ct)) }), ct)) Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
            using (var status = await a.PostAsync($"/operator/tickets/{ticket.Id}/status", new FormUrlEncodedContent(new Dictionary<string, string> { ["NextStatus"] = "1", ["RowVersion"] = Convert.ToBase64String(ticket.RowVersion), ["__RequestVerificationToken"] = Token(await a.GetStringAsync("/operator/tickets", ct)) }), ct)) Assert.Equal(HttpStatusCode.Redirect, status.StatusCode);
            Assert.True(await db.Products.AsNoTracking().Where(x => x.Id == product.Id).Select(x => x.IsDeleted).SingleAsync(ct));
            var target = (await store.UserAsync(op.User.Id, ct))!;
            using (var changed = await Post(a, "/admin/users/" + target.Id, new() { ["Role"] = "Customer", ["IsActive"] = "true", ["Version"] = target.Version })) Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
            using (var oldSession = await o.GetAsync("/Operator/Dashboard", ct))
            { Assert.Equal(HttpStatusCode.Redirect, oldSession.StatusCode); Assert.Contains("/Account/Login", oldSession.Headers.Location!.ToString(), StringComparison.Ordinal); }
            using (var stale = await Post(b, "/admin/users/" + target.Id, new() { ["Role"] = "Admin", ["IsActive"] = "true", ["Version"] = target.Version })) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            var self = (await store.UserAsync(admin.User.Id, ct))!;
            using (var refused = await Post(a, "/admin/users/" + self.Id, new() { ["Role"] = "Customer", ["IsActive"] = "false", ["Version"] = self.Version })) Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            // Suspension revokes a real customer's cookie on the very next request.
            var member = (await store.UserAsync(customer.User.Id, ct))!;
            using (var changed = await Post(a, "/admin/users/" + member.Id, new() { ["Role"] = "Customer", ["IsActive"] = "false", ["Version"] = member.Version })) Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
            using (var suspended = await c.GetAsync("/Member", ct)) { Assert.Equal(HttpStatusCode.Redirect, suspended.StatusCode); Assert.Contains("/Account/Login", suspended.Headers.Location!.ToString(), StringComparison.Ordinal); }
            var events = await db.Set<AuditEvent>().AsNoTracking().Where(x => x.ActorUserId == admin.User.Id).ToListAsync(ct);
            Assert.Contains(events, x => x.Action == "Settings.Changed" && x.Fields.Contains("RatesPaused", StringComparison.Ordinal));
            Assert.Contains(events, x => x.Action == "Product.Deleted" && x.EntityId == product.Id.ToString()); Assert.Contains(events, x => x.Action == "Ticket.StatusChanged" && x.EntityId == ticket.Id.ToString());
            Assert.Contains(events, x => x.Action == "User.RoleChanged"); Assert.Contains(events, x => x.Action == "User.AccessChanged");
            Assert.All(events, x => { Assert.NotEmpty(x.CorrelationId); Assert.NotEqual(default, x.OccurredAtUtc); Assert.DoesNotContain(password, x.Fields, StringComparison.Ordinal); });
            Assert.Empty(await db.Set<AuditEvent>().Where(x => x.ActorUserId == other.User.Id).ToListAsync(ct));
            var audit = events.First(); db.Attach(audit); audit.Action = "tampered";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct)); db.ChangeTracker.Clear();
            Assert.Equal(AdminSaveResult.Forbidden, await store.SaveUserAsync(customer.User.Id, admin.User.Id, "Customer", false, self.Version, ct));
        }
        finally
        {
            var latest = (await store.GetAsync(ct))!;
            Assert.True(await store.SaveSettingsAsync(admin.User.Id, original.Paused, original.IntervalSeconds, latest.Version, ct) is AdminSaveResult.Saved or AdminSaveResult.Unchanged);
        }
    }
    [RequiresSqlServerFact]
    public async Task ConcurrentCrossDemotionCannotRemoveBothAdministrators()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(RequiresSqlServerFactAttribute.EnvironmentVariableName)!) { InitialCatalog = "SadGallery_AdminRace_" + Guid.NewGuid().ToString("N") };
        using var baseFactory = new CatalogWebFactory();
        using var factory = baseFactory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:SadGallery", connection.ConnectionString));
        using var setup = factory.Services.CreateScope(); var db = setup.ServiceProvider.GetRequiredService<SadGalleryDbContext>();
        try
        {
            await db.Database.MigrateAsync(ct);
            var roleManager = setup.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            foreach(var role in RoleNames.All) Assert.True((await roleManager.CreateAsync(new(role))).Succeeded);
            var manager = setup.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            async Task<ApplicationUser> Create()
            { var u = new ApplicationUser { UserName = Guid.NewGuid().ToString("N"), IsActive = true }; Assert.True((await manager.CreateAsync(u)).Succeeded); Assert.True((await manager.AddToRoleAsync(u, RoleNames.Admin)).Succeeded); return u; }
            var a = await Create(); var b = await Create();
            using var first = factory.Services.CreateScope(); using var second = factory.Services.CreateScope();
            var results = await Task.WhenAll(first.ServiceProvider.GetRequiredService<IAdministrationStore>().SaveUserAsync(a.Id, b.Id, "Customer", true, b.ConcurrencyStamp!, ct),
                second.ServiceProvider.GetRequiredService<IAdministrationStore>().SaveUserAsync(b.Id, a.Id, "Customer", true, a.ConcurrencyStamp!, ct));
            Assert.Single(results, x => x == AdminSaveResult.Saved); Assert.Single(results, x => x == AdminSaveResult.Forbidden);
            var adminRole = await db.Roles.SingleAsync(x => x.Name == RoleNames.Admin, ct);
            Assert.Equal(1, await db.UserRoles.CountAsync(x => x.RoleId == adminRole.Id, ct));
        }
        finally { await db.Database.EnsureDeletedAsync(ct); }
    }
}
