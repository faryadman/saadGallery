using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Application.Tickets;
using SadGallery.Domain.Tickets;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;
using SadGallery.Tests.Integration.Infrastructure;
using Xunit;

namespace SadGallery.Tests.Integration.Tickets;

public sealed class TicketInboxSqlTests
{
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value);

    private sealed class Fixture : IDisposable
    {
        public CatalogWebFactory Factory { get; } = new();
        private readonly List<HttpClient> clients = [];
        public async Task<(int Id, HttpClient Client)> User(string role, CancellationToken ct)
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SadGalleryDbContext>();
            await db.Database.MigrateAsync(ct);
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
            var email = "inbox-" + Guid.NewGuid().ToString("N") + "@test.invalid";
            var password = "TestOnly!a1-" + Guid.NewGuid().ToString("N");
            var user = new ApplicationUser { UserName = email, Email = email, IsActive = true };
            Assert.True((await manager.CreateAsync(user, password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
            var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            clients.Add(client);
            using var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Identifier"] = email, ["Password"] = password,
                ["__RequestVerificationToken"] = Token(await client.GetStringAsync("/Account/Login", ct)),
            }), ct);
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            return (user.Id, client);
        }
        public void Dispose() { foreach (var client in clients) client.Dispose(); Factory.Dispose(); }
    }

    [RequiresSqlServerFact]
    public async Task ReplyWithoutStatusKeepsStatus_EmptyInvalidAndStaleFormsRenderConversationAndPreserveDraft()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = new Fixture();
        var customer = await f.User(RoleNames.Customer, ct);
        var op = await f.User(RoleNames.Operator, ct);
        using var scope = f.Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<TicketService>();
        var id = (await svc.CreateAsync(new(customer.Id, false), new("اختیاری بودن وضعیت", TicketType.Other, "پیام اولیه"), ct))!.Value;
        var url = "/operator/tickets/" + id;
        var csrf = Token(await op.Client.GetStringAsync(url, ct));
        var ticket = (await svc.GetCustomerAsync(customer.Id, id, ct))!;
        var stale = ticket.RowVersion;
        async Task<HttpResponseMessage> Post(string? body, byte[] version, string? status = null, bool note = false)
        {
            var fields = new Dictionary<string, string> { ["RowVersion"] = Convert.ToBase64String(version), ["__RequestVerificationToken"] = csrf };
            if (body != null) fields["Body"] = body;
            if (status != null) fields["Status"] = status;
            if (note) fields["InternalNote"] = "true";
            return await op.Client.PostAsync(url, new FormUrlEncodedContent(fields), ct);
        }
        using (var sent = await Post("پاسخ بدون تغییر وضعیت", ticket.RowVersion)) Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
        ticket = (await svc.GetCustomerAsync(customer.Id, id, ct))!;
        Assert.Equal(TicketStatus.New, ticket.Ticket.Status);
        Assert.Equal("پاسخ بدون تغییر وضعیت", ticket.Messages.Last().Body);
        Assert.NotNull(ticket.Ticket.FirstResponseAtUtc);
        using (var empty = await Post(null, ticket.RowVersion))
        {
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            var html = await empty.Content.ReadAsStringAsync(ct);
            Assert.Contains("sg-ticket-thread", html, StringComparison.Ordinal);
            Assert.Contains("alert alert-danger", html, StringComparison.Ordinal);
            Assert.Contains("برای ثبت، پیام بنویسید", html, StringComparison.Ordinal);
        }
        const string draft = "متن حفظ‌شده <script>alert(1)</script>";
        using (var invalid = await Post(draft, ticket.RowVersion, "4", true))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var html = await invalid.Content.ReadAsStringAsync(ct);
            Assert.DoesNotContain(draft, html, StringComparison.Ordinal);
            Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
            Assert.Matches("id=\"internalNote\"[^>]*checked", html);
            Assert.Contains("sg-ticket-thread", html, StringComparison.Ordinal);
        }
        using (var conflict = await Post(draft, stale))
        {
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            var html = await conflict.Content.ReadAsStringAsync(ct);
            Assert.Contains("پاسخ حفظ‌شده", html, StringComparison.Ordinal);
            Assert.Contains(Convert.ToBase64String(ticket.RowVersion), WebUtility.HtmlDecode(html), StringComparison.Ordinal);
            Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        }
        ticket = (await svc.GetCustomerAsync(customer.Id, id, ct))!;
        Assert.Equal(2, ticket.Messages.Count);
        Assert.Empty((await svc.GetOperatorAsync(id, ct))!.InternalNotes);
        using (var corrected = await Post("پاسخ اصلاح‌شده", ticket.RowVersion)) Assert.Equal(HttpStatusCode.Redirect, corrected.StatusCode);
        ticket = (await svc.GetCustomerAsync(customer.Id, id, ct))!;
        using (var malformed = await Post("پیش‌نویس معتبر", [], "not-a-status"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
            var html = await malformed.Content.ReadAsStringAsync(ct);
            Assert.Contains("پیش‌نویس معتبر", html, StringComparison.Ordinal);
            Assert.Contains("sg-ticket-thread", html, StringComparison.Ordinal);
        }
        Assert.Equal(TicketStatus.New, ticket.Ticket.Status);
    }

    [RequiresSqlServerFact]
    public async Task UnreadIsPrivatePerViewer_OnlyDisplayedIncomingMessagesAreRead_ConcurrentOpensAreIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = new Fixture();
        var customer = await f.User(RoleNames.Customer, ct);
        var other = await f.User(RoleNames.Customer, ct);
        var op = await f.User(RoleNames.Operator, ct);
        var op2 = await f.User(RoleNames.Operator, ct);
        using var scope = f.Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<TicketService>();
        var inbox = scope.ServiceProvider.GetRequiredService<TicketInboxService>();
        var db = scope.ServiceProvider.GetRequiredService<SadGalleryDbContext>();
        var id = (await svc.CreateAsync(new(customer.Id, false), new("نشان پیام جدید", TicketType.Other, "اولین پیام مشتری"), ct))!.Value;
        async Task<int> Count(int user, bool staff) => (await inbox.CountsAsync(new(user, staff), [id], ct)).Tickets.GetValueOrDefault(id);
        Assert.Equal(1, await Count(op.Id, true));
        Assert.Equal(0, await Count(customer.Id, false));
        Assert.Equal(0, await Count(other.Id, false));
        var snapshot = (await svc.GetOperatorAsync(id, ct))!.Public;
        async Task Change(TicketActor actor, string body, bool note = false)
        {
            var current = (await svc.GetCustomerAsync(customer.Id, id, ct))!;
            Assert.Equal(TicketWriteResult.Saved, await svc.ChangeAsync(actor, id, new(body, note, null, null, current.RowVersion), ct));
        }
        await Change(new(op.Id, true), "یادداشت محرمانه", true);
        Assert.Equal(0, await Count(customer.Id, false));
        await Change(new(op.Id, true), "پاسخ عمومی");
        Assert.Equal(1, await Count(customer.Id, false));
        Assert.Equal(1, await Count(op.Id, true)); // Own answer does not notify its author.
        await Change(new(customer.Id, false), "پیام رسیده پس از snapshot");
        await inbox.MarkReadAsync(new(op.Id, true), id, snapshot.Messages, ct);
        Assert.Equal(1, await Count(op.Id, true)); // Later incoming message remains unread.
        Assert.Equal(2, await Count(op2.Id, true)); // Another operator has independent reads.
        var currentOp = (await svc.GetOperatorAsync(id, ct))!.Public;
        async Task OpenInSeparateScope()
        {
            using var parallel = f.Factory.Services.CreateScope();
            await parallel.ServiceProvider.GetRequiredService<TicketInboxService>().MarkReadAsync(new(op.Id, true), id, currentOp.Messages, ct);
        }
        await Task.WhenAll(OpenInSeparateScope(), OpenInSeparateScope());
        Assert.Equal(0, await Count(op.Id, true));
        Assert.Equal(2, await db.Set<TicketMessageRead>().CountAsync(x => x.UserId == op.Id && x.OperatorView, ct));
        await inbox.MarkReadAsync(new(other.Id, false), id, currentOp.Messages, ct);
        Assert.False(await db.Set<TicketMessageRead>().AnyAsync(x => x.UserId == other.Id, ct));
        await Change(new(op2.Id, true), "پاسخ تازه دوم");
        Assert.Equal(2, await Count(customer.Id, false));
        var rows = await svc.ListAsync(new(customer.Id, false), new(Search: "نشان پیام جدید"), ct);
        Assert.Equal(2, Assert.Single(rows).UnreadMessages);
        using (var response = await customer.Client.GetAsync($"/tickets/unread?ticketIds={id}", ct))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            Assert.Equal(2, json.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(2, json.RootElement.GetProperty("tickets").GetProperty(id.ToString()).GetInt32());
            Assert.DoesNotContain("body", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
        using (var denied = await customer.Client.GetAsync("/tickets/unread?operatorQueue=true", ct)) Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        using (var denied = await other.Client.GetAsync("/tickets/" + id, ct)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(2, await Count(customer.Id, false));
        using var opened = await customer.Client.GetAsync("/tickets/" + id, ct);
        Assert.True(opened.Headers.CacheControl!.NoStore);
        var html = await opened.Content.ReadAsStringAsync(ct);
        Assert.Contains("پاسخ تازه دوم", html, StringComparison.Ordinal);
        Assert.DoesNotContain("یادداشت محرمانه", html, StringComparison.Ordinal);
        Assert.Equal(0, await Count(customer.Id, false));
        Assert.Equal(2, await Count(op2.Id, true));
        await op2.Client.GetStringAsync("/operator/tickets/" + id, ct);
        Assert.Equal(0, await Count(op2.Id, true));
        using (var response = await other.Client.GetAsync($"/tickets/unread?ticketIds={id}", ct))
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            Assert.Equal(0, json.RootElement.GetProperty("total").GetInt32());
            Assert.Empty(json.RootElement.GetProperty("tickets").EnumerateObject());
        }
        // A staff member using the customer area has an independent inbox scope.
        var ownId = (await svc.CreateAsync(new(op.Id, false), new("درخواست شخصی اپراتور", TicketType.Other, "پیام صاحب درخواست"), ct))!.Value;
        var own = (await svc.GetCustomerAsync(op.Id, ownId, ct))!;
        Assert.Equal(TicketWriteResult.Saved, await svc.ChangeAsync(new(op2.Id, true), ownId,
            new("پاسخ همکار", false, null, null, own.RowVersion), ct));
        own = (await svc.GetCustomerAsync(op.Id, ownId, ct))!;
        await inbox.MarkReadAsync(new(op.Id, true), ownId, own.Messages, ct);
        Assert.Equal(1, (await inbox.CountsAsync(new(op.Id, false), [ownId], ct)).Tickets[ownId]);
        Assert.False((await inbox.CountsAsync(new(op.Id, true), [ownId], ct)).Tickets.ContainsKey(ownId));
        await inbox.MarkReadAsync(new(op.Id, false), ownId, own.Messages, ct);
        Assert.False((await inbox.CountsAsync(new(op.Id, false), [ownId], ct)).Tickets.ContainsKey(ownId));
        Assert.Equal(1, (await inbox.CountsAsync(new(op2.Id, true), [ownId], ct)).Tickets[ownId]);
        using var guest = f.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var guestCounts = await guest.GetAsync("/tickets/unread", ct);
        Assert.Equal(HttpStatusCode.Redirect, guestCounts.StatusCode);
    }
}
