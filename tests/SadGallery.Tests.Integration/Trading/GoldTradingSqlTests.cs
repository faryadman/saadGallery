using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Administration;
using SadGallery.Application.Market;
using SadGallery.Application.Trading;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Trading;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;
namespace SadGallery.Tests.Integration.Trading;

public sealed class GoldTradingSqlTests
{
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow; }
    private sealed class Fixture : IAsyncDisposable
    {
        public readonly CatalogWebFactory Base = new();
        public WebApplicationFactory<Program> Factory { get; }
        public readonly TestClock Clock = new();
        public IServiceScope Scope { get; }
        public SadGalleryDbContext Db => Scope.ServiceProvider.GetRequiredService<SadGalleryDbContext>();
        public IGoldTradingStore Store => Scope.ServiceProvider.GetRequiredService<IGoldTradingStore>();
        public RateOptions Options => Scope.ServiceProvider.GetRequiredService<RateOptions>();
        private readonly string password = "TestOnly!7-" + Guid.NewGuid().ToString("N");
        public Fixture()
        {
            var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(RequiresSqlServerFactAttribute.EnvironmentVariableName)!) { InitialCatalog = "SadGallery_Gold_" + Guid.NewGuid().ToString("N") };
            Factory = Base.WithWebHostBuilder(b => { b.UseSetting("ConnectionStrings:SadGallery", connection.ConnectionString); b.UseSetting("RateOptions:StartupDelaySeconds", "3600"); b.ConfigureServices(s => s.AddSingleton<IClock>(Clock)); });
            Scope = Factory.Services.CreateScope();
        }
        public async Task Init(CancellationToken ct)
        {
            await Db.Database.MigrateAsync(ct);
            var roles = Scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            foreach (var role in RoleNames.All) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
            // Only the trading reader is enabled; the real HTTP provider stays Disabled in this hermetic factory.
            Options.Provider = RateOptions.ProviderTgn;
            await Rate(ct);
        }
        public async Task Rate(CancellationToken ct, string provider = "Tgn", RateQuality quality = RateQuality.Live, CurrencyUnit unit = CurrencyUnit.Irt, bool anomaly = false, DateTimeOffset? at = null)
        {
            Db.ChangeTracker.Clear(); Db.MarketRates.Add(new() { AssetCode = "GOLD_GRAM_18", ProviderId = provider, Amount = 2_000_000m, QuoteUnit = unit,
                Quality = quality, IsAnomalySuspected = anomaly, QuotedAtUtc = at ?? Clock.UtcNow, FetchedAtUtc = at ?? Clock.UtcNow, CreatedAtUtc = Clock.UtcNow }); await Db.SaveChangesAsync(ct);
        }
        public async Task<(int Id, HttpClient Client)> User(string role, CancellationToken ct)
        {
            var manager = Scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = Guid.NewGuid().ToString("N") + "@test.invalid", DisplayName = "مشتری آزمون", IsActive = true, CreatedAtUtc = Clock.UtcNow };
            Assert.True((await manager.CreateAsync(user, password)).Succeeded); Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
            var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var login = await client.GetStringAsync("/Account/Login", ct);
            using var result = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = user.UserName, ["Password"] = password, ["__RequestVerificationToken"] = Token(login) }), ct);
            Assert.Equal(HttpStatusCode.Redirect, result.StatusCode); return (user.Id, client);
        }
        public async Task Configure(int admin, CancellationToken ct)
        { var s = await Store.SettingsAsync(ct); Assert.Equal(GoldResult.Saved, await Store.SettingsAsync(admin, true, 1.5m, s.Version, ct)); }
        public async Task Wallet(int admin, int customer, decimal cash, CancellationToken ct)
        {
            Assert.Equal(GoldResult.Saved, await Store.EnableWalletAsync(admin, customer, true, null, ct));
            Assert.Equal(GoldResult.Saved, await Store.CreditAsync(admin, customer, Guid.NewGuid(), GoldLedgerKind.TomanCredit, cash, "شارژ آزمایشی مستقل", ct));
        }
        public async Task<GoldResult> Isolated(Func<IGoldTradingStore, Task<GoldResult>> operation)
        { using var scope = Factory.Services.CreateScope(); return await operation(scope.ServiceProvider.GetRequiredService<IGoldTradingStore>()); }
        public async ValueTask DisposeAsync() { await Db.Database.EnsureDeletedAsync(); Scope.Dispose(); Factory.Dispose(); Base.Dispose(); }
    }
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value);
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, Dictionary<string, string> data, CancellationToken ct)
    { data["__RequestVerificationToken"] = Token(await client.GetStringAsync("/Account/Login", ct)); return await client.PostAsync(path, new FormUrlEncodedContent(data), ct); }
    private static void Denied(HttpResponseMessage response)
    { Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString(), StringComparison.Ordinal); }
    [RequiresSqlServerFact]
    public async Task RealHttpPurchaseApprovalAuthorizationLedgerAndMissingDecision()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = new Fixture(); await f.Init(ct);
        var admin = await f.User(RoleNames.Admin, ct); var op = await f.User(RoleNames.Operator, ct); var user = await f.User(RoleNames.Customer, ct); var other = await f.User(RoleNames.Customer, ct);
        await f.Configure(admin.Id, ct); await f.Wallet(admin.Id, user.Id, 10_000_000m, ct);
        var creditId = Guid.NewGuid();
        Dictionary<string, string> Credit() => new() { ["OperationId"] = creditId.ToString(), ["Kind"] = "GoldCredit", ["Amount"] = "۱٫۲۵۰", ["Reason"] = "طلای تحویلی مشتری؛ آزمون" };
        using (var r = await Post(admin.Client, $"/admin/gold/wallets/{user.Id}/credit", Credit(), ct)) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        using (var r = await Post(admin.Client, $"/admin/gold/wallets/{user.Id}/credit", Credit(), ct)) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        using (var r = await Post(op.Client, $"/admin/gold/wallets/{user.Id}/credit", Credit(), ct)) Denied(r);
        var missingKey = Credit(); missingKey.Remove("OperationId");
        using (var r = await Post(admin.Client, $"/admin/gold/wallets/{user.Id}/credit", missingKey, ct)) Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        using (var r = await Post(user.Client, "/admin/gold/settings", new(), ct)) Denied(r);
        using (var r = await user.Client.PostAsync("/gold/quote", new FormUrlEncodedContent(new Dictionary<string, string> { ["Weight"] = "0.5", ["OperationId"] = Guid.NewGuid().ToString() }), ct)) Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var quoteId = Guid.NewGuid();
        using (var r = await Post(user.Client, "/gold/quote", new() { ["OperationId"] = quoteId.ToString(), ["Weight"] = "۰٫۵" }, ct)) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var before = (await f.Store.WalletAsync(user.Id, 1, ct)).Wallet!; Assert.Equal(0, before.ReservedIrt); Assert.Equal(1.25m, before.Grams);
        using (var r = await other.Client.GetAsync($"/gold/orders/{quoteId}", ct)) Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        using (var r = await Post(other.Client, $"/gold/orders/{quoteId}/submit", new(), ct)) Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        for (var repeat = 0; repeat < 2; repeat++) using (var r = await Post(user.Client, $"/gold/orders/{quoteId}/submit", new(), ct)) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var pending = (await f.Store.OrderAsync(user.Id, quoteId, false, ct))!;
        var wallet = (await f.Store.WalletAsync(user.Id, 1, ct)).Wallet!;
        Assert.Equal(10_000_000m, wallet.CashIrt); Assert.Equal(1_015_000m, wallet.ReservedIrt); Assert.Equal(1.25m, wallet.Grams);
        using (var r = await Post(op.Client, $"/operator/gold/{quoteId}/review", new() { ["Version"] = Convert.ToBase64String(pending.Version), ["Note"] = "متن حفظ شود" }, ct))
        { Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); var html = await r.Content.ReadAsStringAsync(ct); Assert.Contains("متن حفظ شود", html, StringComparison.Ordinal); Assert.Contains("نتیجه بررسی", html, StringComparison.Ordinal); }
        using (var r = await Post(user.Client, $"/operator/gold/{quoteId}/review", new() { ["Decision"] = "Approved", ["Version"] = Convert.ToBase64String(pending.Version) }, ct)) Denied(r);
        using (var r = await Post(op.Client, $"/operator/gold/{quoteId}/review", new() { ["Decision"] = "Approved", ["Version"] = Convert.ToBase64String(new byte[8]) }, ct)) Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        // A changed current rate/fee does not reprice a submitted request.
        var settings = await f.Store.SettingsAsync(ct); Assert.Equal(GoldResult.Saved, await f.Store.SettingsAsync(admin.Id, false, 5, settings.Version, ct));
        for (var repeat = 0; repeat < 2; repeat++) using (var r = await Post(op.Client, $"/operator/gold/{quoteId}/review", new() { ["Decision"] = "Approved", ["Version"] = Convert.ToBase64String(pending.Version) }, ct)) Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        wallet = (await f.Store.WalletAsync(user.Id, 1, ct)).Wallet!;
        Assert.Equal(8_985_000m, wallet.CashIrt); Assert.Equal(0, wallet.ReservedIrt); Assert.Equal(1.75m, wallet.Grams);
        Assert.Equal(1, await f.Db.Set<GoldLedgerEntry>().CountAsync(x => x.OrderId == quoteId, ct));
        var allOrders = await op.Client.GetStringAsync("/operator/gold?status=", ct);
        Assert.Contains("تأیید و ثبت نهایی", allOrders, StringComparison.Ordinal);
        Assert.Contains(quoteId.ToString(), allOrders, StringComparison.Ordinal);
        var ledger = await f.Db.Set<GoldLedgerEntry>().AsNoTracking().Where(x => x.UserId == user.Id).ToListAsync(ct);
        Assert.Equal(wallet.CashIrt, ledger.Sum(x => x.CashDeltaIrt)); Assert.Equal(wallet.Grams, ledger.Sum(x => x.GoldDeltaGrams));
        Assert.Contains(await f.Db.Set<AuditEvent>().AsNoTracking().ToListAsync(ct), x => x.Action == "Gold.LedgerPosted" && x.ActorUserId == op.Id && x.EntityId == quoteId.ToString());
        f.Db.ChangeTracker.Clear(); var entry = await f.Db.Set<GoldLedgerEntry>().FirstAsync(ct); entry.Reason = "tamper";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync(ct)); f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<SqlException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE GoldLedger SET Reason=N'tamper' WHERE Id={entry.Id}", ct));
        using (var r = await user.Client.GetAsync("/gold", ct)) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.True(r.Headers.CacheControl?.NoStore, r.Headers.CacheControl?.ToString()); Assert.False(r.Headers.CacheControl?.Public); }
        Assert.Equal(GoldResult.Saved, await f.Store.EnableWalletAsync(admin.Id, user.Id, false, wallet.Version, ct));
        Assert.Equal(GoldResult.Forbidden, (await f.Store.QuoteAsync(user.Id, Guid.NewGuid(), .1m, ct)).Result);
    }
    [RequiresSqlServerFact]
    public async Task ConcurrentReservationsApprovalAndCancelAreAtomicAndIdempotent()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = new Fixture(); await f.Init(ct);
        var admin = await f.User(RoleNames.Admin, ct); var op = await f.User(RoleNames.Operator, ct); var user = await f.User(RoleNames.Customer, ct);
        await f.Configure(admin.Id, ct); await f.Wallet(admin.Id, user.Id, 1_100_000m, ct);
        var a = (await f.Store.QuoteAsync(user.Id, Guid.NewGuid(), .5m, ct)).OrderId!.Value;
        var b = (await f.Store.QuoteAsync(user.Id, Guid.NewGuid(), .5m, ct)).OrderId!.Value;
        var reservations = await Task.WhenAll(f.Isolated(s => s.SubmitAsync(user.Id, a, ct)), f.Isolated(s => s.SubmitAsync(user.Id, b, ct)));
        Assert.Single(reservations, x => x == GoldResult.Saved); Assert.Single(reservations, x => x == GoldResult.InsufficientFunds);
        var id = reservations[0] == GoldResult.Saved ? a : b;
        var pending = (await f.Store.OrderAsync(user.Id, id, false, ct))!;
        var results = await Task.WhenAll(f.Isolated(s => s.DecideAsync(op.Id, id, GoldOrderStatus.Approved, pending.Version, null, ct)), f.Isolated(s => s.DecideAsync(user.Id, id, GoldOrderStatus.Cancelled, pending.Version, null, ct)));
        Assert.Single(results, x => x == GoldResult.Saved); Assert.Single(results, x => x == GoldResult.Conflict);
        var wallet = (await f.Store.WalletAsync(user.Id, 1, ct)).Wallet!; Assert.Equal(0, wallet.ReservedIrt);
        Assert.True((wallet.CashIrt == 85_000m && wallet.Grams == .5m) || (wallet.CashIrt == 1_100_000m && wallet.Grams == 0));
        var opId = Guid.NewGuid();
        var credits = await Task.WhenAll(f.Isolated(s => s.CreditAsync(admin.Id, user.Id, opId, GoldLedgerKind.TomanCredit, 10_000, "شارژ هم‌زمان آزمایشی", ct)), f.Isolated(s => s.CreditAsync(admin.Id, user.Id, opId, GoldLedgerKind.TomanCredit, 10_000, "شارژ هم‌زمان آزمایشی", ct)));
        Assert.All(credits, x => Assert.Equal(GoldResult.Saved, x));
        Assert.Equal(wallet.CashIrt + 10_000, (await f.Store.WalletAsync(user.Id, 1, ct)).Wallet!.CashIrt);
        Assert.Equal(GoldResult.Conflict, await f.Store.CreditAsync(admin.Id, user.Id, opId, GoldLedgerKind.TomanCredit, 20_000, "شارژ هم‌زمان آزمایشی", ct));
        // Reject and customer cancellation both release all reserved funds, without ledger entries.
        var q = (await f.Store.QuoteAsync(user.Id, Guid.NewGuid(), .001m, ct)).OrderId!.Value;
        Assert.Equal(GoldResult.Saved, await f.Store.SubmitAsync(user.Id, q, ct)); pending = (await f.Store.OrderAsync(user.Id, q, false, ct))!;
        Assert.Equal(GoldResult.Invalid, await f.Store.DecideAsync(op.Id, q, GoldOrderStatus.Rejected, pending.Version, "", ct));
        Assert.Equal(GoldResult.Saved, await f.Store.DecideAsync(op.Id, q, GoldOrderStatus.Rejected, pending.Version, "رد آزمایشی", ct));
        Assert.Equal(0, (await f.Store.WalletAsync(user.Id, 1, ct)).Wallet!.ReservedIrt);
        Assert.False(await f.Db.Set<GoldLedgerEntry>().AnyAsync(x => x.OrderId == q, ct));
        var retry = (await f.Store.QuoteAsync(user.Id, Guid.NewGuid(), .001m, ct)).OrderId!.Value;
        Assert.Equal(GoldResult.Saved, await f.Store.SubmitAsync(user.Id, retry, ct));
        var retryOrder = (await f.Store.OrderAsync(user.Id, retry, false, ct))!;
        var held = (await f.Store.WalletAsync(user.Id, 1, ct)).Wallet!;
        // Simulate a database failure while posting: cash, gold, reservation and status must all roll back.
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER TR_GoldLedger_TestFailure ON GoldLedger AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Kind=3) THROW 51003, 'Synthetic posting failure', 1; END", ct);
        try { await Assert.ThrowsAsync<DbUpdateException>(() => f.Store.DecideAsync(op.Id, retry, GoldOrderStatus.Approved, retryOrder.Version, null, ct)); }
        finally { await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER TR_GoldLedger_TestFailure", ct); f.Db.ChangeTracker.Clear(); }
        var afterFailure = (await f.Store.WalletAsync(user.Id, 1, ct)).Wallet!;
        Assert.Equal((held.CashIrt, held.ReservedIrt, held.Grams), (afterFailure.CashIrt, afterFailure.ReservedIrt, afterFailure.Grams));
        Assert.Equal(GoldOrderStatus.Pending, (await f.Store.OrderAsync(user.Id, retry, false, ct))!.Status);
        Assert.False(await f.Db.Set<GoldLedgerEntry>().AnyAsync(x => x.OrderId == retry, ct));
        Assert.Equal(GoldResult.Saved, await f.Store.DecideAsync(op.Id, retry, GoldOrderStatus.Approved, retryOrder.Version, null, ct));
    }
    [RequiresSqlServerFact]
    public async Task InvalidSourcesExpiredQuotesAccessAndMigrationRollbackFailClosed()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = new Fixture(); await f.Init(ct);
        var admin = await f.User(RoleNames.Admin, ct); var user = await f.User(RoleNames.Customer, ct); var other = await f.User(RoleNames.Customer, ct);
        await f.Configure(admin.Id, ct); await f.Wallet(admin.Id, user.Id, 1_100_000m, ct);
        Assert.Equal(GoldResult.Forbidden, await f.Store.EnableWalletAsync(user.Id, other.Id, true, null, ct));
        Assert.Equal(GoldResult.Invalid, await f.Store.EnableWalletAsync(admin.Id, admin.Id, true, null, ct));
        Assert.Equal(GoldResult.Forbidden, await f.Store.CreditAsync(user.Id, user.Id, Guid.NewGuid(), GoldLedgerKind.TomanCredit, 1, "غیرمجاز", ct));
        foreach (var provider in new[] { RateOptions.ProviderFixture, RateOptions.ProviderDisabled }) { f.Options.Provider = provider; Assert.Null(await f.Store.RateAsync(ct)); }
        f.Options.Provider = RateOptions.ProviderTgn;
        await f.Rate(ct, provider: "Fixture"); Assert.Null(await f.Store.RateAsync(ct));
        await f.Rate(ct, quality: RateQuality.Invalid); Assert.Null(await f.Store.RateAsync(ct));
        await f.Rate(ct, unit: CurrencyUnit.Irr); Assert.Null(await f.Store.RateAsync(ct));
        await f.Rate(ct, anomaly: true); Assert.Null(await f.Store.RateAsync(ct));
        await f.Rate(ct, at: f.Clock.UtcNow.AddMinutes(1)); Assert.Null(await f.Store.RateAsync(ct));
        await f.Rate(ct, at: f.Clock.UtcNow.AddMinutes(-3)); Assert.Null(await f.Store.RateAsync(ct));
        Assert.Equal(GoldResult.Unavailable, (await f.Store.QuoteAsync(user.Id, Guid.NewGuid(), .001m, ct)).Result);
        await f.Rate(ct); var quote = (await f.Store.QuoteAsync(user.Id, Guid.NewGuid(), .001m, ct)).OrderId!.Value;
        f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(3);
        Assert.Equal(GoldResult.Unavailable, await f.Store.SubmitAsync(user.Id, quote, ct));
        Assert.Equal(0, (await f.Store.WalletAsync(user.Id, 1, ct)).Wallet!.ReservedIrt);
        await f.Rate(ct); var next = (await f.Store.QuoteAsync(user.Id, Guid.NewGuid(), .001m, ct)).OrderId!.Value;
        Assert.Equal(GoldResult.Saved, await f.Store.SubmitAsync(user.Id, next, ct));
        var order = (await f.Store.OrderAsync(user.Id, next, false, ct))!;
        var w = (await f.Store.WalletAsync(user.Id, 1, ct)).Wallet!;
        Assert.Equal(GoldResult.Saved, await f.Store.EnableWalletAsync(admin.Id, user.Id, false, w.Version, ct));
        Assert.Equal(GoldResult.Forbidden, await f.Store.DecideAsync(admin.Id, next, GoldOrderStatus.Approved, order.Version, null, ct));
        Assert.Equal(GoldResult.Saved, await f.Store.DecideAsync(user.Id, next, GoldOrderStatus.Cancelled, order.Version, null, ct));
        var cash = (await f.Store.WalletAsync(user.Id, 1, ct)).Wallet!.CashIrt; Assert.Equal(1_100_000m, cash);
        // Preserve pre-existing Identity and rates through a real Down/Up. Feature balances intentionally require backup before Down.
        var rates = await f.Db.MarketRates.CountAsync(ct); var users = await f.Db.Users.CountAsync(ct);
        f.Db.ChangeTracker.Clear(); await f.Db.GetService<IMigrator>().MigrateAsync("20261016010911_AdministrationSettingsAudit", ct);
        Assert.Equal(users, await f.Db.Users.CountAsync(ct)); Assert.Equal(rates, await f.Db.MarketRates.CountAsync(ct));
        await f.Db.Database.MigrateAsync(ct);
        Assert.Empty(await f.Db.Set<GoldWallet>().ToListAsync(ct)); var settings = await f.Store.SettingsAsync(ct); Assert.False(settings.Enabled); Assert.Null(settings.FeePercent);
    }
}
