# استقرار، پیکربندی و بازیابی — SadGallery

> وضعیت فاز ۰: **هنوز هیچ استقراری انجام نشده است.** این سند طرح اجرایی است و در فازهای بعد با مقادیر واقعی تکمیل می‌شود.

---

## ۱. محیط‌ها

| محیط | ماشین | دیتابیس | Secret | هدف |
| --- | --- | --- | --- | --- |
| Development | ماشین مالک (Windows/Linux) | SQL Server Express / LocalDB | User Secrets | توسعه و تست سریع |
| Staging (پیشنهاد) | سرور مشابه تولید | دیتابیس کپی ناشناس‌شده | Secret Store مجزا | آزمون Migration/Backup/چرخش کلید |
| Production | تعیین‌نشده (`Q-ENV-1`) | SQL Server با Full Recovery | Secret Store + گاوصندوق آفلاین Passphrase | سرویس واقعی |
| Sandbox ایجنت | Debian 13 بدون SQL Server | — | — | فقط تحلیل و کد؛ **تست یکپارچه اینجا اجرا نمی‌شود** |

## ۲. پیش‌نیازهای Production (چک‌لیست)
- .NET 10 Runtime (ASP.NET Core Runtime) آخرین Patch امن.
- SQL Server (نسخه هدف بر اساس `Q-ENV-1`)، دیتابیس با Collation فارسی‌پسند (پیشنهاد: `Persian_100_CI_AS_SC` — باید روی Staging آزمایش شود) و رمزنگاری داده در حالت سکون (TDE) در صورت لزوم سیاست.
- Reverse proxy با TLS 1.2/1.3 معتبر (Nginx/IIS ARR/YARP) + HSTS.
- پوشه ذخیره فایل خارج از پوشه برنامه (بدون مجوز اجرا، بدون لیست‌کردن).
- سیستم پشتیبان‌گیری با مقصد جدا (ترجیحاً رمزنگاری‌شده و آفلاین).
- پایش: دسترسی به Health Check + جمع‌آوری لاگ.

## ۳. پیکربندی (نمای کلیدها — بدون هیچ مقدار محرمانه)

```jsonc
// appsettings.json — بدون Secret در مخزن
{
  "ConnectionStrings": { "SadGallery": "HOST_FROM_ENV" }, // مقدار واقعی فقط در Secret Store/ENV
  "RateOptions": {
    "FetchIntervalSeconds": 60,
    "StaleThresholdMinutes": 15,
    "MaxResponseBytes": 2097152,
    "HttpTimeoutSeconds": 10,
    "AllowedProviderDomains": [ "example-feed.invalid" ],
    "AnomalyChangeThresholdPercent": 50,
    "AnomalyPolicy": "ManualReview"     // ManualReview | AutoAccept
  },
  "SecurityOptions": {
    "Pbkdf2Iterations": 600000,          // اندازه‌گیری و ثبت در محیط واقعی؛ قابل ارتقا
    "Otp": { "Length": 6, "TtlSeconds": 120, "MaxAttempts": 5, "ResendCooldownSeconds": 60, "MaxPerDayPerIdentifier": 10 },
    "Lockout": { "MaxFailedAttempts": 5, "LockoutMinutes": 15 },
    "RateLimits": { "LoginPerMinute": 10, "OtpPerMinute": 3, "TicketPerHour": 5 }
  },
  "StorageOptions": {
    "UploadsRoot": "/var/sadgallery/uploads",   // بیرون از wwwroot
    "MaxUploadBytes": 5242880,
    "AllowedImageFormats": [ "jpeg", "png", "webp" ],
    "MaxImagesPerProduct": 8
  },
  "Serilog": { "MinimumLevel": { "Default": "Information" } }
}
```

**قواعد:**
- `appsettings.json` **هیچ** راز، کلید یا رشته اتصالی ندارد. فقط جای‌نگهدار و پیش‌فرض‌های بی‌خطر.
- در Dev: `dotnet user-secrets`؛ در Prod: Environment Variables یا Secret Store پلتفرم.
- `ValidateOnStart` برای همه Options ⇒ برنامه با تنظیمات نامعتبر بالا نمی‌آید (Fail Fast).

> ⚠️ **دربارهٔ مقدار `HOST_FROM_ENV` (تله‌ای که واقعاً رخ داد — BUG-009):**
> این مقدار **عمداً** یک رشتهٔ نامعتبر است تا برنامه بدون تنظیم واقعی بالا نیاید. اگر آن را با متغیر محیطی بازنویسی نکنید،
> پیام فارسی و راهنما می‌گیرید (نه خطای مبهم). **تقدم پیکربندی در .NET:** `appsettings.json` →
> `appsettings.{Environment}.json` → User Secrets → **متغیرهای محیطی** → آرگومان خط فرمان.
> پس کافی است متغیر محیطی را تنظیم کنید و **لازم نیست** فایل را تغییر دهید:
> ```powershell
> $env:ConnectionStrings__SadGallery = "Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True"
> ```

> 🪟 **روی ویندوز:** `StorageOptions.UploadsRoot` در نمونه به‌صورت مسیر لینوکسی (`/var/sadgallery/uploads`) آمده است.
> اگر روی ویندوز استقرار می‌دهید، مقدار را به مسیری مثل `C:\\SadGallery\\uploads` تغییر دهید
> (پیکربندی این مقدار در فاز ۴ — گالری — نهایی و آزمون می‌شود).
- کلیدهای اضافه/غایب در Prod باعث خطای صریح و لاگ بدون افشای مقدار می‌شوند.

## ۴. راه‌اندازی گام‌به‌گام (Production) — Runbook

```bash
# 1) انتشار
dotnet publish src/SadGallery.Web -c Release -o /srv/sadgallery/app

# 2) پیش از هر چیز: پشتیبان کامل
#    (دستور واقعی بر اساس محیط در Q-ENV-1 مشخص می‌شود)

# 3) مهاجرت با اسکریپت بازبینی‌شده (نه Update خودکار در Prod در اولین بار)
#    اسکریپت idempotent را در Staging آزموده باشید
dotnet ef migrations script <from> <to> --idempotent -o migrate.sql
# اجرای migrate.sql روی دیتابیس، سپس:

# 4) تنظیم Secretها (نام‌ها نمونه؛ مقادیر فقط روی سرور)
export ConnectionStrings__SadGallery="***"
export SadGallery__Passphrase="***"        # از Secret Store خوانده شود، نه تایپ در تاریخچه Shell
export SMS__ApiKey="***"

# 5) اجرا
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:5000 dotnet /srv/sadgallery/app/SadGallery.Web.dll

# 6) بررسی سلامت (پس از استقرار، الزامی)
curl -fsS http://127.0.0.1:5000/health/ready
```

## ۴.۵ راه‌اندازی اولین‌بار دیتابیس — «دیتابیس ساخته نشده، کجا مهاجرت بزنم؟»

**پاسخ کوتاه: دستی دیتابیس نسازید. دستور `database update` خودش دیتابیس را می‌سازد.**
مهاجرت‌های EF Core هنگام اجرا، اگر دیتابیس وجود نداشته باشد `CREATE DATABASE` را صادر می‌کنند و بعد جدول‌ها را می‌سازند.

### الف) کجا اجرا کنم؟
روی **همان ماشینی که SQL Server دارد**، در **ریشهٔ مخزن** (جایی که `SadGallery.sln` هست). فقط یک‌بار لازم است.

### ب) ویندوز — PowerShell (رایج‌ترین حالت)

```powershell
cd C:\path\to\saadGallery

# ۱) ابزار EF را یک‌بار نصب کنید (نسخه ۱۰)
dotnet tool install --global dotnet-ef --version 10.*

# ۲) رشته اتصال را فقط در همین پنجرهٔ شل تعیین کنید (نه در فایل مخزن)
$env:SADGALLERY_CONNECTION = "Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True"

# ۳) اعمال مهاجرت‌ها (دیتابیس در همین لحظه ساخته می‌شود)
dotnet ef database update --project src/SadGallery.Infrastructure --startup-project src/SadGallery.Web
```

- اگر **SQL Server Express** دارید: `Server=localhost\SQLEXPRESS;...`
- اگر **LocalDB** دارید (همراه Visual Studio نصب می‌شود): می‌توانید مرحلهٔ ۲ را کامل حذف کنید؛ کارخانهٔ زمان طراحی خودش به `(localdb)\MSSQLLocalDB` برمی‌گردد. یا صریح بنویسید: `Server=(localdb)\MSSQLLocalDB;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True`
- اگر از **Git Bash** روی ویندوز استفاده می‌کنید:
  ```bash
  SADGALLERY_CONNECTION="Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True" bash scripts/ef.sh database update
  ```

### ج) لینوکس / سرور

```bash
export SADGALLERY_CONNECTION="Server=localhost;Database=SadGallery;User Id=sa;Password=<رمز-از-Secret-Store>;TrustServerCertificate=True"
bash scripts/ef.sh database update
```

### د) ⚠️ تله‌ای که باید بدانید: دو نام متغیر برای یک رشته اتصال
| مصرف‌کننده | متغیر | چرا |
| --- | --- | --- |
| ابزار `dotnet ef` (مهاجرت) | `SADGALLERY_CONNECTION` | کارخانهٔ زمان طراحی `SadGalleryDbContextFactory` همین را می‌خواند |
| خودِ برنامه (`dotnet run`) | `ConnectionStrings__SadGallery` | این نگارش، معادل `ConnectionStrings:SadGallery` در `appsettings.json` است |

برای اجرای برنامه، جدا از دستور مهاجرت:
```powershell
$env:ConnectionStrings__SadGallery = "Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project src/SadGallery.Web -- --seed    # ساخت سه نقش (ایدِمپوتنت، بی‌خطر برای تکرار)
dotnet run --project src/SadGallery.Web             # اجرای واقعی
```
`scripts/ef.sh` اگر `SADGALLERY_CONNECTION` نبود، مقدار `ConnectionStrings__SadGallery` را به‌کار می‌برد؛ اما **برنامه** این پل را ندارد — پس برای اجرای اپ همان نام `ConnectionStrings__SadGallery` لازم است.

### ه) بررسی موفقیت (اجباری، بدون حدس)
```bash
# ۱) جدول‌ها باید ۸ مورد باشند: ۷ جدول AspNet* + __EFMigrationsHistory
sqlcmd -S localhost -d SadGallery -Q "SELECT name FROM sys.tables ORDER BY name"

# ۲) مهاجرت ثبت‌شده
sqlcmd -S localhost -d SadGallery -Q "SELECT MigrationId FROM __EFMigrationsHistory"
#    انتظار: 20261005172327_InitialIdentity

# ۳) نقش‌ها پس از --seed
sqlcmd -S localhost -d SadGallery -Q "SELECT Name FROM AspNetRoles"
#    انتظار: Admin, Customer, Operator

# ۴) آمادگی سرویس (پس از اجرای برنامه)
curl http://localhost:5000/health/ready     # انتظار: 200 Healthy
```

### و) مسیر جایگزین: اجرای مستقیم اسکریپت SQL
اگر نمی‌خواهید از ابزار EF استفاده کنید، `database/scripts/InitialIdentity.sql` (ایدِمپوتنت، ۱۶ محافظ) آماده است.
**اما توجه: این اسکریپت دیتابیس را نمی‌سازد** — اول دیتابیس را بسازید:
```bash
sqlcmd -S localhost -Q "IF DB_ID('SadGallery') IS NULL CREATE DATABASE SadGallery"
sqlcmd -S localhost -d SadGallery -i database/scripts/InitialIdentity.sql
```
(در Production همین مسیر با بازبینی و پشتیبان‌گیری انجام می‌شود — بخش ۴.)

### ز) اگر SQL Server ندارید
یکی را نصب کنید (فاز ۱ فقط به یکی از این‌ها نیاز دارد):
| گزینه | مناسب برای | یادداشت |
| --- | --- | --- |
| SQL Server Express | توسعه + پیش‌تولید | رایگان، همان موتور واقعی؛ برای Production هم کافی است اگر محدودیت‌هایش پذیرفتنی باشد |
| LocalDB | فقط توسعهٔ محلی ویندوز | همراه Visual Studio می‌آید؛ برای Production به‌کار نمی‌رود |
| Docker (mssql) | توسعه روی لینوکس/مک | در سندباکس ایجنت موجود نیست؛ روی ماشین خودتان ممکن است |

### ح) چیزی که در سندباکس ایجنت اجرا نشد
سندباکس SQL Server/Docker ندارد، بنابراین **اجرای واقعی مهاجرت روی دیتابیس فقط روی ماشین مالک/CI انجام می‌شود**
(دستورهای بالا). تست خودکار `IdentitySchemaTests` همین مسیر را روی دیتابیس موقت می‌آزماید و با تنظیم
`SADGALLERY_TEST_SQL` اجرا می‌شود (docs/TESTING.md §۴).

## ۴.۶ ساخت کاربران اولیه (Seed) — «چطور اولین مدیر را بسازم؟»

**خلاصه:** کاربران اولیه از بخش تنظیمات `SeedUsers` خوانده می‌شوند و رمزشان **فقط** از متغیر محیطی
`SADGALLERY_SEED_PASSWORD` (یا `SeedUsers:n:Password`) می‌آید. هیچ رمز پیش‌فرضی در مخزن نیست (ADR-0011).

### الف) ویندوز — PowerShell
```powershell
# ۱) رمز دلخواه خودتان را فقط در همین پنجره شل تعیین کنید (در فایل مخزن نگذارید)
$env:SADGALLERY_SEED_PASSWORD = "<یک-رمز-قوی-حداقل-۸-نویسه-با-رقم>"

# ۲) اجرای Seed (نقش‌ها + کاربران تعریف‌شده در appsettings.Development.json)
dotnet run --project src/SadGallery.Web -- --seed
```
خروجی موفق چنین است («ساخته شد» در اجرای اول، «از قبل بود» در اجرای بعدی):
```
نقش‌های پایه بررسی/ایجاد شدند: Customer, Operator, Admin
  • admin@sadgallery.local — ساخته شد؛ نقش Admin اضافه شد
  • operator@sadgallery.local — ساخته شد؛ نقش Operator اضافه شد
  • customer@sadgallery.local — ساخته شد؛ نقش Customer اضافه شد
پایان Seed. (رمزهای عبور هرگز چاپ یا لاگ نمی‌شوند)
```

### ب) لینوکس / Git Bash
```bash
export SADGALLERY_SEED_PASSWORD="<رمز قوی>"
dotnet run --project src/SadGallery.Web -- --seed
```

### ج) اگر رزهای خودتان می‌خواهید (Production/Staging)
`SeedUsers` را در تنظیمات محیط خود تعریف کنید (مثلاً با ENV) و رمز را بدهید:
```bash
export SeedUsers__0__UserName="owner@example.com"
export SeedUsers__0__Email="owner@example.com"
export SeedUsers__0__DisplayName="مالک"
export SeedUsers__0__Role="Admin"
export SeedUsers__0__Password="<رمز-قوی-از-Secret-Store>"
export SeedUsers__1__UserName="operator@example.com"
export SeedUsers__1__Role="Operator"
export SADGALLERY_SEED_PASSWORD="<رمز دوم>"          # برای ورودی‌هایی که رمز اختصاصی ندارند
dotnet run --project src/SadGallery.Web -- --seed
```
> در محیط غیر Development هشدار صریح چاپ می‌شود تا اجرای سهوی مشخص باشد.

### د) قواعد امنیتی این مسیر (ADR-0011)
| قاعده | چرا |
| --- | --- |
| تعریف ناقص ⇒ شکست **پیش از هر تغییر** در دیتابیس (کد خروج `۲`) | جلوگیری از نیمه‌ساخته‌شدن داده |
| کاربر موجود بازنویسی نمی‌شود و **رمز تغییر نمی‌کند** | ایدِمپوتنسی امن؛ اجرای دوباره بی‌خطر است |
| نقش جاافتاده اضافه می‌شود | می‌توان نقش را بعداً ارتقا داد |
| رمز هرگز چاپ/لاگ نمی‌شود | جلوگیری از ماندن راز در لاگ/تاریخچه Shell |
| `PasswordHash` فقط از `UserManager` (هش استاندارد Identity) | ممنوعیت رمزنگاری دست‌ساز |

### ه) تأیید
```bash
sqlcmd -S localhost -d SadGallery -Q "SELECT UserName, IsActive FROM AspNetUsers"
sqlcmd -S localhost -d SadGallery -Q "SELECT u.UserName, r.Name FROM AspNetUserRoles ur JOIN AspNetUsers u ON u.Id=ur.UserId JOIN AspNetRoles r ON r.Id=ur.RoleId"
```
سپس با همین کاربران در `/Account/Login` وارد شوید (شناسه = نام کاربری یا ایمیل).

### و) پاک‌کردن کاربران نمونه در پایان کار (اختیاری)
```sql
-- فقط اگر مطمئنید که داده وابسته‌ای ندارند
DELETE ur FROM AspNetUserRoles ur JOIN AspNetUsers u ON u.Id = ur.UserId WHERE u.UserName LIKE '%@sadgallery.local';
DELETE FROM AspNetUsers WHERE UserName LIKE '%@sadgallery.local';
```

## ۴.۷ عیب‌یابی راه‌اندازی اولیه (خطاهای واقعی دیده‌شده)

### خطا: `Format of the initialization string does not conform to specification starting at index 0`
**معنا:** مقدار رشته اتصال از نظر شکل نامعتبر است (این پیام از SqlClient می‌آید، نه از کد ما).

| علت رایج | نمونه | راه‌حل |
| --- | --- | --- |
| متن جای‌نگهدار به‌جای مقدار واقعی | `<connection-string>` یا **`HOST_FROM_ENV`** (مقدار پیش‌فرض `appsettings.json`) | رشته اتصال واقعی را در **متغیر محیطی** بگذارید (نیازی به تغییر فایل نیست) |
| کوتیشن اضافه (کپی از نمونهٔ JSON) | `'"Server=...;Database=...;'` یا `""Server=...""` | کوتیشن‌های داخلی را حذف کنید؛ فقط کوتیشن خود شل بماند |
| نبود `کلید=مقدار` | `HOST_FROM_ENV` | از الگوی زیر استفاده کنید |
| کلید Server یا Database جا افتاده | `Server=localhost;` | هر دو کلید لازم است |

**مقدار درست (اپلیکیشن):**
```powershell
$env:ConnectionStrings__SadGallery = "Server=(localdb)\MSSQLLocalDB;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True"
```
**مقدار درست (ابزار مهاجرت — نام متغیر متفاوت است!):**
```powershell
$env:SADGALLERY_CONNECTION = "Server=(localdb)\MSSQLLocalDB;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True"
```
> از نسخهٔ اخیر، برنامه **پیش از** اتصال، شکل رشته را بررسی می‌کند و پیام فارسی با راه‌حل می‌دهد
> (`ConnectionStringGuard` + `scripts/windows-setup.ps1`) — دیگر این خطای مبهم را نمی‌بینید.

### خطا: `LocalDB is not supported on this platform`
روی لینوکس رخ می‌دهد؛ یعنی رشته اتصال به LocalDB اشاره می‌کند. روی لینوکس از SQL Server واقعی/Docker استفاده کنید.

### خطا: `Login failed for user 'sa'` یا `Cannot open database "SadGallery"`
- اگر دیتابیس وجود ندارد: `dotnet ef database update` خودش می‌سازد.
- اگر رمز اشتباه است: مقدار `sa` را از Secret Store بدهید، نه از فایل.

### خطا: `A network-related or instance-specific error ... error: 40`
سرور پیدا نشد. بررسی کنید: سرویس SQL Server روشن است، نام نمونه درست است (`localhost\SQLEXPRESS` برای Express)،
و در SQL Server Configuration Manager پروتکل TCP/IP فعال است.

### خطای Seed: `رمز عبور تعیین نشده است`
متغیر `SADGALLERY_SEED_PASSWORD` را در همان پنجره تنظیم کنید (فقط نشستی، نه در فایل).

### بررسی سریع همه‌چیز
```powershell
.\scripts\windows-setup.ps1 -Run     # اعتبارسنجی + مهاجرت + Seed + اجرا، همه در یک مرحله
```

## ۵. مدیریت Secret و عبارت محرمانه

| راز | محل نگهداری | چرخش |
| --- | --- | --- |
| Connection String | Secret Store / ENV سرور | هنگام تغییر رمز DB |
| Passphrase (عبارت محرمانه) | Secret Store + نسخه فیزیکی رمزنگاری‌شده در گاوصندوق | فقط با فرایند چرخش کلید (`SECURITY.md` §4.4) |
| API Key منبع نرخ | رمزنگاری‌شده در DB (`PriceProvider.ApiKeyProtected`) | هر ۹۰ روز یا در صورت نشت |
| API Key پیامک | رمزنگاری‌شده در DB یا Secret Store | هر ۹۰ روز |
| رمز SQL | Secret Store | طبق سیاست |

**اگر Passphrase گم شود:** داده‌های رمزنگاری‌شده (کلیدهای API ذخیره‌شده، کد ملی) **قابل بازیابی نیستند**. رویه: (۱) بازیابی از گاوصندوق؛ (۲) در صورت نبود نسخه، سیستم را با Passphrase جدید بالا بیاورید (کلیدها بازنشسته)، کلیدهای API را از پنل تازه وارد کنید؛ (۳) پذیرش از‌دست‌رفتن `NationalId` (بی‌اهمیت‌ترین داده رمزنگاری‌شده). **پشتیبان دیتابیس به‌تنهایی کافی نیست** — این نکته باید در Runbook سرور نوشته شود.

## ۶. پشتیبان‌گیری و بازیابی

**سیاست پیشنهادی:**
| نوع | تناوب | نگهداری | محل |
| --- | --- | --- | --- |
| Full DB Backup | روزانه | ۳۰ روز | مخزن جدا + نسخه آفلاین هفتگی |
| Differential | هر ۶ ساعت | ۷ روز | مخزن جدا |
| Transaction Log | هر ۱۵ دقیقه (Full Recovery) | ۷ روز | مخزن جدا |
| فایل‌های آپلود | روزانه (Incremental) | ۳۰ روز | مخزن جدا |
| پیکربندی/Secret schema (بدون مقادیر) | در هر تغییر | نامحدود | Git |

**آزمون بازیابی (الزامی در فاز ۸، حداقل یک بار):**
1. بازگردانی آخرین Full+Differential+Log روی سرور/دیتابیس جداگانه.
2. اجرای `DBCC CHECKDB` و تأیید یکپارچگی.
3. مقایسه شمارش رکوردهای کلیدی (`MarketPrice`, `PriceHistory`, `SupportTicket`, `TicketMessage`, `AspNetUsers`) و بازبینی ۵ تصویر تصادفی.
4. راه‌اندازی نسخه Staging روی دیتابیس بازگردانی‌شده و آزمون ورود + صفحه نرخ.
5. ثبت زمان واقعی (RTO) و میزان داده ازدست‌رفته (RPO) در `PROJECT_STATUS.md`.
6. **تست بازگردانی گذرواژه/کلید:** رمزگشایی موفق یک رکورد رمزنگاری‌شده پس از بازیابی.

> تست بازیابی که اجرا نشده باشد، پشتیبان محسوب نمی‌شود؛ فقط امید است. (اصل پروژه)

## ۷. پایش و هشدار
| چه چیزی | آستانه پیشنهادی | اقدام |
| --- | --- | --- |
| آخرین دریافت موفق نرخ | > ۲× بازه Job | هشدار + برچسب کهنگی در UI |
| خطای متوالی Provider | ≥ ۳ بار | هشدار + سوئیچ به منبع جایگزین |
| نرخ غیرعادی (Anomaly) | جهش > آستانه | صف بررسی اپراتور |
| فضای دیسک آپلود/DB | < ۱۵٪ | هشدار |
| خطای 5xx افزایشی | > ۱٪ درخواست‌ها | هشدار |
| SecurityEvent بحرانی | هر رخداد | بررسی فوری |

## ۸. بازگشت به عقب (Rollback) — سناریوها
| سناریو | رویه |
| --- | --- |
| خطای نسخه برنامه | توقف، بازگردانی پوشه انتشار قبلی، بررسی نسخه Migration (بدون Down در Prod) |
| Migration معیوب | توقف برنامه، بازگردانی از پشتیبان پیش از Migration، اجرای اسکریپت اصلاحی آزمایش‌شده در Staging |
| اختلاف داده نرخ | توقف نمایش نرخ مشکوک (پرچم `Invalid`)، بررسی `RawJson` و `PriceFetchRun`، اصلاح و ثبت در `BUGS.md` |
| نشت Secret | ابطال کلید، چرخش، بازبینی Audit/SecurityEvent، ثبت Incident |

## ۹. موجودی تحویل نهایی (Definition of Done استقرار)
- [ ] Build/Test سبز با خروجی واقعی ثبت‌شده.
- [ ] Migration روی Staging با داده واقعی‌نما آزمون‌شده.
- [ ] Secretها خارج از مخزن و تأیید نبود راز در تاریخچه Git.
- [ ] HTTPS + Headerهای امنیتی آزمون‌شده.
- [ ] Backup زمان‌بندی‌شده + **آزمون بازیابی مستند**.
- [ ] Health Check + هشدار فعال.
- [ ] `docs/DEPLOYMENT.md` با مقادیر واقعی محیط (بدون راز) تکمیل‌شده.
- [ ] آموزش کوتاه مالک: اجرای Job، مشاهده لاگ، چرخش کلید، بازیابی پشتیبان.
