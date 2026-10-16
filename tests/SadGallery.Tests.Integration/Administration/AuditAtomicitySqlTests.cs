using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SadGallery.Application.Administration;
using SadGallery.Application.Market;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Tickets;
using SadGallery.Infrastructure.Administration;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Administration;

public sealed class AuditAtomicitySqlTests
{
    private sealed class FixedAuditContext(string correlation) : IAuditContext { public AuditActor? Actor => new(42, "127.0.0.1", correlation); }
    private sealed class Clock : SadGallery.Application.Abstractions.IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class Provider : SadGallery.Application.Abstractions.IRateProvider
    { public string ProviderId => "Tgn"; public bool IsConfigured => false; public Task<SadGallery.Application.Abstractions.RateProviderFetchResult> FetchAsync(CancellationToken cancellationToken) => throw new NotSupportedException(); }
    [RequiresSqlServerFact]
    public async Task ConcurrentTicketFailureRollsBackProductAndAudit_DashboardReevaluatesStoredRates()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(RequiresSqlServerFactAttribute.EnvironmentVariableName)!) { InitialCatalog = "SadGallery_AuditAtomic_" + Guid.NewGuid().ToString("N") };
        var options = new DbContextOptionsBuilder<SadGalleryDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new SadGalleryDbContext(options);
        try
        {
            await db.Database.MigrateAsync(ct);
            var user = new ApplicationUser { UserName = Guid.NewGuid().ToString("N"), IsActive = true }; db.Add(user); await db.SaveChangesAsync(ct);
            var now = DateTimeOffset.UtcNow;
            var p = new Product { Title = "محصول آزمایشی تراکنش", CreatedByUserId = user.Id, CreatedAtUtc = now };
            var t = new SupportTicket { Id = Guid.NewGuid(), CustomerUserId = user.Id, Subject = "متن خصوصی که نباید ممیزی شود", CreatedAtUtc = now, LastActivityAtUtc = now };
            var category = new ProductCategory { Name = "گروه اولیه" };
            db.AddRange(p, t, category);
            db.AddRange(new MarketRateSnapshot { AssetCode = "GOLD_GRAM_18", Amount = 1000000, QuoteUnit = CurrencyUnit.Irt, ProviderId = "Tgn", QuotedAtUtc = now.AddHours(-3), FetchedAtUtc = now, Quality = RateQuality.Live, CreatedAtUtc = now },
                new MarketRateSnapshot { AssetCode = "CURRENCY_USD", Amount = 100000, QuoteUnit = CurrencyUnit.Irt, ProviderId = "Tgn", QuotedAtUtc = now.AddDays(-3), FetchedAtUtc = now, Quality = RateQuality.Live, CreatedAtUtc = now });
            await db.SaveChangesAsync(ct);
            await using var stale = new SadGalleryDbContext(options, new FixedAuditContext("failed-transaction"));
            var staleTicket = await stale.Set<SupportTicket>().SingleAsync(x => x.Id == t.Id, ct);
            var staleProduct = await stale.Products.SingleAsync(x => x.Id == p.Id, ct);
            t.Status = TicketStatus.InReview; await db.SaveChangesAsync(ct);
            staleTicket.Status = TicketStatus.Closed; staleProduct.IsDeleted = true;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(ct));
            Assert.False(await db.Products.AsNoTracking().Where(x => x.Id == p.Id).Select(x => x.IsDeleted).SingleAsync(ct));
            Assert.Empty(await db.Set<AuditEvent>().Where(x => x.CorrelationId == "failed-transaction").ToListAsync(ct));
            await using var good = new SadGalleryDbContext(options, new FixedAuditContext("successful-transaction"));
            var product = await good.Products.SingleAsync(x => x.Id == p.Id, ct); product.IsDeleted = true;
            var ticket = await good.Set<SupportTicket>().SingleAsync(x => x.Id == t.Id, ct); ticket.Status = TicketStatus.Closed;
            var group = await good.ProductCategories.SingleAsync(x => x.Id == category.Id, ct); group.Name = "نام جدید گروه";
            await good.SaveChangesAsync(ct);
            var events = await db.Set<AuditEvent>().Where(x => x.CorrelationId == "successful-transaction").ToListAsync(ct);
            Assert.Equal(3, events.Count); Assert.All(events, e => { Assert.Equal(42, e.ActorUserId); Assert.Equal("127.0.0.1", e.IpAddress); Assert.DoesNotContain(t.Subject, e.Fields, StringComparison.Ordinal); });
            var rateOptions = new RateOptions();
            var dashboard = await new AdministrationStore(db, new Clock(), rateOptions, new Provider(), RateFreshnessPolicy.FromOptions(rateOptions)).DashboardAsync(ct);
            Assert.Contains(dashboard.Rates, x => x.AssetCode == "GOLD_GRAM_18" && x.Quality == RateQuality.Stale);
            Assert.Contains(dashboard.Rates, x => x.AssetCode == "CURRENCY_USD" && x.Quality == RateQuality.Invalid);
        }
        finally { await db.Database.EnsureDeletedAsync(ct); }
    }
}
