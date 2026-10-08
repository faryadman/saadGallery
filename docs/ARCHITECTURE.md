# معماری — SadGallery

> هدف: معماری‌ای که مالک پروژه بتواند بخواند، توسعه دهد و نگه دارد. سادگی بر هوشمندی اولویت دارد.

---

## ۱. نمای کلان

```
┌──────────────────────────────────────────────────────────────────────────┐
│                          SadGallery.Web (ASP.NET Core 10 MVC)            │
│  Razor Views (RTL, Bootstrap 5.3) · Controllers · PWA (Manifest+SW)     │
│  Identity UI · Admin/Operator/Customer Areas · Middlewareها              │
│  Health Checks · Rate Limiting · Security Headers · ProblemDetails       │
└───────────────┬──────────────────────────────────────────────────────────┘
                │ فقط از طریق سرویس‌های Application
┌───────────────▼──────────────────────────────────────────────────────────┐
│                        SadGallery.Application                            │
│  قراردادها: IRateProvider، IRateService، IBubbleCalculator،              │
│             IProductService، ITicketService،     IKeyDerivationService،  │
│             ISecretProtector، ISmsSender، IClock، IAuditLogger، IFileStore│
│  DTO/ViewModelها · اعتبارسنجی ورودی · ماشین وضعیت تیکت · قواعد مجوزدهی   │
└───────────────┬──────────────────────────────────────────────────────────┘
                │
┌───────────────▼──────────────────────────────────────────────────────────┐
│                          SadGallery.Domain                                │
│  موجودیت‌ها · Value Objectها (Money, Unit, Purity, RateSnapshot,         │
│  BubbleResult) · قواعد تغییرناپذیر (Invariant) · Enumها · بدون وابستگی    │
└───────────────▲──────────────────────────────────────────────────────────┘
                │
┌───────────────┴──────────────────────────────────────────────────────────┐
│                      SadGallery.Infrastructure                            │
│  EF Core DbContext + Migrations · Identity Store · Providerهای واقعی نرخ  │
│  HttpClientFactories + Polly (Retry/Timeout/CircuitBreaker) · Cache (IMemoryCache)│
│  BackgroundService (PriceFetchJob) · File Store محلی/آینده‌نگر · SMS Gateway│
│  AES-GCM SecretProtector + PBKDF2/HKDF KeyDerivation · Audit/Security Log  │
└──────────────────────────────────────────────────────────────────────────┘
       │                              │
  SQL Server                     منابع بیرونی
  (EF Core)                 (API نرخ، درگاه پیامک)
```

### جهت وابستگی (غیرقابل نقض)
`Web → Application → Domain` و `Infrastructure → Application → Domain`.
- `Domain` به هیچ پروژه‌ای وابسته نیست.
- `Web` **نباید** به اینترفیس‌های زیرساختی مثل `IRateProvider` یا `DbContext` وابسته باشد؛ فقط سرویس‌های Application را می‌شناسد. (استثنای مستند: ثبت DI در `Program.cs`/`DependencyInjection.cs`)
- ثبت وابستگی‌ها فقط در یک نقطه: `Infrastructure.DependencyInjection.AddSadGalleryInfrastructure(...)` و `Application.DependencyInjection.AddSadGalleryApplication(...)`.

### جای منطق کجاست؟
| نوع منطق | محل درست | محل ممنوع |
| --- | --- | --- |
| محاسبه حباب، ارزش ذاتی، تبدیل عیار/وزن | `Domain` (Value Object/Calculator) — قابل تست بدون I/O | هیچ‌کدام از موارد دیگر |
| جریان کاری تیکت، سیاست دسترسی، هم‌ترازی نرخ | `Application` | Controller |
| فراخوانی HTTP، SQL، فایل، Cache | `Infrastructure` | Controller/View |
| نحوه نمایش، قالب‌بندی، اعداد فارسی، RTL | `Web` (Helpers/TagHelpers) | Domain |
| اعتبارسنجی شکل ورودی (الزامی/طول/الگو) | `Application` DTO + `Domain` invariant | JavaScript |

> **قاعده طلایی:** هر قاعده‌ای که اگر نقض شود باعث «عدد غلط پولی» یا «افشای داده» می‌شود، باید در `Domain`/`Application` باشد و تست داشته باشد.

## ۲. نسخه‌ها و وابستگی‌های تأییدشده (بررسی‌شده در 2026-10-05)

> **تأیید عملی (2026-10-05):** در سندباکس نصب شد `SDK 10.0.401` (Host `10.0.12`) و restore موفق `Microsoft.EntityFrameworkCore.SqlServer 10.0.12` از NuGet در ۳.۵ ثانیه. قالب `dotnet new mvc` موجود است ⇒ فاز ۱ قابل ساخت و تست واحد در همین محیط است.

| جزء | نسخه هدف | وضعیت/دلیل |
| --- | --- | --- |
| .NET SDK | `10.0.4xx` (آزمون‌شده: **10.0.401**؛ Host/Runtime: **10.0.12**، 2026-09-08) | LTS فعال، پشتیبانی تا 2028-11-14. .NET 8/9 در 2026-11-10 پایان پشتیبانی دارند ⇒ .NET 10 انتخاب درست است |
| ASP.NET Core | 10.x (MVC) | انتخاب MVC بر Razor Pages: تعداد نقش‌ها/پنل‌های متفاوت و ساختار Areaها برای این پروژه خواناتر است (ADR-0001) |
| EF Core + `Microsoft.EntityFrameworkCore.SqlServer` | 10.x | هم‌نسخه با .NET 10 |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 10.x | نقش‌ها و ورود استاندارد (ADR-0004) |
| Bootstrap | `5.3.8` (2025-08-25) | شاخه پایدار جاری با پشتیبانی RTL داخلی. Bootstrap 6 در آلفا است و برای Production مجاز نیست |
| فونت فارسی | Vazirmatn (Self-host، WOFF2، Subset) | حریم خصوصی (بدون CDN) و عملکرد بهتر + پشتیبانی کامل اعداد فارسی |
| نمودار | Chart.js `v4` (Self-host) | سبک، بدون وابستگی سنگین، سازگار با RTL |
| لاگ | `Serilog.AspNetCore` + Sink فایل/کنسول (+Sink DB اختیاری در فاز ۸) | ساختاریافته + Redaction داده حساس |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly v8) | Retry/Timeout/CircuitBreaker استاندارد و نگهداری‌شده |
| تست | `xunit.v3 4.0.1` + `xunit.runner.visualstudio 4.0.0` + `Microsoft.AspNetCore.Mvc.Testing 10.0.12` · اجرا روی **Microsoft.Testing.Platform** با opt-in در `global.json` | **بدون FluentAssertions** (مجوز تجاری از v8)؛ بدون `Microsoft.NET.Test.Sdk`/`coverlet` (مخصوص VSTest). xunit v2 منسوخ است — ADR-0010 |
| قالب Solution | `SadGallery.sln` **کلاسیک** (SDK 10 پیش‌فرض `.slnx` می‌سازد) | سازگاری ابزارها؛ مهاجرت آینده یک‌دستوری — ADR-0010 |
| Bootstrap / فونت | Bootstrap `5.3.8` (RTL) و Vazirmatn `33.0.3` — **self-host در مخزن** | بدون CDN: حریم خصوصی، آفلاین PWA، پایداری دسترسی — ADR-0010 |
| PWA | manifest + Service Worker دست‌ساز | بدون Framework سنگین؛ کنترل کامل Cache (ADR-0008) |

**قواعد وابستگی (الزامی پیش از افزودن هر بسته):**
1. نسخه دقیقاً Pin شود (`Directory.Packages.props` با Central Package Management).
2. بسته باید نگهداری‌شده و دارای License سازگار باشد.
3. `dotnet list package --vulnerable --include-transitive` باید صفر آسیب‌پذیری High/Critical بدهد.
4. افزودن هر بسته در `docs/CHANGELOG.md` + دلیل یک‌خطی ثبت شود.

## ۳. ساختار Solution (پیشنهاد فاز ۱)

```
SadGallery.sln
Directory.Build.props            # TargetFramework, Nullable, TreatWarningsAsErrors, LangVersion
Directory.Packages.props         # Central Package Management (نسخه‌های Pin‌شده)
src/
  SadGallery.Domain/             # Entities, ValueObjects, Enums, Invariants, Calculators
  SadGallery.Application/        # Contracts, DTOs, Services, Policies, Validators
  SadGallery.Infrastructure/     # EF Core, Providers, Cache, Jobs, Crypto, Files, Sms, Identity Store
  SadGallery.Web/                # MVC, Areas(Admin/Operator), Views(RTL), wwwroot, PWA
tests/
  SadGallery.Tests.Unit/
  SadGallery.Tests.Integration/
scripts/
  dev-setup.sh, test.sh
```

### نگاشت ماژول‌ها در پروژه Web
```
Areas/Admin/        # نقش Admin: کاربران، نقش‌ها، منابع نرخ، تنظیمات، گزارش‌ها، Audit
Areas/Operator/     # نقش Operator: محصولات، تصاویر، صف تیکت، گزارش عملیاتی مجاز
Areas/Account/      # ورود/ثبت‌نام/OTP/بازیابی رمز/پروفایل
Controllers/        # عمومی: Home, Market, Coins, Currencies, Crypto, Calculator, Bubble, Products, Tickets(customer), About, Contact
Views/Shared/       # Layout RTL، Partialها (RateCard، TrendChart، StatusBadge)
wwwroot/            # css/fonts/js/img، manifest.webmanifest، service-worker.js، offline.html
```

## ۴. جریان داده نرخ (قلب سیستم)

```
[BackgroundService: PriceFetchJob]  هر N ثانیه (از Config، پیش‌فرض 60s)
        │  1) قفل اجرای هم‌زمان (semaphore + قفل دیتابیسی برای چند نمونه)
        │  2) انتخاب Provider فعال (اصلی ⇒ جایگزین در صورت خطا)
        ▼
[IRateProvider.FetchAsync(ct)] → HTTP با Timeout، Retry محدود، Circuit Breaker
        ▼  JSON خام
[DTO اعتبارسنجی‌شده] → نگاشت + نرمال‌سازی واحد (ریال ⇄ تومان صریح) + زمان‌ها
        ▼
[Application: RateSnapshotService] → مقایسه با آخرین نرخ، ساخت رکورد MarketPrice + PriceHistory
        ▼
[SQL Server]  +  [IMemoryCache: آخرین نرخ‌ها با TTL]
        ▼
[Web: RateCard / MarketController]  → نمایش مقدار + واحد + منبع + زمان + وضعیت تازگی
        ▼
[PWA Service Worker]  → کش فقط برای نمایش آفلاین، همیشه با برچسب زمان
```

**سیاست تازگی (Staleness) — سه وضعیت نمایش اجباری**
| وضعیت | شرط (پیش‌فرض، قابل تنظیم) | نمایش |
| --- | --- | --- |
| 🟢 زنده (Live) | `now - FetchedAtUtc ≤ 2 × FetchInterval` و آخرین دریافت موفق بوده | «لحظه‌ای» + زمان |
| 🟡 تأخیری (Delayed) | `2 × FetchInterval < age ≤ StaleThreshold` (پیش‌فرض ۱۵ دقیقه) | «تأخیری» + زمان + نشان هشدار |
| 🔴 کهنه (Stale) | `age > StaleThreshold` یا آخرین دریافت ناموفق | «آخرین نرخ ثبت‌شده» + زمان + پیام «داده‌ها در حال به‌روزرسانی نیست؛ استعلام نهایی با فروشگاه» |

قواعد سخت:
- در حالت 🔴 هیچ محاسبه‌ای (حباب، قیمت محاسباتی محصول، درصد تغییر) بدون برچسب کهنگی و پرچم `IsTrusted=false` انجام نمی‌شود.
- درصد تغییر فقط نسبت به یک **دوره مرجع مشخص** (مثلاً «آخرین نرخ روز کاری قبل») محاسبه می‌شود، نه «آخرین رکورد در جدول».
- هیچ نرخ ساختگی برای «قشنگ شدن» نمودار ساخته نمی‌شود.

### ۴.۱ وضعیت پیاده‌شدهٔ جریان نرخ (فاز ۲ — اجراشده)

```
[BackgroundService: RateFetchBackgroundService]
        │  بازه از config · CancellationToken · عقب‌نشینی نمایی (سقف MaxBackoffMinutes)
        ▼
[RateFetchOrchestrator]  ← Singleton (IServiceScopeFactory برای مخزن اسکوپ‌شده)
        │ ۱) قفل درون‌فرایندی (SemaphoreSlim، رد اجرای هم‌زمان)
        │ ۲) قفل چند-نمونه‌ای: UPDATE شرطی روی MarketFetchLease (اجارهٔ منقضی‌شونده)
        │ ۳) ثبت شروع اجرا (MarketRateFetchRuns)
        ▼
[IRateProvider]  ── TgnRateProvider (HTTP واقعی) | FixtureRateProvider (توسعه/تست)
        │  سقف بایت · مهلت · فقط https و دامنه مجاز · آدرس هرگز لاگ نمی‌شود
        ▼
[TgnResponseParser]  (Application) نگاشت نام‌محور؛ کلید غایب = اطلاع، نه خطا
        ▼
[RateNormalizer]  مقیاس منبع (یک بار) · بازه معقول · تازگی · جهش غیرعادی
        ├─ Accepted   ⇒ کش (RateSnapshotCache) + MarketRates
        └─ Flagged    ⇒ فقط MarketRates (IsAnomalySuspected / Quality=Invalid)
        ▼
[RateDisplayService] ← HomeController  (مسیر گرم: صفر کوئری دیتابیس، ۳–۸ms اندازه‌گیری‌شده)
        ▼
صفحه عمومی (کارت‌ها + برچسب لحظه‌ای/تأخیری/آخرین نرخ ثبت‌شده) · /health/rates
```

خرابی منبع ⇒ آخرین مجموعه معتبر از کش با برچسب کهنگی نمایش داده می‌شود. خرابی دیتابیس ⇒ کش باز هم به‌روز می‌شود (با هشدار) تا نمایش قطع نشود. جزئیات تصمیم‌ها: ADR-0012.

### ۴.۲ لایه رابط مشتری فاز ۳ (اجراشده)

```
MarketController (Web)
├── /market, /market/rate/{code}  → RateDisplayService (کش، بدون کوئری در مسیر گرم)
├── /market/bubble  → IBubbleCalculator (Application؛ ورودی صریح) ← پیش‌پر از کش + CoinStandards
├── /market/gold    → IGoldCalculator   (Application؛ گرم/مثقال/سوت، عیار ۰–۲۴)
├── /market/history (Policy) → RateHistoryService → IRateStore.GetHistoryAsync → MarketChart (SVG درون‌خطی)
├── /market/advanced-bubble (Policy) → مقایسه همه CoinStandards با نرخ‌های لحظه‌ای
└── /api/rates/{code}/history (Policy) → JSON
```

- `CoinStandards` (Domain): وزن/عیار رسمی مسکوکات با منبع مستند و تأیید در انتظار مالک (`Q-COIN-1`).
- `PersianNumber` (Application/Text): خواندن ورودی عددی فارسی فرم‌ها؛ سخت‌گیر اما فارسی‌پسند.
- `MarketChart` (Application/Market): نمودار SVG درون‌خطی بدون کتابخانه خارجی (ADR-0013 §۷).
- تصمیم‌های کامل: ADR-0013.

## ۵. Provider/Adapter نرخ

```csharp
public interface IRateProvider
{
    string Key { get; }                                  // مثلاً "tala.ir" یا "navasan"
    RateProviderCapabilities Capabilities { get; }        // کدام دارایی‌ها؟ ارز؟ رمزارز؟ مظنه؟ تاریخچه؟
    Task<ProviderFetchResult> FetchAsync(RateRequest request, CancellationToken ct);
}
```
- `RateRequest` مشخص می‌کند چه چیزی لازم است (کلیدهای دارایی) تا درخواست حداقلی بماند.
- `ProviderFetchResult` شامل `IReadOnlyList<RawRateItem>` + `QuotedAtUtc` (زمان اعلام منبع) + هدرهای نرخ‌محدودکننده + وضعیت موفقیت است. **`FetchedAtUtc` در لایه Application مهر می‌شود** تا از «زمان اعلام» جدا بماند.
- هر Provider در `Infrastructure/RateProviders/<Name>/` با: کلاینت اختصاصی، DTO، Mapper قابل تست، و Fixture JSON در پروژه تست.
- افزودن منبع جدید = افزودن یک کلاس + رجیستر در DI + Fixture تست. **هیچ تغییری در Domain لازم نیست.**
- انتخاب منبع از جدول `PriceProviders` (فعال/غیرفعال/اولویت) خوانده می‌شود، نه هاردکد.

## ۶. الگوهای استفاده‌شده و آگاهانه **استفاده‌نشده**

**استفاده می‌کنیم:** Dependency Injection، Provider/Adapter، Options Pattern (`IOptions<T>` + ValidateOnStart)، Result/`ValidationResult` برای خطاهای قابل انتظار، ماشین وضعیت تیکت، Value Object برای پول/واحد.

**استفاده نمی‌کنیم (با دلیل):**
- **Repository روی EF Core:** `DbContext` خودش Unit of Work و Repository است؛ لایه اضافه بدون سود مشخص ⇒ حذف (ADR-0003).
- **MediatR/CQRS:** تعداد سناریوها کم است و تیم یک‌نفره؛ سربار شناختی بدون بازده.
- **AutoMapper:** نگاشت‌ها ساده‌اند؛ نگاشت دستی صریح، قابل دیباگ و قابل تست است.
- **Microservices/Event Bus:** خارج از مقیاس این محصول؛ یک اپلیکیشن یکپارچه با ماژول‌های روشن کافی است.

## ۷. چارچوب‌های عرضی (Cross-cutting)

| موضوع | رویکرد |
| --- | --- |
| Log | `ILogger` ساختاریافته با Scoped/CorrelationId؛ Redaction اسرار. **Serilog فعلاً افزوده نشده** (وابستگی اضافه بدون سود فوری)؛ اگر در فاز ۸ نیاز به Sink فایل/DB شد، افزودن آن با ADR بررسی می‌شود |
| رمزگذاری HTML | `WebEncoderOptions` با `UnicodeRanges.All` تا متن فارسی به‌صورت یونیکد نوشته شود (کاراکترهای خطرناک همچنان رمزگذاری می‌شوند) — BUG-003 |
| خطا | `IExceptionHandler`/Middleware ⇒ `ProblemDetails` + پیام فارسی کاربرپسند + Log کامل سمت سرور بدون داده حساس |
| CancellationToken | از `HttpContext.RequestAborted` تا EF/HTTP؛ طول عمر Job از `IHostApplicationLifetime.ApplicationStopping` |
| زمان | `IClock` تزریق‌شده (تست‌پذیر)؛ ذخیره UTC؛ نمایش جلالی فقط در `Web` |
| پول | `decimal(18,2)` برای ریال/تومان، `decimal(18,4)` برای اونس/رمزارز، `decimal(9,4)` برای وزن گرم |
| کش | `IMemoryCache` برای نرخ‌های جاری؛ ورودی‌های محصول/دسته با `IChangeToken` باطل می‌شوند (بدون Cache خیلی زود کهنه شود) |
| پیکربندی | `IOptions<RateOptions>`, `IOptions<SecurityOptions>`، `IOptions<StorageOptions>` … با `ValidateDataAnnotations().ValidateOnStart()` |
| صفحات خطا | `/error` عمومی، `/error/404`, `/error/403` فارسی با راهنمای بازگشت |

## ۸. تصمیم‌های مرتبط

ADR-0001 (MVC)، ADR-0002 (SQL Server تنها DB)، ADR-0003 (بدون Repository)، ADR-0004 (Identity)، ADR-0005 (Provider نرخ)، ADR-0006 (رمزنگاری/عبارت محرمانه)، ADR-0007 (پردازش درون‌فرایندی به‌جای صف)، ADR-0008 (PWA دست‌ساز)، ADR-0009 (واحد پول و عیار). مسیر: `docs/DECISIONS/`.
