# گزارش وظیفه: اجرای پنل فاز ۶
وضعیت: انجام‌شده در دامنه معیارهای پنل ROADMAP؛ بازبینی مالک باقی است. تاریخ: 2026-10-16.

## ۱) چه کاری انجام شد
- داشبورد واقعی مدیر/اپراتور و گزارش عملیاتی با شمارش تیکت/محصول، سلامت منبع، آخرین دریافت/Job و فهرست نرخ‌های کهنه، نامعتبر و جهش‌دار.
- کاربران و نقش‌ها با جست‌وجو/صفحه‌بندی، نسخه هم‌زمانی، محافظ حساب خود و آخرین مدیر فعال و تراکنش چندنمونه‌ای SQL؛ ابطال نشست قبلی و نشست جدید ۳۰دقیقه‌ای مدیر/اپراتور.
- StoreSettings مجاز برای توقف/ادامه و فاصله دریافت، RowVersion و اثر بدون restart؛ در نبود/خطای تنظیمات یا ثبت‌نشدن سرویس خواندن، تماس خروجی مسدود است. سیاست استقرار/اسرار و URL از پنل قابل تغییر نیستند.
- Audit اتمیکِ نام فیلدهای حساس و گزارش صفحه‌بندی‌شده/CSV حداکثر ۵۰۰ ردیف با محافظ فرمول؛ خطای فرم و تعارض در همان رابط، با حفظ انتخاب‌ها. منو و جدول‌های موبایل با سبک گروه‌بندی هماهنگ‌اند.

## ۲) فایل‌های تغییر‌یافته
| مسیر | نوع تغییر |
| --- | --- |
| `AGENTS.md` | اصلاح |
| `database/scripts/AdministrationSettingsAudit.sql` | افزوده |
| `database/scripts/AdministrationSettingsAuditDown.sql` | افزوده |
| `docs/BUGS.md` | اصلاح |
| `docs/CHANGELOG.md` | اصلاح |
| `docs/DATA_MODEL.md` | اصلاح |
| `docs/DECISIONS/ADR-0020-administration-and-audit.md` | افزوده |
| `docs/DECISIONS/README.md` | اصلاح |
| `docs/DEPLOYMENT.md` | اصلاح |
| `docs/KNOWN_LIMITATIONS.md` | اصلاح |
| `docs/PHASE6_REPORT.md` | افزوده |
| `docs/PROJECT_STATUS.md` | اصلاح |
| `docs/ROADMAP.md` | اصلاح |
| `docs/SECURITY.md` | اصلاح |
| `docs/TESTING.md` | اصلاح |
| `src/SadGallery.Application/Administration/AdminContracts.cs` | افزوده |
| `src/SadGallery.Infrastructure/Administration/AdministrationStore.cs` | افزوده |
| `src/SadGallery.Infrastructure/DependencyInjection.cs` | اصلاح |
| `src/SadGallery.Infrastructure/Market/RateFetchBackgroundService.cs` | اصلاح |
| `src/SadGallery.Infrastructure/Market/RateFetchOrchestrator.cs` | اصلاح |
| `src/SadGallery.Infrastructure/Persistence/AdministrationAudit.cs` | افزوده |
| `src/SadGallery.Infrastructure/Persistence/AdministrationModel.cs` | افزوده |
| `src/SadGallery.Infrastructure/Persistence/Entities/Administration.cs` | افزوده |
| `src/SadGallery.Infrastructure/Persistence/Migrations/20261016010911_AdministrationSettingsAudit.Designer.cs` | افزوده |
| `src/SadGallery.Infrastructure/Persistence/Migrations/20261016010911_AdministrationSettingsAudit.cs` | افزوده |
| `src/SadGallery.Infrastructure/Persistence/Migrations/SadGalleryDbContextModelSnapshot.cs` | اصلاح |
| `src/SadGallery.Infrastructure/Persistence/SadGalleryDbContext.cs` | اصلاح |
| `src/SadGallery.Web/Areas/Admin/Controllers/DashboardController.cs` | اصلاح |
| `src/SadGallery.Web/Areas/Admin/Controllers/ManagementController.cs` | افزوده |
| `src/SadGallery.Web/Areas/Admin/Views/Dashboard/Index.cshtml` | اصلاح |
| `src/SadGallery.Web/Areas/Admin/Views/Management/Audit.cshtml` | افزوده |
| `src/SadGallery.Web/Areas/Admin/Views/Management/Settings.cshtml` | افزوده |
| `src/SadGallery.Web/Areas/Admin/Views/Management/UserAccess.cshtml` | افزوده |
| `src/SadGallery.Web/Areas/Admin/Views/Management/Users.cshtml` | افزوده |
| `src/SadGallery.Web/Areas/Operator/Controllers/DashboardController.cs` | اصلاح |
| `src/SadGallery.Web/Areas/Operator/Controllers/ReportsController.cs` | افزوده |
| `src/SadGallery.Web/Areas/Operator/Views/Dashboard/Index.cshtml` | اصلاح |
| `src/SadGallery.Web/Areas/Operator/Views/Reports/Index.cshtml` | افزوده |
| `src/SadGallery.Web/Controllers/AccountController.cs` | اصلاح |
| `src/SadGallery.Web/Models/AdministrationForms.cs` | افزوده |
| `src/SadGallery.Web/Program.cs` | اصلاح |
| `src/SadGallery.Web/Security/AppUserClaimsPrincipalFactory.cs` | اصلاح |
| `src/SadGallery.Web/Security/HttpAuditContext.cs` | افزوده |
| `src/SadGallery.Web/Views/Shared/ManagementUnavailable.cshtml` | افزوده |
| `src/SadGallery.Web/Views/Shared/_Layout.cshtml` | اصلاح |
| `src/SadGallery.Web/Views/Shared/_OperationsDashboard.cshtml` | افزوده |
| `src/SadGallery.Web/wwwroot/css/site.css` | اصلاح |
| `tests/SadGallery.Tests.Integration/Administration/AdministrationAuthorizationTests.cs` | افزوده |
| `tests/SadGallery.Tests.Integration/Administration/AdministrationSqlTests.cs` | افزوده |
| `tests/SadGallery.Tests.Integration/Administration/AuditAtomicitySqlTests.cs` | افزوده |
| `tests/SadGallery.Tests.Integration/Administration/RuntimeRateSettingsTests.cs` | افزوده |
| `tests/SadGallery.Tests.Integration/Administration/SessionValidationTests.cs` | افزوده |
| `tests/SadGallery.Tests.Integration/Market/RateFetchOrchestratorTests.cs` | اصلاح |
| `tests/SadGallery.Tests.Unit/Administration/AdminRulesTests.cs` | افزوده |

## ۳) تست‌های اجراشده و خروجی واقعی
| بررسی | نتیجه واقعی |
| --- | --- |
| `dotnet restore` و `dotnet build --no-restore -warnaserror -m:4` از helper راه‌اندازی آزموده‌شده | ۰ هشدار، ۰ خطا، هر ۶ پروژه |
| پایه پیش از تغییر: `dotnet test --no-build` با تزریق امن SQL | 567 موفق، ۰ شکست، ۰ Skip |
| نهایی: `dotnet test --no-build` روی SQL Server 2022 Developer | **598 موفق، ۰ شکست، ۰ Skip**؛ حدود ۲۵ ثانیه |
| ماتریس MVC و SQL/Identity واقعی | کشف تمام actionهای Admin/Operator؛ منع Customer/مهمان، منع Operator از Admin، CSRF؛ HTML/CSV با canary اعتبارنامه و URL، انقضای کوکی و ابطال پس از تغییر نقش/تعلیق |
| هم‌زمانی/ممیزی | درخواست هم‌زمان دو مدیر، حفظ حداقل یک مدیر؛ نسخه stale، منع تغییر خود؛ rollback تغییر محصول و Audit در شکست نسخه تیکت؛ ثبت واقعی عملیات HTTP محصول/تیکت/کاربر/تنظیمات و منع ویرایش Audit |
| دریافت نرخ | توقف/نبود رکورد/خطا/نبود ثبت DI در Scheduled و Manual بدون تماس؛ pause/resume در همان orchestrator؛ حداقل فاصله و محافظ مقدار |
| مهاجرت و SQL Up/Down | Dev/Test و DB جدا؛ RowVersion و singleton؛ Down فقط دو جدول فاز ۶ را حذف کرد، داده Identity/تیکت/پیام/اعلان باقی ماند؛ Up دو بار idempotent و seed/history یکتا |
| `dotnet ef migrations has-pending-model-changes --no-build` | No changes have been made to the model since the last migration |
| Chromium واقعی | مدیر/اپراتور/مشتری، جست‌وجو و ویرایش نقش، خروج نشست قدیمی، منع تغییر خود، فرم stale=409 و invalid=400 با حفظ انتخاب، همه صفحات و منوی موبایل ۳۹۰px بدون overflow صفحه؛ تصاویر دسکتاپ/موبایل بازبینی شدند |
| `python3 scripts/accessibility-check.py` | همه ۲۴ جفت‌رنگ و حضور هدف‌های لمسی پذیرفته شد |
| `git diff --check` | بدون خطای whitespace |

تنظیمات Dev بعد از آزمون مرورگر به مقدار قبلی بازگردانده شد؛ حساب‌های مصنوعی `admin6-ui-*` ساخته‌شده در همین بررسی غیرفعال و نقش‌هایشان پاک شدند. اطلاعات واقعی مالک/اعتبارنامه‌ای در خروجی چاپ نشد. لاگ‌های محلی بررسی در ابزارهای خارج checkout هستند؛ screenshotهای مرجع `admin-dashboard-desktop.png`، `admin-dashboard-mobile.png`، `admin-user-mobile.png` و `admin-menu-mobile.png` نیز همان‌جا هستند.

## ۴) مشکلات باقی‌مانده / مسدودکننده‌ها
پنل معیارهای فاز ۶ را پوشش می‌دهد. انتشار Production، داده محصول واقعی و تأیید جداگانه ۴.۱ انجام نشده‌اند. sms.ir به دستور قبلی مالک غیرفعال و الگو/پارامترها خالی‌اند؛ OTP پیامکی و MFA اجباری پیاده نشده‌اند و سیاست Q-AUTH-1 باز است. گزارش فروش/فاکتور و ویرایش تاریخی جهش نرخ در دامنه این پنل نبود. مرورگر فیزیکی Safari/iOS بررسی نشده است.

## ۵) ریسک امنیتی
Policy سمت سرور، CSRF، نسخه هم‌زمانی و بررسی مجدد نقش فعال در ذخیره اعمال می‌شود. تغییر دسترسی در قفل تراکنشی استاندارد SQL و SecurityStamp Identity انجام می‌شود. اسرار، SecurityStamp، رمز، اطلاعات تماس و کلیدهای TOTP در پنل نیستند؛ توکن نسخه کاربر ConcurrencyStamp است. Audit فقط نام فیلد دارد؛ CSV شامل IP/CorrelationId نیست. بررسی نشستِ ناموفق کوکی را رد/حذف می‌کند و فقط نوع خطا لاگ می‌شود.

بررسی stamp در هر درخواست احرازشده هزینه DB دارد؛ درخواست آغازشده قبل از توقف/تغییر نقش ممکن است کامل شود. نشست‌های قبل از ارتقا تا انقضای قبلی/ابطال stamp می‌مانند. Audit درخواست HTTP و تغییرهای allowlist را پوشش می‌دهد؛ عملیات مستقیم SQL/CLI و مقادیر قبل/بعد را ضبط نمی‌کند. حفاظت در برابر DBA، محدودیت حساب SQL، retention/backup، TLS/DataProtection عملیاتی و MFA اجباری برای سخت‌سازی باقی‌اند.

## ۶) Migration لازم است؟
بله: `20261016010911_AdministrationSettingsAudit`، شامل StoreSettings و AuditEvents و seed تک‌ردیفی. SQL رفت/بازگشت در `database/scripts/AdministrationSettingsAudit.sql` و `AdministrationSettingsAuditDown.sql` است. ابتدا migration، سپس راه‌اندازی کد جدید. برای Down برنامه را متوقف/به نسخه قبلی برگردانید و Audit را backup کنید؛ Down این دو جدول و داده‌شان را حذف می‌کند، سایر داده‌ها باقی می‌مانند. این فرایند واقعاً روی DB جدا با داده قبلی آزموده شد؛ سرور عملیاتی تغییری نکرد.

## ۷) به‌روزرسانی مستندات
ROADMAP معیارهای پیش از پیاده‌سازی و شواهد نهایی، PROJECT_STATUS و CHANGELOG وضعیت واقعی، DATA_MODEL مدل اجراشده، SECURITY/KNOWN_LIMITATIONS مرزها، TESTING شواهد، DEPLOYMENT ارتقا/بازگشت، BUGS اصلاح‌های کشف‌شده، ADR-0020 تصمیم و AGENTS وضعیت فاز را ثبت می‌کنند.

## ۸) قدم بعدی دقیق
مالک یک حساب اپراتور واقعی را در پنل کاربران بازبینی کند و گردش پاسخ/اتمام یک تیکت واقعی را در محیط موردنظر آزمایش کند؛ ارسال پیامک تا تعیین الگوی تأییدشده غیرفعال می‌ماند.
