# تاریخ تغییرات — SadGallery

قالب بر اساس [Keep a Changelog](https://keepachangelog.com/fa/1.1.0/) · نسخه‌گذاری بر اساس [Semantic Versioning](https://semver.org/lang/fa/).

## [Unreleased] — فاز ۰: بررسی و طرح

### Added
- مخزن Git و ساختار مستندات دائمی پروژه.
- `AGENTS.md` به‌عنوان دستورالعمل دائمی (شامل آیین شروع جلسه، اصول غیرقابل مذاکره، قالب گزارش پایان وظیفه، محدودیت‌های محیط ایجنت).
- `README.md` معرفی محصول، ساختار مخزن و مسیرهای شروع.
- `docs/PROJECT_STATUS.md` — وضعیت لحظه‌ای، مسدودکننده‌ها، ریسک‌های فعال، قدم بعدی.
- `docs/ROADMAP.md` — فازهای ۰ تا ۸ با معیار پذیرش آزمون‌پذیر.
- `docs/ARCHITECTURE.md` — لایه‌ها، جریان داده نرخ، سیاست تازگی، نسخه‌های تأییدشده، الگوهای استفاده‌شده/نشده.
- `docs/DATA_MODEL.md` — موجودیت‌ها، نوع داده دقیق مالی، Index/Constraint، برنامه Migration و نگهداری داده.
- `docs/SECURITY.md` — دارایی‌ها، مدل تهدید، کنترل‌ها، معماری کلید و عبارت محرمانه، رویه چرخش کلید، چک‌لیست استقرار، ریسک‌های پذیرفته‌شده.
- `docs/API.md` — قرارداد موردنیاز از API بیرونی نرخ، چک‌لیست داده‌های لازم از مالک، نمونه‌های JSON، قواعد نگاشت و API داخلی.
- `docs/DEPLOYMENT.md` — محیط‌ها، پیکربندی، Secretها، پشتیبان‌گیری و بازیابی، Runbook و Rollback.
- `docs/TESTING.md` — هرم تست، دستورها، تست‌های امنیتی الزامی، Quality Gates و محدودیت‌های سندباکس.
- `docs/BUGS.md`، `docs/KNOWN_LIMITATIONS.md`، `docs/OPEN_QUESTIONS.md`، `docs/PHASE0_REPORT.md`.
- ADR-0001..ADR-0009 در `docs/DECISIONS/`.
- `scripts/dev-setup.sh` (نصب SDK در سندباکس، غیرماندگار) و `scripts/test.sh`.
- `.gitignore` و `.editorconfig` با قواعد سخت‌گیرانه سطح هشدار امنیتی.

## [Unreleased] — فاز ۱: زیرساخت

### Added
- **Solution و ساختار پروژه:** `SadGallery.sln` (قالب کلاسیک — ADR-0010) با ۶ پروژه: Domain، Application، Infrastructure، Web، Tests.Unit، Tests.Integration — همه با `net10.0`، `Nullable=enable` و `TreatWarningsAsErrors=true`.
- `global.json` (SDK 10.0.100 با `rollForward: latestFeature`) و `Directory.Build.props` و `Directory.Packages.props` (Central Package Management، نسخه‌های Pin‌شده).
- **Domain:** نوع‌های ارزشی `Money` (با واحد صریح + تبدیل صریح)، `Purity` (عیار/ضریب خلوص)، `Weight` (واحد پایه گرم + مثقال)، enumهای `CurrencyUnit` و `RateQuality` و ثابت‌های `Measurements`.
- **Application:** `IClock`، `IIdentitySeeder`، `PersianText` (نرمال‌سازی ارقام فارسی/عربی برای ورودی و نمایش) و `AddSadGalleryApplication`.
- **Infrastructure:** `ApplicationUser`، `RoleNames`، `IdentitySeeder` (ایدِمپوتنت، بدون ساخت کاربر پیش‌فرض)، `SadGalleryDbContext`، `SadGalleryDbContextFactory` (زمان طراحی)، `SystemClock` و `AddSadGalleryInfrastructure` با Fail-Fast روی نبود رشته اتصال و `EnableRetryOnFailure`.
- **Web:** `Program.cs` با Identity (سیاست رمز/قفل)، Policyهای `AdminOnly`/`OperatorArea`/`MemberFeatures` (شامل Claim «حساب فعال»)، Rate Limiting روی ورود/ثبت‌نام، نقطه‌های سلامت `/health` و `/health/ready`، صفحه خطای فارسی، Middleware سرآیندهای امنیتی، ضدجعل خودکار روی همه POSTها، OpenAPI فقط در محیط توسعه.
- صفحات فارسی RTL: Layout برند، خانه، ورود، ثبت‌نام، AccessDenied، ناحیه اعضا، پنل‌های Admin و Operator (با Policy سمت سرور).
- صفحه ورود/ثبت‌نام با ترجمه فارسی خطاهای Identity و پیام یکنواخت (بدون افشای وجود حساب).
- دارایی‌های self-host: Bootstrap 5.3.8 (RTL + JS) و فونت Vazirmatn 33.0.3 با فایل مجوز (ADR-0010).
- **مهاجرت `InitialIdentity`** (۷ جدول `AspNet*` + جدول `__EFMigrationsHistory`؛ ۸ ایندکس از جمله ایندکس روی `IsActive`) + اسکریپت ایدِمپوتنت `database/scripts/InitialIdentity.sql`.
- تست‌ها: ۳۴ تست واحد (پول، عیار، وزن، متن فارسی) و ۱۷ تست یکپارچه (سلامت، صفحه اصلی RTL و نبود Entity، صفحه خطا ×۳، سرآیندها و نبود سرآیند `Server`، کوکی ناامن، مجوزدهی، CSRF ×۲ و مهاجرت/Seed با `RequiresSqlServerFact`).
- اسکریپت‌ها: `scripts/ef.sh` (پوشش دستورهای EF)، نصب خودکار `dotnet-ef` در `scripts/dev-setup.sh`، و `scripts/git-commit.sh` (هویت Git پایدار در محیط ایجنت).
- **Seed کاربران اولیه (ADR-0011):** بخش تنظیمات `SeedUsers` + رمز از `SADGALLERY_SEED_PASSWORD`/`SeedUsers:n:Password`؛ اعتبارسنجی fail-closed پیش از هر دسترسی به دیتابیس (کد خروج ۲)، ایدِمپوتنس امن (کاربر موجود بازنویسی/تغییررمز نمی‌شود)، افزودن نقش جاافتاده، و ممنوعیت چاپ/لاگ رمز. نمونه‌های توسعه در `appsettings.Development.json` (بدون رمز).
- سه کاربر نمونهٔ توسعه: `admin@sadgallery.local` (Admin)، `operator@sadgallery.local` (Operator)، `customer@sadgallery.local` (Customer) — رمز توسط خود اپراتور تعیین می‌شود.
- مستندات: `ADR-0011`، اصلاح `ADR-0004`، بخش §۴.۶ در `docs/DEPLOYMENT.md` («ساخت اولین مدیر»)، گام Seed در `README.md`.
- راهنمای «راه‌اندازی اولین‌بار دیتابیس» در `docs/DEPLOYMENT.md` §۴.۵ (پاسخ به «دیتابیس ساخته نشده، کجا مهاجرت بزنم؟») + `scripts/ef.sh` که در نبود `SADGALLERY_CONNECTION` از `ConnectionStrings__SadGallery` استفاده می‌کند.

### Changed
- **مهاجرت پشته تست به `xunit.v3` 4.0.1**: بررسی `dotnet list package --deprecated` نشان داد `xunit 2.9.3` منسوخ (Legacy) است ⇒ مطابق اصل پروژه، مهاجرت انجام شد. در پی آن: اجرا روی Microsoft.Testing.Platform، حذف `Microsoft.NET.Test.Sdk` و `coverlet.collector`، و فعال‌سازی تجربه جدید `dotnet test` با `global.json` (`test.runner`).
- دو قاعده تحلیلگر xunit v3 (`xUnit1051` انتقال CancellationToken در تست‌ها، `xUnit3003` سازنده Attribute تست شرطی) **اصلاح شدند، نه خاموش**.
- `dotnet.config` که در این SDK اثری نداشت، حذف و راه‌حل درست (`global.json`) مستند شد (ADR-0010).

### Fixed
- BUG-001: کامنت XML حاوی `--` بارگذاری `Directory.Packages.props` را می‌شکست.
- BUG-002: صفحه خطای فارسی محتوای خود را پیدا نمی‌کرد (`The view 'Index' was not found`) ⇒ درخواست ناموجود ۵۰۰ می‌داد.
- BUG-003: متن فارسی خروجی به Entity عددی تبدیل می‌شد ⇒ تنظیم `UnicodeRanges.All` در `WebEncoderOptions`.
- BUG-004: شرط نصب در `dev-setup.sh` پوشه والد (`/opt`) را بررسی می‌کرد، نه خود پوشه نصب ⇒ رد نادرست نصب.
- BUG-005: `NUGET_HTTP_CACHE_PATH` بیرون از پوشه قابل‌نوشتن ساخته می‌شد ⇒ شکست Restore با `NU1900`.
- BUG-006: ناهماهنگی کش نوگت بین اسکریپت‌ها («وجود» در برابر «قابل‌نوشتن بودن») ⇒ ماژول مشترک `scripts/lib/env.sh`.
- BUG-008: ابزار `dotnet-ef` در `/opt/tools` (غیرقابل‌نوشتن) نصب می‌شد ⇒ اصلاح به `${DOTNET_DIR}/tools`.

### Verified
- `dotnet build SadGallery.sln -warnaserror` ⇒ **۰ خطا، ۰ هشدار**.
- `dotnet test` ⇒ **۵۴/۵۴ واحد سبز** (۳۴ قبلی + ۲۰ تست Seed کاربران) + **۱۷/۱۷ یکپارچه سبز** + ۳ Skip صریح (نیازمند SQL Server: ۱ مهاجرت + ۲ Seed).
- اجرای واقعی `--seed` در سندباکس (سه سناریو): بدون رمز ⇒ ۳ خطای فارسی و کد خروج `2` بدون لمس دیتابیس؛ با رمز ⇒ عبور از اعتبارسنجی و رسیدن به مرحلهٔ دیتابیس (در سندباکس: خطای اتصال، مورد انتظار)؛ نقش نامعتبر ⇒ «نقش Wizard شناخته‌شده نیست؛ نقش‌های مجاز: Customer, Operator, Admin». هیچ رمزی در هیچ خروجی ظاهر نشد.
- اجرای واقعی برنامه روی Kestrel: `/`=200 (۵۵۱۲ بایت، صفر Entity عددی)؛ `/no-such-page`=404 «صفحه یافت نشد»؛ `/health`=200 `Healthy`؛ `/health/ready`=503 (بدون DB، درست)؛ `/Admin|Operator|Member`=302 به ورود؛ POST بدون توکن=400؛ دارایی‌های self-host=200.
- سرآیندهای پاسخ (curl بدون فیلتر): `X-Content-Type-Options: nosniff`، `X-Frame-Options: DENY`، `Referrer-Policy: strict-origin-when-cross-origin`، `Cross-Origin-Opener-Policy: same-origin`، `Permissions-Policy: geolocation=(), camera=(), microphone=(), payment=()` — و **هیچ سرآیند `Server` ارسال نمی‌شود** (تأییدشده؛ `Program.cs` خط ۲۰).

### Notes
- **BUG-007 (باز — تحت پایش):** یک شکست ناپایدار تست یکپارچه در 2026-10-06 مشاهده شد که در ۹ اجرای بعدی بازتولید نشد؛ موازی‌سازی بین کلاس‌های تست غیرفعال شد تا این کلاس از ناپایداری حذف شود.
- **اجرا نشده در سندباکس (نیازمند SQL Server):** مهاجرت روی دیتابیس واقعی، Seed نقش‌ها، ورود/ثبت‌نام و قفل حساب. دستورهای اجرا برای مالک در `README.md` و `docs/TESTING.md`.

### Published
- **فاز صفر روی مخزن رسمی منتشر شد**: `https://github.com/faryadman/saadGallery.git` — شاخه `main`، ۶ کامیت، ۳۰ فایل (کامیت `eeefdbb`).
- **فاز ۱ منتشر شد (2026-10-06):** `git push` از `eeefdbb` به `64a1ff7` (فست‌فوروارد، بدون بازنویسی تاریخچه) — ۱۴ کامیت، ۱۲۴ فایل، شامل `SadGallery.sln`.
  - **تأیید پس از انتشار (اجرای واقعی):** `ls-remote` ناشناس بدون احراز هویت SHA ریموت را برابر `64a1ff7` نشان داد؛ **کلون تازهٔ ناشناس از GitHub** با `dotnet build -warnaserror` ⇒ `Build succeeded, 0 Warning(s), 0 Error(s)`؛ اسکن اسرار روی نسخهٔ منتشرشده ⇒ صفر تطابق.
  - فرایند انتشار بدون ذخیرهٔ توکن: توکن فقط به‌صورت گذرا در فایل موقت با مجوز `600` بیرون از مخزن، با `credential.helper` فقط برای همان یک دستور؛ پس از push با بازنویسی تصادفی پاک شد. `.git/config` و `.git-credentials` دست‌نخورده و پاک ماندند (بررسی شد).
- **تأیید پس از انتشار (اجرای واقعی):** clone ناشناس بدون احراز هویت موفق شد؛ اسکن اسرار روی نسخه عمومی صفر مورد نشان داد (چهار تطابق اولیه همه جای‌نگهدار/دیتابیس تست محلی بودند و بررسی شدند).
- فرایند انتشار بدون ذخیره توکن انجام شد: توکن فقط در یک فایل موقت با مجوز `600`، با `credential.helper` غیرفعال، بدون نوشتن در `.git/config`؛ پس از push حذف شد.

### Repository
- مخزن رسمی پروژه تعیین و بررسی شد: `https://github.com/faryadman/saadGallery.git` — نتیجه بررسی: عمومی، خالی (صفر ref) ⇒ تأیید Greenfield بودن پروژه. شاخه محلی به `main` تغییر نام یافت؛ آدرس ریموت در `README.md` و `AGENTS.md` ثبت شد (چون `.git/config` در snapshot محیط ایجنت ذخیره نمی‌شود).
- `Q-REPO-1` در `OPEN_QUESTIONS.md` ثبت شد (تفاوت نام مخزن `saadGallery` با نام فنی `SadGallery`).

### Verified (خروجی واقعی، نه ادعا)
- `scripts/dev-setup.sh` در سندباکس با موفقیت **.NET SDK 10.0.401** (Host 10.0.12، ~۶۲۲MB) را در `/opt/dotnet` نصب کرد.
- قالب `dotnet new mvc` در دسترس است.
- restore موفق `Microsoft.EntityFrameworkCore.SqlServer 10.0.12` از NuGet در ۳.۴۶ ثانیه ⇒ فاز ۱ در سندباکس قابل Build و Unit Test است.
- SQL Server/Docker در سندباکس موجود نیست ⇒ تست‌های Integration با Trait `RequiresSqlServer` و Skip صریح.

### Notes
- نسخه‌های بررسی‌شده (2026-10-05): .NET 10 LTS (آخرین Patch 10.0.12، EOL 2028-11-14)، Bootstrap 5.3.8.
- هیچ کد اجرایی، Migration یا Secret در این مرحله ایجاد نشده است؛ فاز صفر صرفاً طراحی و مستندسازی است.
- منتظر پاسخ `docs/OPEN_QUESTIONS.md` (به‌ویژه `Q-API-1`) و تأیید مالک برای شروع فاز ۱.
