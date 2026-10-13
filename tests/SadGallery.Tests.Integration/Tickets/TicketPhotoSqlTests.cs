using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Media;
using SadGallery.Application.Tickets;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Tickets;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Media;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Infrastructure.Persistence.Entities;
using SadGallery.Tests.Integration.Infrastructure;
using SkiaSharp;
using Xunit;
namespace SadGallery.Tests.Integration.Tickets;

public sealed class TicketPhotoSqlTests
{
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value);
    private static byte[] Image()
    {
        using var bitmap = new SKBitmap(80,60);
        using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.Goldenrod);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png,90);
        return data.ToArray();
    }
    [RequiresSqlServerFact]
    public async Task RealUploadsArePrivate_RepliesKeepTheirPhotos_RejectsAndConflictsCleanFiles()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CatalogWebFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SadGalleryDbContext>();
        await db.Database.MigrateAsync(ct);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        foreach(var name in RoleNames.All) if(!await roles.RoleExistsAsync(name)) Assert.True((await roles.CreateAsync(new(name))).Succeeded);
        var password = "PhotoTest!1-"+Guid.NewGuid().ToString("N");
        async Task<ApplicationUser> User(string role)
        {
            var email = Guid.NewGuid().ToString("N")+"@photo.test.invalid";
            var user = new ApplicationUser { UserName=email, Email=email, IsActive=true };
            Assert.True((await users.CreateAsync(user,password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user,role)).Succeeded); return user;
        }
        var owner = await User(RoleNames.Customer); var other = await User(RoleNames.Customer); var op = await User(RoleNames.Operator);
        var options = new WebApplicationFactoryClientOptions { AllowAutoRedirect=false };
        using var a = factory.CreateClient(options); using var b = factory.CreateClient(options); using var o = factory.CreateClient(options); using var guest = factory.CreateClient(options);
        async Task Login(HttpClient client, ApplicationUser user)
        {
            var html = await client.GetStringAsync("/Account/Login",ct);
            using var response = await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>
                { ["Identifier"]=user.UserName!, ["Password"]=password, ["__RequestVerificationToken"]=Token(html) }),ct);
            Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        }
        await Login(a,owner); await Login(b,other); await Login(o,op);
        var original = Image();
        const string hiddenPayload = "THIS_APPENDED_EXECUTABLE_PAYLOAD_MUST_NOT_SURVIVE_REENCODING";
        var polyglot = original.Concat(Encoding.UTF8.GetBytes(hiddenPayload)).ToArray();
        async Task<MultipartFormDataContent> Form(string path, params (string Name, byte[] Bytes)[] pictures)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(Token(await a.GetStringAsync(path,ct))),"__RequestVerificationToken");
            form.Add(new StringContent("متن پیام و توضیح عکس"),"Body");
            foreach(var photo in pictures)
            {
                var content = new ByteArrayContent(photo.Bytes); content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                form.Add(content,"Photos",photo.Name);
            }
            return form;
        }
        using var create = await Form("/tickets/new",("../../receipt.png",polyglot));
        create.Add(new StringContent("تیکت عکس "+Guid.NewGuid().ToString("N")),"Subject"); create.Add(new StringContent("0"),"Type");
        using var created = await a.PostAsync("/tickets/new",create,ct); Assert.Equal(HttpStatusCode.Redirect,created.StatusCode);
        var id = Guid.Parse(created.Headers.Location!.ToString().Split('/').Last());
        var tickets = scope.ServiceProvider.GetRequiredService<TicketService>();
        var ticket = await tickets.GetCustomerAsync(owner.Id,id,ct); Assert.NotNull(ticket);
        var message = Assert.Single(ticket.Messages); var photoRecord = Assert.Single(message.Photos!);
        var media = scope.ServiceProvider.GetRequiredService<LocalMediaStore>();
        var store = scope.ServiceProvider.GetRequiredService<ITicketStore>();
        var photoFile = await store.GetPhotoAsync(new(owner.Id,false),photoRecord.Id,ct); Assert.NotNull(photoFile);
        Assert.True(MediaFileNaming.IsWellFormed(photoFile.StoredFileName)); Assert.DoesNotContain("receipt",photoFile.StoredFileName,StringComparison.Ordinal);
        Assert.True(media.Exists(MediaKind.PrivateFile,photoFile.StoredFileName)); Assert.False(media.Exists(MediaKind.PublicImage,photoFile.StoredFileName));
        using(var image = await a.GetAsync("/tickets/photos/"+photoRecord.Id,ct))
        {
            Assert.Equal(HttpStatusCode.OK,image.StatusCode); Assert.Equal("image/webp",image.Content.Headers.ContentType?.MediaType);
            Assert.True(image.Headers.CacheControl?.NoStore == true);
            var bytes = await image.Content.ReadAsByteArrayAsync(ct);
            Assert.NotNull(new SkiaImageProcessor().Inspect(bytes)); Assert.DoesNotContain(hiddenPayload,Encoding.UTF8.GetString(bytes),StringComparison.Ordinal);
        }
        using(var denied = await b.GetAsync("/tickets/photos/"+photoRecord.Id,ct)) Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        using(var denied = await guest.GetAsync("/tickets/photos/"+photoRecord.Id,ct)) Assert.Equal(HttpStatusCode.Redirect,denied.StatusCode);
        using(var allowed = await o.GetAsync("/tickets/photos/"+photoRecord.Id,ct)) Assert.Equal(HttpStatusCode.OK,allowed.StatusCode);
        using(var denied = await guest.GetAsync("/media/products/"+photoFile.StoredFileName,ct)) Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        var ownerHtml = await a.GetStringAsync("/tickets/"+id,ct);
        Assert.Contains("/tickets/photos/"+photoRecord.Id,ownerHtml,StringComparison.Ordinal);
        Assert.DoesNotContain(photoFile.StoredFileName,ownerHtml,StringComparison.Ordinal);
        var operatorHtml = await o.GetStringAsync("/operator/tickets/"+id,ct); Assert.Contains("/tickets/photos/"+photoRecord.Id,operatorHtml,StringComparison.Ordinal);
        var oldVersion = ticket.RowVersion;
        using(var reply = await Form("/tickets/"+id,("one.png",original),("two.png",original)))
        {
            reply.Add(new StringContent(Convert.ToBase64String(oldVersion)),"RowVersion");
            using var response = await a.PostAsync($"/tickets/{id}/reply",reply,ct); Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        }
        ticket = await tickets.GetCustomerAsync(owner.Id,id,ct); Assert.NotNull(ticket); Assert.Equal(2,ticket.Messages.Count);
        Assert.Equal(2,ticket.Messages[1].Photos!.Count); Assert.Equal(3,Directory.GetFiles(media.PrivateRoot).Length);
        using(var stale = await Form("/tickets/"+id,("stale.png",original)))
        {
            stale.Add(new StringContent(Convert.ToBase64String(oldVersion)),"RowVersion");
            using var response = await a.PostAsync($"/tickets/{id}/reply",stale,ct); Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);
        }
        Assert.Equal(3,Directory.GetFiles(media.PrivateRoot).Length);
        var count = await db.Set<SupportTicket>().CountAsync(x=>x.CustomerUserId==owner.Id,ct);
        async Task Reject(string expected, params (string Name, byte[] Bytes)[] pictures)
        {
            using var form = await Form("/tickets/new",pictures);
            form.Add(new StringContent("نباید ثبت شود"),"Subject"); form.Add(new StringContent("0"),"Type");
            using var response = await a.PostAsync("/tickets/new",form,ct);
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            Assert.Contains(expected,await response.Content.ReadAsStringAsync(ct),StringComparison.Ordinal);
            Assert.Equal(count,await db.Set<SupportTicket>().CountAsync(x=>x.CustomerUserId==owner.Id,ct));
            Assert.Equal(3,Directory.GetFiles(media.PrivateRoot).Length);
        }
        await Reject("تصویر معتبر",("valid.png",original),("fake.png",Encoding.UTF8.GetBytes("<svg onload='alert(1)'></svg>")));
        await Reject("۵ مگابایت",("large.png",new byte[TicketPhotoService.MaxFileBytes+1]));
        await Reject("حداکثر ۳",("a.png",original),("b.png",original),("c.png",original),("d.png",original));
        var submissions = scope.ServiceProvider.GetRequiredService<TicketPhotoService>();
        await Assert.ThrowsAsync<DbUpdateException>(()=>submissions.CreateAsync(new(int.MaxValue,false),new("broken FK",TicketType.Other,"body"),
            [new("failure.png","image/png",original.Length,original)],ct));
        db.ChangeTracker.Clear(); Assert.Equal(3,Directory.GetFiles(media.PrivateRoot).Length);
    }
}
