using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.WebEncoders;
using SadGallery.Application;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Identity;
using SadGallery.Application.Market;
using SadGallery.Application.Text;
using SadGallery.Infrastructure;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Market;
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

// ---- بازار: زنجیره نرخ (فاز ۲) ----
// تنظیمات از بخش RateOptions خوانده می‌شود؛ اعتبارنامه فقط از ENV/User Secrets.
var rateOptions = new RateOptions();
builder.Configuration.GetSection("RateOptions").Bind(rateOptions);

if (rateOptions.IsFixtureProvider && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "منبع نرخ Fixture فقط در محیط Development مجاز است. " +
        "در محیط عملیاتی مقدار RateOptions:Provider را روی Tgn یا Disabled بگذارید.");
}

builder.Services.AddSadGalleryMarket(rateOptions);
builder.Services.AddScoped<RateDisplayService>();

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
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<RateFeedHealthCheck>("rate-feed", tags: ["rates"]);

// ---- OpenAPI (فقط محیط توسعه منتشر می‌شود؛ در Prod سطح حمله کم می‌شود) ----
builder.Services.AddOpenApi();

var app = builder.Build();

// اعتبارسنجی تنظیمات بازار (بدون متوقف کردن سایت: سایت باید صفحه «نرخ در دسترس نیست» را نشان دهد،
// نه اینکه کاملاً از کار بیفتد؛ اما مشکل صریح لاگ می‌شود).
foreach (var rateConfigurationError in rateOptions.Validate())
{
    app.Logger.LogWarning("تنظیمات نرخ (RateOptions): {Error}", rateConfigurationError);
}

// دستور عملیاتی: dotnet run --project src/SadGallery.Web -- --seed
//  ۱) نقش‌های پایه را ایدِمپوتنت می‌سازد.
//  ۲) کاربران اولیه را فقط اگر در تنظیمات (SeedUsers) تعریف شده باشند و رمزشان از
//     Secret/ENV آمده باشد می‌سازد. هیچ رمز پیش‌فرضی در مخزن نیست (ADR-0011).
if (args.Contains("--seed", StringComparer.Ordinal))
{
    // ترتیب مهم است: نخست تنظیمات اعتبارسنجی می‌شود (بدون هیچ دسترسی به دیتابیس)،
    // سپس نقش‌ها و در آخر کاربران ساخته می‌شوند. پس تعریف ناقص ⇒ شکست سریع و بدون اثر جانبی.
    var seedUsers = SeedUsersConfiguration.Read(app.Configuration);

    if (seedUsers.Count > 0)
    {
        var validationErrors = SeedUsersConfiguration.Validate(seedUsers, RoleNames.All);

        if (validationErrors.Count > 0)
        {
            Console.Error.WriteLine("Seed کاربران انجام نشد؛ ابتدا این موارد را اصلاح کنید:");
            foreach (var error in validationErrors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            Environment.ExitCode = 2; // تا اسکریپت‌های اتوماسیون متوجه شکست شوند
            return;
        }
    }

    using var seedScope = app.Services.CreateScope();
    var seeder = seedScope.ServiceProvider.GetRequiredService<IIdentitySeeder>();

    await seeder.SeedAsync();
    Console.WriteLine($"نقش‌های پایه بررسی/ایجاد شدند: {string.Join(", ", RoleNames.All)}");

    if (seedUsers.Count == 0)
    {
        Console.WriteLine("هیچ کاربر اولیه‌ای تعریف نشده است (بخش SeedUsers خالی است).");
        Console.WriteLine(
            $"برای ساخت کاربر، SeedUsers را تعریف کنید و رمز را با متغیر محیطی " +
            $"{SeedUsersConfiguration.SharedPasswordEnvironmentVariable} بدهید.");
        return;
    }

    if (!app.Environment.IsDevelopment())
    {
        Console.WriteLine(
            $"هشدار: Seed کاربران در محیط «{app.Environment.EnvironmentName}» اجرا می‌شود — مطمئن شوید عمدی است.");
    }

    var outcomes = await seeder.SeedUsersAsync(seedUsers);

    foreach (var outcome in outcomes)
    {
        var userState = outcome.UserCreated ? "ساخته شد" : "از قبل موجود بود (رمز تغییر نکرد)";
        var roleState = outcome.RoleAssigned ? $"نقش {outcome.Role} اضافه شد" : $"نقش {outcome.Role} از قبل بود";
        Console.WriteLine($"  • {outcome.UserName} — {userState}؛ {roleState}");
    }

    Console.WriteLine("پایان Seed. (رمزهای عبور هرگز چاپ یا لاگ نمی‌شوند)");
    return;
}

// دستور عملیاتی: dotnet run --project src/SadGallery.Web -- --fetch-rates-once
//   یک اجرای کامل دریافت نرخ (قفل‌ها، دریافت، اعتبارسنجی، کش، ثبت تاریخچه).
//   هیچ اعتبارنامه‌ای چاپ نمی‌شود؛ فقط نتیجه و فهرست نرخ‌های دریافت‌شده.
if (args.Contains("--fetch-rates-once", StringComparer.Ordinal))
{
    var orchestrator = app.Services.GetRequiredService<RateFetchOrchestrator>();
    var report = await orchestrator.RunOnceAsync(RunTriggers.Manual);

    Console.WriteLine($"منبع نرخ: {report.ProviderId} · وضعیت: {report.Status}");

    if (report.QuotedAtUtc is { } quotedAt)
    {
        Console.WriteLine($"زمان اعلام نرخ (وقت ایران): {PersianDate.ToJalaliDateTimeText(quotedAt)}");
    }

    Console.WriteLine(
        $"نرخ معتبر: {report.AcceptedCount} · علامت‌دار (فقط ثبت): {report.FlaggedCount} · " +
        $"ردشده: {report.RejectedCount} · کلید ناشناخته: {report.UnknownKeyCount} · " +
        $"کلید غایب: {report.MissingKeyCount} · " +
        $"ثبت در دیتابیس: {(report.Persisted ? "بله" : "خیر")} · مدت: {report.DurationMs}ms");

    if (!string.IsNullOrWhiteSpace(report.Message))
    {
        Console.WriteLine($"خلاصه: {report.Message}");
    }

    var snapshot = app.Services.GetRequiredService<RateSnapshotCache>().Get();

    if (snapshot is { Publishable.Count: > 0 })
    {
        if (string.Equals(snapshot.ProviderId, FixtureRateProvider.Id, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("توجه: این اعداد از منبع «نمونهٔ آزمایشی» (Fixture) است و نرخ روز بازار نیست.");
        }

        Console.WriteLine("نرخ‌های دریافت‌شده:");
        foreach (var rate in snapshot.Publishable)
        {
            Console.WriteLine($"  • {rate.Title}: {RateFormat.Amount(rate.Amount)} {RateFormat.Unit(rate.QuoteUnit)} ({RateFormat.QualityLabel(rate.Quality)})");
        }
    }

    if (!report.Persisted && report.Status == RateFetchStatus.Success)
    {
        Console.Error.WriteLine("هشدار: نرخ‌ها دریافت شدند اما در دیتابیس ثبت نشدند (اتصال/مهاجرت دیتابیس را بررسی کنید).");
    }

    Environment.ExitCode = report.Status == RateFetchStatus.Success && report.Persisted ? 0 : 2;
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
app.MapHealthChecks("/health/rates", new HealthCheckOptions { Predicate = check => check.Tags.Contains("rates") });


app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>
/// اعلام عمومی کلاس Program تا پروژه تست یکپارچه بتواند برنامه واقعی را با
/// <c>WebApplicationFactory&lt;Program&gt;</c> اجرا کند (روش رسمی مایکروسافت برای برنامه‌های Top-level).
/// </summary>
public partial class Program;
