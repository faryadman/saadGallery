using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
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

public sealed class TicketFinishSqlTests
{
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value);
    [RequiresSqlServerFact]
    public async Task OwnerOperatorAndAdminCanFinishOpenTicketsWithOwnershipCsrfAndConcurrencyEnforced()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(RequiresSqlServerFactAttribute.EnvironmentVariableName)!) { InitialCatalog = "SadGallery_TicketFinish_" + Guid.NewGuid().ToString("N") };
        using var original = new CatalogWebFactory(); using var factory = original.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:SadGallery", connection.ConnectionString));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SadGalleryDbContext>();
        try
        {
            await db.Database.MigrateAsync(ct); var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            foreach (var role in RoleNames.All) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var password = "TestOnly!8-" + Guid.NewGuid().ToString("N");
            async Task<(int Id, HttpClient Client)> User(string role)
            {
                var u = new ApplicationUser { UserName = Guid.NewGuid().ToString("N") + "@test.invalid", IsActive = true };
                Assert.True((await manager.CreateAsync(u, password)).Succeeded); Assert.True((await manager.AddToRoleAsync(u, role)).Succeeded);
                var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
                using var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["Identifier"] = u.UserName, ["Password"] = password, ["__RequestVerificationToken"] = Token(await client.GetStringAsync("/Account/Login", ct)) }), ct);
                Assert.Equal(HttpStatusCode.Redirect, login.StatusCode); return (u.Id, client);
            }
            var owner = await User(RoleNames.Customer); var other = await User(RoleNames.Customer); var op = await User(RoleNames.Operator); var admin = await User(RoleNames.Admin);
            Assert.DoesNotContain("عملیات انجام نشد", await owner.Client.GetStringAsync("/tickets", ct), StringComparison.Ordinal);
            var service = scope.ServiceProvider.GetRequiredService<TicketService>();
            async Task<CustomerTicket> Ticket(TicketStatus status)
            {
                var id = (await service.CreateAsync(new(owner.Id, false), new("رگرسیون اتمام", TicketType.Other, "متن پیام اولیه"), ct))!.Value;
                var entity = await db.Set<SupportTicket>().SingleAsync(x => x.Id == id, ct); entity.Status = status; await db.SaveChangesAsync(ct);
                return (await service.GetCustomerAsync(owner.Id, id, ct))!;
            }
            async Task<HttpResponseMessage> Finish(HttpClient client, CustomerTicket ticket, bool staff, string? version = null, bool list = false)
            {
                return await client.PostAsync($"{(staff ? "/operator" : "")}/tickets/{ticket.Ticket.Id}/finish?search=SG-&page=1", new FormUrlEncodedContent(new Dictionary<string,string> {
                    ["__RequestVerificationToken"] = Token(await client.GetStringAsync("/Account/Login", ct)), ["RowVersion"] = version ?? Convert.ToBase64String(ticket.RowVersion), ["ReturnToList"] = list ? "true" : "false" }), ct);
            }
            foreach (var actor in new[] { (owner.Id, owner.Client, Staff: false), (op.Id, op.Client, Staff: true), (admin.Id, admin.Client, Staff: true) })
            foreach (var status in Enum.GetValues<TicketStatus>().Where(TicketWorkflow.CanFinish))
            {
                var ticket = await Ticket(status); var root = actor.Staff ? "/operator/tickets" : "/tickets";
                var initialDetails = await actor.Client.GetStringAsync(root + "/" + ticket.Ticket.Id, ct);
                Assert.Contains($"/{ticket.Ticket.Id}/finish", initialDetails, StringComparison.Ordinal);
                Assert.DoesNotContain(actor.Staff ? "ثبت تغییر انجام نشد" : "عملیات انجام نشد", initialDetails, StringComparison.Ordinal);
                using var finished = await Finish(actor.Client, ticket, actor.Staff, list: true);
                Assert.Equal(HttpStatusCode.Redirect, finished.StatusCode); Assert.Contains("search=SG-", finished.Headers.Location!.ToString(), StringComparison.Ordinal);
                var closed = (await service.GetCustomerAsync(owner.Id, ticket.Ticket.Id, ct))!;
                Assert.Equal(TicketStatus.Closed, closed.Ticket.Status); Assert.NotNull(closed.Ticket.ClosedAtUtc); Assert.Single(closed.Messages); Assert.Null(closed.Ticket.FirstResponseAtUtc);
                var details = await actor.Client.GetStringAsync(root + "/" + ticket.Ticket.Id, ct);
                Assert.DoesNotContain($"/{ticket.Ticket.Id}/finish", details, StringComparison.Ordinal);
                Assert.True(await db.Set<TicketEvent>().AnyAsync(x => x.TicketId == ticket.Ticket.Id && x.ActorUserId == actor.Id && x.Kind == "Ticket.Finished", ct));
            }
            var target = await Ticket(TicketStatus.New);
            using (var denied = await Finish(other.Client, target, false)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            using (var noCsrf = await owner.Client.PostAsync($"/tickets/{target.Ticket.Id}/finish", new FormUrlEncodedContent(new Dictionary<string,string> { ["RowVersion"] = Convert.ToBase64String(target.RowVersion) }), ct)) Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
            using (var conflict = await Finish(owner.Client, target, false, Convert.ToBase64String(new byte[8])))
            { Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode); var html = await conflict.Content.ReadAsStringAsync(ct); Assert.Contains("تیکت تغییر کرده", html, StringComparison.Ordinal); Assert.Contains("عملیات انجام نشد", html, StringComparison.Ordinal); }
            Assert.Equal(TicketStatus.New, (await service.GetCustomerAsync(owner.Id, target.Ticket.Id, ct))!.Ticket.Status);
            using (var finished = await Finish(owner.Client, target, false)) Assert.Equal(HttpStatusCode.Redirect, finished.StatusCode);
            var closedTarget = (await service.GetCustomerAsync(owner.Id, target.Ticket.Id, ct))!;
            var closedAt = closedTarget.Ticket.ClosedAtUtc;
            foreach (var staff in new[] { op, admin })
            {
                var csrf = Token(await staff.Client.GetStringAsync("/Account/Login", ct));
                using (var reply = await staff.Client.PostAsync($"/operator/tickets/{target.Ticket.Id}", new FormUrlEncodedContent(new Dictionary<string,string> {
                    ["__RequestVerificationToken"] = csrf, ["Body"] = "پاسخ عمومی ممنوع پس از اتمام", ["RowVersion"] = Convert.ToBase64String(closedTarget.RowVersion) }), ct)) Assert.Equal(HttpStatusCode.BadRequest, reply.StatusCode);
                using (var manual = await staff.Client.PostAsync($"/operator/tickets/{target.Ticket.Id}/status", new FormUrlEncodedContent(new Dictionary<string,string> {
                    ["__RequestVerificationToken"] = csrf, ["NextStatus"] = "1", ["RowVersion"] = Convert.ToBase64String(closedTarget.RowVersion) }), ct)) Assert.Equal(HttpStatusCode.BadRequest, manual.StatusCode);
                using (var manualDetails = await staff.Client.PostAsync($"/operator/tickets/{target.Ticket.Id}", new FormUrlEncodedContent(new Dictionary<string,string> {
                    ["__RequestVerificationToken"] = csrf, ["Status"] = "1", ["RowVersion"] = Convert.ToBase64String(closedTarget.RowVersion) }), ct)) Assert.Equal(HttpStatusCode.BadRequest, manualDetails.StatusCode);
            }
            var stillClosed = (await service.GetCustomerAsync(owner.Id, target.Ticket.Id, ct))!;
            Assert.Equal(TicketStatus.Closed, stillClosed.Ticket.Status); Assert.Single(stillClosed.Messages); Assert.Equal(closedAt, stillClosed.Ticket.ClosedAtUtc);
            Assert.DoesNotContain("بازگشایی", await op.Client.GetStringAsync("/operator/tickets", ct), StringComparison.Ordinal);
            var closedCustomerPage = await owner.Client.GetStringAsync($"/tickets/{target.Ticket.Id}", ct);
            Assert.Contains("ارسال پیام جدید و بازگشایی تیکت", closedCustomerPage, StringComparison.Ordinal);
            Assert.DoesNotContain("data-ticket-reply open", closedCustomerPage, StringComparison.Ordinal);
            Assert.Equal(TicketWriteResult.Saved, await service.ChangeAsync(new(op.Id, true), target.Ticket.Id, new("یادداشت خصوصی پس از اتمام", true, null, null, stillClosed.RowVersion), ct));
            closedTarget = (await service.GetCustomerAsync(owner.Id, target.Ticket.Id, ct))!;
            Assert.Equal(TicketStatus.Closed, closedTarget.Ticket.Status); Assert.Equal(closedAt, closedTarget.Ticket.ClosedAtUtc); Assert.Single(closedTarget.Messages);
            using (var customerMessage = await owner.Client.PostAsync($"/tickets/{target.Ticket.Id}/reply", new FormUrlEncodedContent(new Dictionary<string,string> {
                ["__RequestVerificationToken"] = Token(await owner.Client.GetStringAsync("/Account/Login", ct)), ["Body"] = "پاسخ و بازگشایی", ["RowVersion"] = Convert.ToBase64String(closedTarget.RowVersion) }), ct)) Assert.Equal(HttpStatusCode.Redirect, customerMessage.StatusCode);
            var reopened = (await service.GetCustomerAsync(owner.Id, target.Ticket.Id, ct))!;
            Assert.Equal(TicketStatus.InReview, reopened.Ticket.Status); Assert.Null(reopened.Ticket.ClosedAtUtc);
            using (var staffReply = await op.Client.PostAsync($"/operator/tickets/{target.Ticket.Id}", new FormUrlEncodedContent(new Dictionary<string,string> {
                ["__RequestVerificationToken"] = Token(await op.Client.GetStringAsync("/Account/Login", ct)), ["Body"] = "پاسخ پس از بازشدن مجاز است", ["RowVersion"] = Convert.ToBase64String(reopened.RowVersion) }), ct)) Assert.Equal(HttpStatusCode.Redirect, staffReply.StatusCode);
            var answered = (await service.GetCustomerAsync(owner.Id, target.Ticket.Id, ct))!;
            Assert.Equal(3, answered.Messages.Count); Assert.DoesNotContain(answered.Messages, x => x.Body.Contains("یادداشت خصوصی", StringComparison.Ordinal));
        }
        finally { await db.Database.EnsureDeletedAsync(ct); }
    }
}
