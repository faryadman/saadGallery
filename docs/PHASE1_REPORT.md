# گزارش فاز ۱ — اسکلت Solution و پایه کیفیت

| کلید | مقدار |
| --- | --- |
| فاز | ۱ — اسکلت Solution و پایه کیفیت |
| تاریخ | 2026-10-05 |
| وضعیت | **انجام‌شده در سندباکس** — ۳ مورد نیازمند اجرای مالک روی ماشین دارای SQL Server |
| ثبت در Git | کامیت‌های محلی `a7a7cd9` (کد)، `c170635` (رفع باگ اسکریپت‌ها)، `9850678` (مستندات) روی `4868ff3` — **push در انتظار توکن مالک** |
| فاز قبلی | فاز صفر (تأییدشده توسط مالک) |
| مبنا | `docs/ROADMAP.md` — بخش فاز ۱ |

---

## ۰) جمع‌بندی یک‌نگاه

اسکلت کامل Solution با ۶ پروژه ساخته شد؛ Identity با ۳ نقش و Seed ایدِمپوتنت، DbContext و **مهاجرت اولیه**، صفحه خطای فارسی، `/health`، و پشته تست (۳۴ واحد + ۱۷ یکپارچه) — همه با **Build بدون خطا/هشدار** و **تست‌های سبز/ثبت‌شده**.
در جریان فاز، سه باگ واقعی کشف و برطرف شد (BUG-001 تا BUG-003) و پشته تست پس از کشف «منسوخ بودن xunit v2» به `xunit.v3` مهاجرت کرد (ADR-0010، بازنگری‌شده).

## ۱) چه کاری انجام شد (نگاشت به معیارهای پذیرش)

| معیار پذیرش ROADMAP | وضعیت | شاهد |
| --- | --- | --- |
| Solution با ۶ پروژه | ✅ | `SadGallery.sln` (Domain/Application/Infrastructure/Web/Tests.Unit/Tests.Integration) |
| CPM با نسخه‌های Pin | ✅ | `Directory.Packages.props` + `Directory.Build.props` (net10.0، Nullable، `TreatWarningsAsErrors`) |
| Identity با ۳ نقش و Seed ایدِمپوتنت | ✅ کد + تست نوشته‌شده | `RoleNames`، `IdentitySeeder` (دو بار اجرا = بدون تکرار؛ تست Skip تا اجرای مالک) |
| DbContext + مهاجرت اولیه | ✅ | `20261005172327_InitialIdentity` + `database/scripts/InitialIdentity.sql` |
| صفحه خطای فارسی | ✅ | `/no-such-page` → 404 «صفحه یافت نشد»؛ `/error` → 500 «خطای غیرمنتظره»؛ `/error/429` هم پوشش دارد |
| `/health` | ✅ | 200 + `Healthy` (JSON/متن)؛ `/health/ready` بدون DB = 503 (درست) |
| ۱ تست واحد + ۲ تست یکپارچه (حداقل) | ✅ فراتر از حداقل | ۳۴ واحد + ۱۷ یکپارچه (۱ Skip صریح) |
| خروجی واقعی Build/Test | ✅ | بخش ۳ همین سند |
| هیچ رازی در کد نیست | ✅ | اسکن واژگان حساس روی مخزن: صفر مورد؛ رشته اتصال فقط از Environment/User Secrets |
| کد ورود رمز عبور | ✅ کد + مسیرها | `/Account/Login`, `/Account/Register` با ترجمه فارسی خطاها و پیام یکنواخت |

## ۲) فایل‌های اصلی تغییر‌یافته

**ساخت (New):**
- `SadGallery.sln`، `global.json`، `Directory.Build.props`، `Directory.Packages.props`
- `src/SadGallery.Domain/` — `Money`, `Purity`, `Weight`, `CurrencyUnit`, `RateQuality`, `Measurements`
- `src/SadGallery.Application/` — `IClock`, `IIdentitySeeder`, `PersianText`, `DependencyInjection`
- `src/SadGallery.Infrastructure/` — `ApplicationUser`, `RoleNames`, `IdentitySeeder`, `SadGalleryDbContext`, `SadGalleryDbContextFactory`, Migrations، `SystemClock`
- `src/SadGallery.Web/` — `Program.cs`، Controllers (Home/Error/Account/Member)، Areas (Admin/Operator)، Views فارسی RTL، Security، Middleware، HealthChecks، wwwroot (Bootstrap RTL 5.3.8 + Vazirmatn 33.0.3)
- `tests/SadGallery.Tests.Unit` (۳۴ تست) و `tests/SadGallery.Tests.Integration` (۱۷ تست)
- `database/scripts/InitialIdentity.sql`، `scripts/ef.sh`
- `docs/DECISIONS/ADR-0010-...`، `docs/BUGS.md` (بازنویسی کامل با BUG-001..003)، همین گزارش

**تغییر (Modified):** `scripts/dev-setup.sh` (نصب خودکار `dotnet-ef`)، `scripts/test.sh` (پشته جدید)، `AGENTS.md`، و مستندات: `PROJECT_STATUS.md`، `CHANGELOG.md`، `ROADMAP.md`، `ARCHITECTURE.md`، `SECURITY.md`، `TESTING.md`، `DATA_MODEL.md`.

## ۳) تست‌های اجراشده و خروجی واقعی

```text
$ dotnet build SadGallery.sln -c Debug --nologo -m:1 -warnaserror
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/SadGallery.Tests.Unit          → total 34 | succeeded 34 | failed 0 | skipped 0
$ dotnet test tests/SadGallery.Tests.Integration   → total 17 | succeeded 16 | failed 0 | skipped 1
                                                     (Skip: RequiresSqlServerFact — نبود SADGALLERY_TEST_SQL)

$ dotnet list package --vulnerable --include-transitive  → هیچ بسته آسیب‌پذیری (۶ پروژه: «no vulnerable packages»)
$ dotnet list package --deprecated                       → هیچ بسته منسوخ (پس از مهاجرت xunit v2 → v3)
```

**اجرای واقعی برنامه روی Kestrel (پورت ۵۰۸۰) — خروجی curl:**

| مسیر | نتیجه |
| --- | --- |
| `/` | **200** · ۵۵۱۲ بایت · `<h1>ویترین آنلاین طلا و جواهر گالری صاد</h1>` · `dir="rtl"` · **صفر** Entity عددی |
| `/no-such-page` | **404** · `<h1>صفحه یافت نشد</h1>` · بدون StackTrace |
| `/health` | **200** · `Healthy` |
| `/health/ready` | **503** · `Unhealthy` (بدون DB — رفتار درست، بدون افشای جزئیات) |
| `/Admin/Dashboard`، `/Operator/Dashboard`، `/Member` | **302** ← `/Account/Login?ReturnUrl=…` |
| `GET /Account/Login` | **200** |
| `POST /Account/Login` بدون توکن ضدجعل | **400** (رد شد) |
| `/css/site.css`، Bootstrap RTL، فونت Vazirmatn | **200** (self-host، بدون CDN) |

سرآیندهای پاسخ (curl بدون فیلتر):
`X-Content-Type-Options: nosniff` · `X-Frame-Options: DENY` · `Referrer-Policy: strict-origin-when-cross-origin` · `Cross-Origin-Opener-Policy: same-origin` · `Permissions-Policy: geolocation=(), …` — و **هیچ سرآیند `Server` ارسال نمی‌شود** (`Program.cs` خط ۲۰).

**مهاجرت:** `20261005172327_InitialIdentity` — ۷ جدول `AspNet*` + `__EFMigrationsHistory`، ۸ ایندکس (از جمله `IX_AspNetUsers_IsActive`)؛ اسکریپت ایدِمپوتنت `database/scripts/InitialIdentity.sql`؛ `Down()` شامل DropTableها. تست `IdentitySchemaTests` روی دیتابیس واقعی: ساخت دیتابیس یکتا → `Migrate()` → اجرای **دو بار** Seed → بازبینی ۳ نقش → حذف دیتابیس.

## ۴) باگ‌های کشف و برطرف‌شده در این فاز

| باگ | علت ریشه‌ای | برطرف‌سازی |
| --- | --- | --- |
| BUG-001 | رشته `--` داخل کامنت XML در `Directory.Packages.props` ⇒ شکست بارگذاری Solution | بازنویسی کامنت + اعتبارسنجی XML همه فایل‌های پروژه |
| BUG-002 | صفحه خطا View پیدا نمی‌کرد ⇒ درخواست ناموجود ۵۰۰ می‌داد | `return View("Error", ErrorViewModel.For(...))` |
| BUG-003 | رمزگذار پیش‌فرض، متن فارسی را Entity عددی (`&#x635;`) می‌کرد | `WebEncoderOptions` با `UnicodeRanges.All` در `Program.cs` |

جزئیات کامل، بازتولید و تست رگرسیون هر باگ: `docs/BUGS.md`.

## ۵) آنچه در سندباکس اجرا نشد (صادقانه) و مسدودکننده‌ها

| مورد | چرا اجرا نشد | چگونه تأیید می‌شود |
| --- | --- | --- |
| مهاجرت روی دیتابیس واقعی + Seed نقش‌ها | سندباکس SQL Server/Docker ندارد | تست آماده و Skip می‌شود؛ با `SADGALLERY_TEST_SQL` اجرا می‌شود |
| ورود/ثبت‌نام واقعی با رمز عبور، قفل حساب پس از ۵ تلاش | وابسته به دیتابیس | مسیرها و پیکربندی آزمون‌شده (۴۰۰/۲۰۰/۳۰۲)؛ آزمون سرتاسری با DB |
| انتشار فاز روی مخزن رسمی (push) | در نشست جاری، توکن نزد ایجنت نیست؛ هیچ اعتبارنامه‌ای ذخیره نشده | مالک push می‌کند یا توکن را می‌دهد (فقط گذرا، بدون ذخیره) |

**مسدودکننده فاز ۲ (خارج از فاز ۱):** `Q-API-1` — نمونه JSON واقعی از API نرخ؛ تا رسیدن آن، فاز ۲ (طلب و اتصال) شروع نمی‌شود.

## ۶) ریسک امنیتی

- **اجراشده:** CSRF سراسری (تست: ۴۰۰ بدون توکن)، کنترل دسترسی سمت سرور (۳۰۲ به ورود)، محدودیت نرخ ورود/ثبت‌نام (۱۰/دقیقه/IP)، سرآیندهای امنیتی، خطای بدون جزئیات، کوکی HttpOnly، اسکن وابستگی (آسیب‌پذیری ۰ / منسوخ ۰)، Fail-Fast روی نبود رشته اتصال، صفر Secret در مخزن و تاریخچه.
- **باقی‌مانده (زمان‌بندی‌شده):** CSP کامل و Redaction سراسری لاگ (فاز ۸)، OTP و سیاست‌های نرخ اختصاصی آن (فاز ۶)، تست Payloadهای XSS روی محتوای کاربر (فاز ۵)، آزمون واقعی هش رمز/قفل حساب (نیازمند DB، همین فاز — اجرای مالک).

## ۷) Migration لازم است؟

**بله.** `InitialIdentity` — شامل ۷ جدول `AspNet*` + `__EFMigrationsHistory` و ۸ ایندکس.
- راه بازگشت: `Down()` (DropTableها) یا اسکریپت ایدِمپوتنت `database/scripts/InitialIdentity.sql` (شامل بلوک‌های بازگشت).
- اجرا روی ماشین مالک: `bash scripts/ef.sh database update` (رشته اتصال از `SADGALLERY_CONNECTION`).
- قبل از اجرا روی دیتابیس موجود: نسخه پشتیبان تهیه شود (دیتابیس پروژه هنوز خالی است؛ ریسک عملی صفر).

## ۸) مستندات به‌روزشده

`PROJECT_STATUS.md` (§۲.۱ — جدول شواهد واقعی)، `CHANGELOG.md` (بخش Unreleased فاز ۱)، `ROADMAP.md` (تیک معیارها + ⚠️ روی موارد وابسته به DB)، `BUGS.md`، `SECURITY.md` (وضعیت اجرا: ✅/🟡/⛔)، `TESTING.md` (پشته xunit v3/MTP و دستورها)، `ARCHITECTURE.md` (جدول نسخه‌ها)، `DATA_MODEL.md` (مهاجرت)، `AGENTS.md` (فاز جاری)، `DECISIONS/ADR-0010` (بازنگری‌شده).

## ۹) تأیید نهایی توسط مالک (روی ماشین دارای SQL Server)

```bash
dotnet build SadGallery.sln -warnaserror                                  # انتظار: 0 Warning, 0 Error
export SADGALLERY_TEST_SQL="Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
dotnet test                                                               # انتظار: Skip دیتابیس حذف و تست مهاجرت/Seed سبز
export SADGALLERY_CONNECTION="Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True"
bash scripts/ef.sh database update                                        # اعمال مهاجرت
dotnet run --project src/SadGallery.Web -- --seed                         # ساخت سه نقش (اجرای دوباره = بدون تکرار)
dotnet run --project src/SadGallery.Web                                   # بازدید: / , /health , /Account/Login
```
خروجی واقعی را برای ثبت در `PROJECT_STATUS.md` بفرستید.

## ۱۰) قدم بعدی دقیق

۱) رفع مسدودکننده فاز ۲: قرار دادن نمونه پاسخ واقعی API نرخ در `docs/samples/provider-response.json` (`Q-API-1`).
۲) پس از تأیید مالک: آغاز فاز ۲ — قرارداد `IRateProvider` + DTO + نگاشت تست‌شده، Cache/تاریخچه، Job دوره‌ای و سیاست «نرخ کهنه».
