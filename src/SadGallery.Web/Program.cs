using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.WebEncoders;
using SadGallery.Application;
using SadGallery.Application.Abstractions;
using SadGallery.Infrastructure;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence;
using SadGallery.Web.HealthChecks;
using SadGallery.Web.Middleware;
using SadGallery.Web.Security;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// سرآیند Server افشا نمی‌شود (کاهش اطلاعات در اختیار مهاجم)
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// ---- لایه‌های پروژه ----
builder.Services.AddSadGalleryApplication();
builder.Services.AddSadGalleryInfrastructure(builder.Configuration);

// ---- Identity (ADR-0004) ----
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    {
        // سیاست رمز عبور: طول حداقل + رقم. (پیچیدگی نمادین اجباری، امنیت واقعی نمی‌آورد و UX را خراب می‌کند)
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        // قفل حساب در برابر حملات حدس رمز
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        // موبایل شناسه اصلی است؛ ایمیل اجباری نیست
        options.User.RequireUniqueEmail = false;

        // تأیید شماره/ایمیل طبق سیاست محصول در فاز ۶ فعال می‌شود
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<SadGalleryDbContext>()
    .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "SadGallery.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // زیر HTTPS در Production، کوکی فقط با Secure فرستاده می‌شود
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// ---- کنترل دسترسی (سرور-محور) ----
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, policy => policy
        .RequireRole(RoleNames.Admin)
        .RequireClaim(AppClaims.IsActive, "true"))
    .AddPolicy(Policies.OperatorArea, policy => policy
        .RequireRole(RoleNames.Operator, RoleNames.Admin)
        .RequireClaim(AppClaims.IsActive, "true"))
    .AddPolicy(Policies.MemberFeatures, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(AppClaims.IsActive, "true"));

// ---- MVC ----
builder.Services.AddControllersWithViews(options =>
{
    // همه درخواست‌های تغییردهنده (POST) به‌صورت پیش‌فرض نیازمند توکن ضدجعل هستند (CSRF)
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// ---- رمزگذاری HTML ----
// پیش‌فرض ASP.NET Core همه نویسه‌های غیر ASCII را به کاراکترهای عددی (مثل &#x635;)
// تبدیل می‌کند که در سایت فارسی حجم پاسخ را بالا می‌برد و بررسی متن خروجی را سخت می‌کند.
// با این تنظیم، متن فارسی به‌صورت یونیکد نوشته می‌شود؛ کاراکترهای خطرناک HTML
// (< > & " ') همچنان رمزگذاری می‌شوند، پس ایمنی XSS حفظ می‌شود.
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// ---- محدودیت نرخ درخواست (ورود/ثبت‌نام) ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Authentication, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// ---- سلامت سرویس ----
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

// ---- OpenAPI (فقط محیط توسعه منتشر می‌شود؛ در Prod سطح حمله کم می‌شود) ----
builder.Services.AddOpenApi();

var app = builder.Build();

// دستور عملیاتی: dotnet run --project src/SadGallery.Web -- --seed
// نقش‌های پایه را به‌صورت ایدِمپوتنت می‌سازد و خارج می‌شود (هیچ کاربری با رمز پیش‌فرض ساخته نمی‌شود).
if (args.Contains("--seed", StringComparer.Ordinal))
{
    using var seedScope = app.Services.CreateScope();
    var seeder = seedScope.ServiceProvider.GetRequiredService<IIdentitySeeder>();
    await seeder.SeedAsync();
    return;
}

app.UseSecurityHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// /health = زنده بودن خود سرویس (بدون وابستگی) · /health/ready = آمادگی، شامل دیتابیس
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>
/// اعلام عمومی کلاس Program تا پروژه تست یکپارچه بتواند برنامه واقعی را با
/// <c>WebApplicationFactory&lt;Program&gt;</c> اجرا کند (روش رسمی مایکروسافت برای برنامه‌های Top-level).
/// </summary>
public partial class Program;
