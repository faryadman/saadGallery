# استراتژی تست — SadGallery

> قاعده: «تست سبز» فقط وقتی گفته می‌شود که دستور اجرا شده و خروجی واقعی دیده شده باشد. تست حذف یا `Skip` شده باید **دلیل و تاریخ** در همین فایل داشته باشد.

---

## ۱. هرم تست پروژه

| سطح | پروژه | چه چیزی آزمون می‌شود | سرعت هدف |
| --- | --- | --- | --- |
| واحد | `SadGallery.Tests.Unit` | محاسبات مالی، حباب، عیار/وزن، ماشین وضعیت تیکت، Policy، نگاشت DTO/JSON | < ۵ ثانیه کل |
| یکپارچه | `SadGallery.Tests.Integration` | Identity، دسترسی نقش‌ها، Migration، تیکت، آپلود، Provider با سرور جعلی، Cache/Stale | < ۳ دقیقه |
| امنیتی | در Integration + سناریوهای مشخص | IDOR، XSS، CSRF، SQLi، سوءاستفاده OTP، Rate Limit، افشای Secret | — |
| دستی/دود (Smoke) | چک‌لیست در فازها | RTL، موبایل، PWA نصب، دسترس‌پذیری | — |

از E2E مرورگر (Selenium/Playwright) در نسخه اول **استفاده نمی‌کنیم** (هزینه نگهداری بالا برای تیم یک‌نفره)؛ در عوض تست‌های یکپارچه سطح HTTP با `WebApplicationFactory` + آزمون دستی مستند. اگر PWA رفتار پیچیده‌ای پیدا کرد، این تصمیم در ADR بازبینی می‌شود.

## ۲. آماده‌سازی محیط (آزمون‌شده در 2026-10-05)

```bash
bash scripts/dev-setup.sh     # در سندباکس: نصب SDK 10.0.401 در /opt/dotnet (غیرماندگار) + restore/build
```

| محیط | Build | Unit Test | Integration (SQL) |
| --- | --- | --- | --- |
| سندباکس ایجنت | ✅ آزمون‌شده (SDK 10.0.401، NuGet در دسترس) | ✅ | ❌ (بدون SQL Server ⇒ Skip صریح) |
| ماشین مالک / CI | ✅ | ✅ | ✅ (`SADGALLERY_TEST_SQL`) |

## ۳. دستورهای استاندارد (آزمون‌شده در 2026-10-05)

```bash
bash scripts/test.sh          # همه: واحد + یکپارچه (تست‌های DB در نبود متغیر محیطی Skip می‌شوند)

# اجرای فقط یک پروژه
dotnet test tests/SadGallery.Tests.Unit/SadGallery.Tests.Unit.csproj -c Debug
dotnet test tests/SadGallery.Tests.Integration/SadGallery.Tests.Integration.csproj -c Debug

# اجرای تست‌های نیازمند SQL Server
export SADGALLERY_TEST_SQL="Server=localhost;Database=SadGallery_Test;Trusted_Connection=True;TrustServerCertificate=True"
dotnet test
```

**ابزار تست — وضعیت واقعی:**
| موضوع | انتخاب | یادداشت |
| --- | --- | --- |
| فریم‌ورک | **xunit.v3 4.0.1** | xunit v2 توسط خود پروژه xUnit «Legacy/منسوخ» اعلام شده است (کشف‌شده با بررسی بسته‌های منسوخ) ⇒ طبق اصل پروژه استفاده نمی‌شود |
| اجراکننده | **Microsoft.Testing.Platform (MTP)** | از .NET 10 مسیر VSTest برای پروژه‌های MTP حذف شده است |
| opt-in تجربه جدید | فایل `global.json` → بخش `"test": { "runner": "Microsoft.Testing.Platform" }` | بدون این تنظیم، `dotnet test` با خطای «VSTest target is no longer supported» شکست می‌خورد |
| بسته‌های حذف‌شده | `Microsoft.NET.Test.Sdk`، `coverlet.collector` | با MTP لازم نیستند؛ ابزار پوشش کد در فاز ۸ ارزیابی می‌شود (Quality Gate پوشش، تا آن زمان هدف است نه ادعا) |
| assertion | `Assert`های خود xUnit | **بدون FluentAssertions** (مجوز تجاری از v8) — ADR-0010 |
| اسکایل شرطی | `RequiresSqlServerFactAttribute` (تست شرطی داخلی) | در نبود `SADGALLERY_TEST_SQL` پیام Skip روشن می‌دهد |

**نکته پایداری:** در فایل `tests/SadGallery.Tests.Integration/AssemblyInfo.cs` موازی‌سازی بین کلاس‌های تست
با `[assembly: Parallelization(Mode = ParallelMode.None)]` غیرفعال شده است؛ چون هر کلاس یک میزبان واقعی
ASP.NET Core می‌سازد و اجرای هم‌زمان آن‌ها روی منابع مشترک رقابت می‌کند (BUG-007). این کار هیچ تستی را
حذف نمی‌کند و فقط زمان اجرا را چند ثانیه بیشتر می‌کند.

**نتایج آخرین اجرا (2026-10-06، سندباکس بدون SQL Server):**
```
Unit:        total 34 | succeeded 34 | failed 0 | skipped 0
Integration: total 17 | succeeded 16 | failed 0 | skipped 1  ← Skip: RequiresSqlServerFact (نیازمند SQL Server)
```

## ۴. اجرای تست وابسته به دیتابیس (روی ماشین مالک/CI)

```bash
export SADGALLERY_TEST_SQL="Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
dotnet test          # تست مهاجرت/Seed هم اجرا می‌شود
```
تست `IdentitySchemaTests` یک دیتابیس یکتا با نام تصادفی می‌سازد، مهاجرت را اعمال می‌کند،
Seed را دو بار اجرا می‌کند (اثبات ایدِمپوتنت) و در پایان دیتابیس را Drop می‌کند.

> پوشش کد (coverage): ابزار آن با مهاجرت به Microsoft.Testing.Platform حذف شد و در فاز ۸ ارزیابی می‌شود.
> «Quality Gate پوشش» در بخش ۹ تا آن زمان **هدف** است، نه ادعای محقق‌شده.

## ۵. تست‌های یکپارچه و دیتابیس

- دیتابیس تست اختصاصی: `SadGallery_Test` (هرگز Production).
- هر کلاس تست یک **schema/دیتابیس تازه** می‌خواهد؛ روش ترجیحی: ساخت دیتابیس یکتا با نام تصادفی و `Migrate()` در setup، و Drop در teardown. (روش `EnsureCreated` ممنوع است — رفتار Migration را آزمون نمی‌کند.)
- تست‌های نیازمند DB با Attribute داخلی مشخص می‌شوند:
  ```csharp
  [RequiresSqlServerFact]   // در نبود SADGALLERY_TEST_SQL پیام روشن Skip می‌دهد (نه سبز کاذب)
  public async Task Migrations_ApplyToEmptyDatabase_AndRoleSeed_IsIdempotent() { ... }
  ```
- هر تست، `TestContext.Current.CancellationToken` را به فراخوانی‌های async (HTTP/SQL) می‌دهد
  (الزام تحلیلگر xUnit1051 در xunit v3).
- Migrationها با تست صریح آزمون می‌شوند: `Migrate()` روی دیتابیس خالی + بررسی وجود جداول/Indexهای کلیدی + `Down` تا حد ممکن در محیط تست.
- در سندباکس ایجنت SQL Server وجود ندارد ⇒ این تست‌ها Skip می‌شوند و **در گزارش فاز باید صریحاً «اجرا نشده در سندباکس» قید شود**.

## ۶. تست Provider نرخ (بدون شبکه واقعی)

- یک `StubHttpMessageHandler` (یا `HttpMessageHandler` سفارشی) پاسخ Fixture را برمی‌گرداند؛ آزمون‌ها هرگز به اینترنت واقعی وابسته نیستند.
- Fixtureها در `tests/SadGallery.Tests.Integration/Fixtures/RateProviders/<provider>/` نگهداری می‌شوند و **بدون Secret** هستند.
- موارد الزامی هر Provider: پاسخ موفق، واحد تومان، تاریخ Unix، عدد به‌صورت رشته فارسی/انگلیسی، کلید ناشناس، فیلد گم‌شده، JSON ناقص، خطای 500، Timeout، پاسخ 429.
- **SSRF:** تلاش برای `http://localhost`, `http://127.0.0.1`, `http://169.254.169.254`, دامنه غیرمجاز ⇒ باید رد شود (تست واقعی).
- «نرخ ساختگی» در تست: فقط در Fixture برای آزمون منطق؛ **هرگز در داده Production یا Fallback**.

## ۷. تست‌های امنیتی الزامی (هرکدام یک تست اجراشدنی)

| تست | سناریو | معیار قبولی |
| --- | --- | --- |
| IDOR تیکت | کاربر A به ID تیکت کاربر B (GET و POST پاسخ) | 404 یا 403 (بدون افشای وجود) |
| دسترسی پنل | `Customer` به `/Admin/*` و `/Operator/*` | 403 |
| ماتریس نقش | همه Endpointهای پنل × ۳ نقش | جدول انتظار/واقعیت ۱۰۰٪ منطبق |
| SQLi | ورودی `' OR 1=1--` در ورود/جست‌وجو/تیکت | بدون خطای 500، بدون دورزدن احراز، داده سالم |
| XSS | `<script>`، `"><img onerror>`، `javascript:` در تیکت/محصول/نام | خروجی Encode‌شده، اسکریپت اجرا نشود |
| CSRF | POST بدون توکن Anti-forgery از Origin دیگر | ۴۰۰/۴۰۳ |
| OTP Brute | ۱۰ تلاش با کد نادرست | قفل/رد + `SecurityEvent` |
| OTP Replay | استفاده دوباره از کد مصرف‌شده | رد |
| OTP Flood | درخواست پشت‌سرهم ارسال کد | محدودیت نرخ اعمال + پیام مناسب |
| آپلود مسموم | EXE با نام `.jpg`، SVG با اسکریپت، فایل ۱۰۰MB، نام `../evil.php` | همه رد شوند، هیچ فایلی ذخیره نشود |
| افشای Secret | پیمایش لاگ/پاسخ خطا/HTML پنل | صفر مورد کلید/رمز/OTP/Connection String |
| حدس رمز | ۲۰ تلاش روی حساب | قفل حساب + پیام یکنواخت |

## ۸. تست محاسبات (دقیق‌ترین بخش پروژه)

**حباب:**
- ارزش ذاتی = `وزن × عیار/0.750 × نرخ مرجع`؟ **نه** — فرمول نهایی باید با سند مرجع و نسخه‌بندی در `Domain` تعریف شود (`FormulaVersion`). تست‌ها بر اساس همان نسخه نوشته می‌شوند، نه بر اساس برداشت شخصی.
- موارد مرزی: نرخ مرجع صفر/منفی ⇒ نتیجه `IsTrusted=false` و بدون حباب؛ نرخ کهنه ⇒ پرچم؛ حباب منفی ⇒ نمایش با علامت و توضیح، نه صفر شدن؛ دقت اعشاری: گردکردن فقط در لبه نمایش، نه در محاسبات میانی.
- آزمون «هیچ‌گاه جمع خطا انبار نشود»: محاسبه روی ۱۰۰۰ رکورد نمونه و بررسی اینکه خطای انباشته صفر است (استفاده از decimal).

**تبدیل واحد/عیار:**
- `طلای 18 عیار = 0.750 خالص` — به‌عنوان ثابت مستند در `Domain` و آزمون‌شده.
- تست گردکردن وزن (گرم ↔ مثقال = ۴.۶۰۸۳ گرم) با تلورانس صریح.
- تست اینکه ریال/تومان هرگز بی‌تبدیل یکی گرفته نمی‌شوند (تست منفی).

**ماشین وضعیت تیکت:** ماتریس همه گذارها × نقش‌ها؛ گذار غیرمجاز ⇒ رد.

## ۹. معیارهای کیفیت (Quality Gates)
| سنجه | حد | نحوه بررسی |
| --- | --- | --- |
| Build | صفر خطا، صفر هشدار | `dotnet build -warnaserror` |
| تست واحد | ۱۰۰٪ سبز | `dotnet test` |
| پوشش منطق مالی (`Domain`) | ≥ ۹۰٪ | Coverage Report |
| پوشش کل | ≥ ۶۰٪ (هدف تدریجی) | Coverage Report |
| تست شکسته/Skip بدون دلیل | صفر | بازبینی فایل تست + این سند |
| آسیب‌پذیری بسته | صفر High/Critical | `dotnet list package --vulnerable --include-transitive` |

## ۱۰. باگ‌ها
- هر باگ: ابتدا **تست بازتولیدکننده** (قرمز) ⇒ رفع ⇒ تست سبز ⇒ ثبت در `docs/BUGS.md` با ریشه‌یابی.
- اگر رفع سریع ممکن نیست، آزمون به‌صورت `Skip` با شماره باگ **مجاز نیست بماند**؛ به‌جایش رفع موقت مستند + باگ باز می‌ماند.

## ۱۱. داده تست
- هرگز داده واقعی مشتری در تست/Chat/Fixture. نمونه‌ها ساختگی با برچسب واضح.
- شماره‌های موبایل تستی از بازه‌های غیرواقعی (`09120000000` سبک نمونه) یا Docomo/test-ranges؛ نام‌ها تخیلی.
- پشتیبان Production برای تست فقط پس از ناشناس‌سازی مستند.

## ۱۲. گزاره صداقت
در هر گزارش فاز بنویس:
```
دستورهای اجراشده: <دقیق>
نتیجه واقعی: <خروجی خلاصه، تعداد تست، مدت>
اجرا نشده: <به‌دلیل>  ← مثال: تست‌های RequiresSqlServer در سندباکس (بدون SQL Server)
```
