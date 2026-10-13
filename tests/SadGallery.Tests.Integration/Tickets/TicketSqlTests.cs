using System.Net;
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

public sealed class TicketSqlTests
{
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",RegexOptions.CultureInvariant).Groups[1].Value);
    private sealed class FixedClock(DateTimeOffset now) : SadGallery.Application.Abstractions.IClock
    {
        public DateTimeOffset UtcNow => now;
    }
    private static readonly WebApplicationFactoryClientOptions ClientOptions=new() { AllowAutoRedirect=false };
    [RequiresSqlServerFact]
    public async Task RealIdentityUsersCannotReadOrWriteEachOthersTickets_NotesArePrivate_XssIsEncoded_ConcurrencyAndWorkflowAreEnforced()
    {
        var ct=TestContext.Current.CancellationToken;
        using var factory=new CatalogWebFactory();
        using var scope=factory.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<SadGalleryDbContext>();
        await db.Database.MigrateAsync(ct);
        var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        foreach(var role in RoleNames.All)
            if(!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
        var suffix=Guid.NewGuid().ToString("N");
        var password="TestOnly!a1-"+Guid.NewGuid().ToString("N");
        async Task<ApplicationUser> AddUser(string name,string role)
        {
            var u=new ApplicationUser { UserName=name+suffix+"@test.invalid",Email=name+suffix+"@test.invalid",IsActive=true };
            Assert.True((await manager.CreateAsync(u,password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(u,role)).Succeeded);return u;
        }
        var customer=await AddUser("a",RoleNames.Customer);
        var other=await AddUser("b",RoleNames.Customer);
        var op=await AddUser("op",RoleNames.Operator);
        using var a=factory.CreateClient(ClientOptions); using var b=factory.CreateClient(ClientOptions); using var o=factory.CreateClient(ClientOptions);
        async Task Login(HttpClient client,ApplicationUser user)
        {
            var page=await client.GetStringAsync("/Account/Login",ct);
            using var response=await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>
                { ["Identifier"]=user.UserName!,["Password"]=password,["__RequestVerificationToken"]=Token(page) }),ct);
            Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        }
        await Login(a,customer);await Login(b,other);await Login(o,op);
        var svc=scope.ServiceProvider.GetRequiredService<TicketService>();
        const string payload="<script>alert('ticket')</script>";
        using var created=await a.PostAsync("/tickets/new",new FormUrlEncodedContent(new Dictionary<string,string>
            { ["Subject"]="موضوع "+payload,["Type"]="0",["Body"]=payload,
              ["__RequestVerificationToken"]=Token(await a.GetStringAsync("/tickets/new",ct)) }),ct);
        Assert.Equal(HttpStatusCode.Redirect,created.StatusCode);
        Guid? id=Guid.Parse(created.Headers.Location!.ToString().Split('/').Last());
        var ticket=await svc.GetCustomerAsync(customer.Id,id.Value,ct);Assert.NotNull(ticket);
        Assert.StartsWith("SG-",ticket.Ticket.TrackingCode,StringComparison.Ordinal);
        var url="/tickets/"+id;
        using(var denied=await b.GetAsync(url,ct)) Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        using(var random=await b.GetAsync("/tickets/"+Guid.NewGuid(),ct)) Assert.Equal(HttpStatusCode.NotFound,random.StatusCode);
        var page=await a.GetStringAsync(url,ct);
        Assert.DoesNotContain(payload,page,StringComparison.Ordinal); Assert.Contains("&lt;script&gt;",page,StringComparison.Ordinal);
        using(var denied=await b.PostAsync(url+"/reply",new FormUrlEncodedContent(new Dictionary<string,string>
            { ["Body"]="unauthorized",["RowVersion"]=Convert.ToBase64String(ticket.RowVersion),
                ["__RequestVerificationToken"]=Token(await b.GetStringAsync("/tickets/new",ct)) }),ct))
            Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        Assert.Equal(1,await db.Set<TicketMessage>().CountAsync(x=>x.TicketId==id,ct));
        Assert.True(await db.Set<TicketEvent>().AnyAsync(x=>x.ActorUserId==other.Id && x.Kind=="SecurityEvent.AccessDenied",ct));
        using(var denied=await a.GetAsync("/operator/tickets",ct)) Assert.Equal(HttpStatusCode.Redirect,denied.StatusCode);
        async Task<TicketWriteResult> Change(TicketChange change) => await svc.ChangeAsync(new(op.Id,true),id.Value,change,ct);
        Assert.Equal(TicketWriteResult.Invalid,await Change(new("reply",false,TicketStatus.Answered,null,ticket.RowVersion)));
        using(var reviewed=await o.PostAsync("/operator/tickets/"+id,new FormUrlEncodedContent(new Dictionary<string,string>
            { ["Status"]="1",["RowVersion"]=Convert.ToBase64String(ticket.RowVersion),
              ["__RequestVerificationToken"]=Token(await o.GetStringAsync("/operator/tickets/"+id,ct)) }),ct))
            Assert.Equal(HttpStatusCode.Redirect,reviewed.StatusCode);
        ticket=await svc.GetCustomerAsync(customer.Id,id.Value,ct);Assert.NotNull(ticket);
        var stale=ticket.RowVersion;
        svc=new TicketService(new SadGallery.Infrastructure.Tickets.TicketStore(db,new FixedClock(ticket.Ticket.LastActivityAtUtc)));
        const string secret="INTERNAL-ONLY-DO-NOT-DISCLOSE";
        Assert.Equal(TicketWriteResult.Saved,await Change(new(secret,true,null,op.Id,ticket.RowVersion)));
        Assert.Equal(TicketWriteResult.Conflict,await Change(new("stale",false,TicketStatus.Answered,null,stale)));
        var publicPage=await a.GetStringAsync(url,ct);Assert.DoesNotContain(secret,publicPage,StringComparison.Ordinal);
        var operatorPage=await o.GetStringAsync("/operator/tickets/"+id,ct);Assert.Contains(secret,operatorPage,StringComparison.Ordinal);
        Assert.DoesNotContain(secret,System.Text.Json.JsonSerializer.Serialize(await svc.GetCustomerAsync(customer.Id,id.Value,ct)),StringComparison.Ordinal);
        ticket=await svc.GetCustomerAsync(customer.Id,id.Value,ct);Assert.NotNull(ticket);
        Assert.Equal(TicketWriteResult.Saved,await Change(new("پاسخ پشتیبانی",false,TicketStatus.Answered,null,ticket.RowVersion)));
        ticket=await svc.GetCustomerAsync(customer.Id,id.Value,ct);Assert.NotNull(ticket);Assert.NotNull(ticket.Ticket.FirstResponseAtUtc);
        var first=ticket.Ticket.FirstResponseAtUtc;
        Assert.Equal(TicketWriteResult.Saved,await Change(new(null,false,TicketStatus.Closed,null,ticket.RowVersion)));
        ticket=await svc.GetCustomerAsync(customer.Id,id.Value,ct);Assert.NotNull(ticket);Assert.NotNull(ticket.Ticket.ClosedAtUtc);
        Assert.Equal(TicketWriteResult.Saved,await svc.ChangeAsync(new(customer.Id,false),id.Value,new("پاسخ مشتری",false,null,null,ticket.RowVersion),ct));
        ticket=await svc.GetCustomerAsync(customer.Id,id.Value,ct);Assert.NotNull(ticket);
        Assert.Equal(TicketStatus.InReview,ticket.Ticket.Status);Assert.Null(ticket.Ticket.ClosedAtUtc);Assert.Equal(first,ticket.Ticket.FirstResponseAtUtc);
        Assert.Equal(TicketWriteResult.Invalid,await svc.ChangeAsync(new(customer.Id,false),id.Value,new(secret,true,null,null,ticket.RowVersion),ct));
        // Fresh real-user partition: exercise limiter and persisted security event without changing the ticket.
        var csrf=Token(await a.GetStringAsync(url,ct));
        var limited=false;
        for(var i=0;i<12;i++)
        {
            using var response=await a.PostAsync(url+"/reply",new FormUrlEncodedContent(new Dictionary<string,string>
                { ["Body"]="reply",["RowVersion"]=Convert.ToBase64String(stale),["__RequestVerificationToken"]=csrf }),ct);
            if(response.StatusCode==HttpStatusCode.TooManyRequests) { limited=true;break; }
        }
        Assert.True(limited);
        Assert.True(await db.Set<TicketEvent>().AnyAsync(x=>x.ActorUserId==customer.Id && x.Kind=="SecurityEvent.RateLimitHit",ct));
        Assert.Equal(TicketWriteResult.Invalid,await svc.ChangeAsync(new(op.Id,true),id.Value,new(null,false,null,other.Id,ticket.RowVersion),ct));
        Assert.Null(await svc.CreateAsync(new(customer.Id,false),new("too long",TicketType.Other,new string('a',4001)),ct));
        Assert.Null(await svc.CreateAsync(new(customer.Id,false),new("unknown product",TicketType.PriceInquiry,"body",int.MaxValue),ct));
        var secondId=await svc.CreateAsync(new(customer.Id,false),new("second",TicketType.Other,"second"),ct);
        Assert.NotNull(secondId);
        var second=await svc.GetCustomerAsync(customer.Id,secondId.Value,ct);Assert.NotNull(second);
        Assert.NotEqual(ticket.Ticket.TrackingCode,second.Ticket.TrackingCode);
        var filtered=await svc.ListAsync(new(op.Id,true),new(TicketStatus.InReview, ticket.Ticket.TrackingCode,
            ticket.Ticket.CreatedAtUtc.AddMinutes(-1),ticket.Ticket.CreatedAtUtc.AddMinutes(1)),ct);
        Assert.Single(filtered);
        Assert.Empty(await svc.ListAsync(new(customer.Id,false),new(Search:ticket.Ticket.TrackingCode,Until:ticket.Ticket.CreatedAtUtc),ct));
        Assert.Single(await svc.ListAsync(new(customer.Id,false),new(Search:"موضوع <script>"),ct));
        using(var listing=await a.GetAsync("/tickets",ct)) Assert.Equal(HttpStatusCode.OK,listing.StatusCode);
        var queue=await o.GetStringAsync("/operator/tickets?search="+ticket.Ticket.TrackingCode,ct);
        Assert.Contains(ticket.Ticket.TrackingCode,queue,StringComparison.Ordinal);
        Assert.DoesNotContain(secret,queue,StringComparison.Ordinal);
        var list=await svc.ListAsync(new(customer.Id,false),new(Search:ticket.Ticket.TrackingCode),ct);
        Assert.Single(list);Assert.Equal(id,list[0].Id);
        Assert.Empty(await svc.ListAsync(new(other.Id,false),new(Search:ticket.Ticket.TrackingCode),ct));
        // Test rows intentionally remain in the disposable test database for inspection.
    }
}
