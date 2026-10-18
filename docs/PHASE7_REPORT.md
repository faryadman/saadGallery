# گزارش وظیفه: فاز ۷ — PWA و اعلان نسخه جدید
وضعیت: پیاده و آزموده در cloud؛ پذیرش نهایی فاز در انتظار نصب فیزیکی Android/iOS روی HTTPS معتبر مالک.
تاریخ کاربر: 2026-10-19؛ زمان‌های ابزار UTC هستند.

## ۱) چه کاری انجام شد
- Manifest، آیکون‌های any/maskable/apple و راهنمای نصب در `/pwa/install`؛ تشخیص محیط امن، beforeinstallprompt در مرورگر پشتیبان و راهنمای دستی iOS.
- SW نسخه‌دار از هش فایل‌های عمومی و اسمبلی؛ precache با integrity و allowlist صریح. هیچ HTML زنده (شامل مهمان دارای ضدجعل)، داده حساب/تیکت، تصویر خصوصی، API عضو یا POST کش نمی‌شود. HTML زنده و پاسخ احرازشده/حساس no-store هستند.
- fallback مستقل آفلاین با آخرین DTO عمومی نرخ: رشته decimal قالب‌بندی‌شده و واحد از سرور، زمان اعلام/دریافت، نمونه آزمایشی و انقضا؛ حالت بدون داده/منقضی عدد نمایش نمی‌دهد. درخواست کش نرخ بدون cookie و پاسخ ذخیره‌شده فقط فیلدهای مجاز است.
- اعلان «نسخه جدید آماده است»، Later/یادآور پانوشت، کنترل dirty/file و تأیید دوباره؛ skipWaiting و reload فقط به انتخاب کاربر، بدون reload خودکار سایر تب‌ها. درخواست POST در قطعی تشخیص‌داده‌شده روی همان فرم متوقف می‌شود؛ پیام یا فایل آفلاین صف نمی‌شود.
- worker پاک‌سازی‌کننده rollback با حذف فقط cacheهای عمومی خود برنامه و unregister، بدون reload فرم‌های باز.

## ۲) فایل‌های تغییر‌یافته
| مسیر | نوع تغییر |
| --- | --- |
| `AGENTS.md` | اصلاح |
| `deployment/pwa-cleanup-worker.js` | افزوده |
| `docs/CHANGELOG.md` | اصلاح |
| `docs/DECISIONS/ADR-0008-handmade-pwa.md` | اصلاح |
| `docs/DECISIONS/ADR-0021-pwa-private-cache-and-updates.md` | افزوده |
| `docs/DECISIONS/README.md` | اصلاح |
| `docs/DEPLOYMENT.md` | اصلاح |
| `docs/KNOWN_LIMITATIONS.md` | اصلاح |
| `docs/PHASE7_REPORT.md` | افزوده |
| `docs/PROJECT_STATUS.md` | اصلاح |
| `docs/ROADMAP.md` | اصلاح |
| `docs/SECURITY.md` | اصلاح |
| `docs/TESTING.md` | اصلاح |
| `docs/reports/phase7-validation.json` | افزوده |
| `src/SadGallery.Application/Market/OfflineRates.cs` | افزوده |
| `src/SadGallery.Web/Controllers/PwaController.cs` | افزوده |
| `src/SadGallery.Web/Program.cs` | اصلاح |
| `src/SadGallery.Web/Pwa/PwaAssets.cs` | افزوده |
| `src/SadGallery.Web/Pwa/service-worker.js` | افزوده |
| `src/SadGallery.Web/SadGallery.Web.csproj` | اصلاح |
| `src/SadGallery.Web/Views/Pwa/Install.cshtml` | افزوده |
| `src/SadGallery.Web/Views/Shared/_Layout.cshtml` | اصلاح |
| `src/SadGallery.Web/Views/Shared/_PwaUpdate.cshtml` | افزوده |
| `src/SadGallery.Web/wwwroot/css/site.css` | اصلاح |
| `src/SadGallery.Web/wwwroot/js/offline-page.js` | افزوده |
| `src/SadGallery.Web/wwwroot/js/pwa.js` | افزوده |
| `src/SadGallery.Web/wwwroot/manifest.webmanifest` | افزوده |
| `src/SadGallery.Web/wwwroot/offline.html` | افزوده |
| `src/SadGallery.Web/wwwroot/pwa/icon-180.png` | افزوده |
| `src/SadGallery.Web/wwwroot/pwa/icon-192.png` | افزوده |
| `src/SadGallery.Web/wwwroot/pwa/icon-512.png` | افزوده |
| `src/SadGallery.Web/wwwroot/pwa/icon-maskable-512.png` | افزوده |
| `src/SadGallery.Web/wwwroot/pwa/icon.svg` | افزوده |
| `tests/SadGallery.Tests.Integration/Pwa/PwaEndpointTests.cs` | افزوده |
| `tests/pwa/service-worker.test.cjs` | افزوده |

## ۳) تست‌های اجراشده و خروجی واقعی
| بررسی | نتیجه |
| --- | --- |
| restore/build با SDK10.0.401 و warnaserror | صفر هشدار، صفر خطا، شش پروژه |
| `dotnet test --no-build` با تزریق امن SQL Server2022 | **601 موفق، ۰ شکست، ۰ Skip**؛ حدود۲۴ثانیه؛ ۳ تست جدید endpoint/SRI/DTO/HTML no-store |
| `node --test tests/pwa/service-worker.test.cjs` | **7 موفق، ۰ شکست/Skip**؛ privacy، sanitize، marker/schema، نصب ناقص، activate صریح و rollback |
| Chromium154 Manifest/CDP installability | صفر خطای manifest و installability؛ پروفایل persistent؛ incognito به‌درستی نصب را منع می‌کند |
| مرورگر واقعی آفلاین | SW کنترل‌شده، fallback روی URL خصوصی بدون HTML خصوصی، نرخ عمومی با نمونه/زمان/واحد، انقضا و خالی/پاک‌کردن، اتصال مجدد، POST آفلاین با حفظ ورودی، بدون overflow390 یا خطای JS |
| بازرسی cache/storage با canary واقعی | متن/نام خصوصی، رمز ساختگی و CSRF فرم در CacheStorage/LocalStorage/SessionStorage یافت نشد؛ هیچ کلید مسیر خصوصی/API/upload وجود نداشت |
| releaseهای واقعیِ کپی‌شده A→B→C→D | B با hash تازه waiting و اعلان شد؛ Later و یادآور، dirty/file confirmation و دقیقاً یک reload صریح؛ تب دوم متنش را حفظ کرد؛ C ناقص با SRI رد شد و B ماند؛ D دوباره آماده شد |
| HTTP غیرامن با hostname شبیه LAN | ثبت/نصب ادعا نشد و راهنمای HTTPS روشن نمایش داده شد |
| Lighthouse13.5.0 | Accessibility=100، Best Practices=100، SEO=100؛ این نسخه دسته PWA ندارد و نصب‌پذیری جدا با CDP بررسی شد |
| `python3 scripts/accessibility-check.py` | همه۲۴ جفت‌رنگ و معیارهای موجود هدف لمسی پذیرفته شدند؛ معادل ممیزی کامل نیست |
| publish Release با warnaserror | خروجی فیزیکی ساخته شد؛ worker/manifest/offline/icon/version/health روی loopback در حالت Production خوانده شدند؛ انتشار Production یا TLS واقعی انجام نشد |
| `git diff --check` | بدون خطای whitespace |

یک اجرای publish هم‌زمان با تست‌ها با MSB4166 و خروج child node شکست خورد؛ فایل diagnostic قابل‌استفاده تولید نشد. سرویس smoke خودمان متوقف شد و publish تک‌پردازشی با `-m:1 --disable-build-servers` موفق شد؛ علت قطعی خروج node ادعا نمی‌شود. هیچ assertion، integrity یا سیاست امنیتی برای رفع شکست حذف نشد.

در آزمون A/B، manifest توسعه ASP.NET در ابتدا فایل‌های checkout را بر webroot کپی ترجیح می‌داد و hash دو نسخه یکسان بود؛ در harness به مسیر manifest توسعه ناموجود اشاره داده شد تا release واقعی کپی خوانده شود. این تنظیم فقط harness بود و کد/امنیت برنامه برای تست تغییر نکرد. حساب‌های مصنوعی مرورگر در finally غیرفعال و نقش‌هایشان حذف شدند؛ داده/حساب واقعی مالک تغییر نکرد. لاگ‌ها و تصاویر کامل بیرون checkout در ابزارهای محیط هستند. [خلاصه machine-readable](reports/phase7-validation.json)؛ گزارش کامل Lighthouse در artifacts نادیده‌گرفته‌شده و ابزارهای محیط حفظ است.

## ۴) مشکلات باقی‌مانده / مسدودکننده‌ها
دستگاه فیزیکی Android/iOS و دامنه HTTPS معتبر مالک در این محیط موجود نیست؛ نصب واقعی گوشی و Safari واقعی اجرا نشده و checkbox آن باز است. emulation/CDP نصب فیزیکی گزارش نشده‌اند. HTTP آی‌پی خانه برای اجرای سایت کافی است و برای نصب استاندارد PWA کافی نیست. PWA فعلی سرور یا SQL داخل گوشی و APK/IPA ایجاد نمی‌کند؛ محاسبه‌گرهای سمت سرور/حساب/تیکت و تاریخچه اعضا آفلاین کامل ندارند. ارسال پس‌زمینه/Push/SMS اضافه نشده و تنظیم sms.ir خالی/غیرفعال مانده است.

## ۵) ریسک امنیتی
فقط فایل‌های allowlist عمومی و DTO محدود نرخ بدون cookie ذخیره می‌شوند. HTML/POST و پسوند تصویر ملاک cache نیستند؛ مقادیر فرم، فایل، token، اطلاعات تماس و provider credential/URL در cache نیستند. DOM نرخ با textContent ساخته می‌شود؛ مالی در سرور decimal است. cache عمومی در برابر XSS همان origin یا دسترسی سیستم عامل رمزنگاری مستقل ندارد. سیاست انقضا جلوی نمایش نرخ نامعتبر ذخیره‌شده را می‌گیرد.

بررسی نسخه در تب فعال/بازگشت اتصال و هر۶۰ثانیه است؛ تب خوابیده ممکن است دیرتر متوجه شود. داده آفلاین ممکن است توسط OS/مرورگر پاک شود. «بعداً» در حافظه همان صفحه است؛ با پیمایش بعدی می‌تواند دوباره اعلان شود. Header/cookieهای خصوصی در Response کش نرخ کپی نمی‌شوند. TLS، DataProtection/backup عملیاتی، OTP و MFA تصمیم‌ها/کارهای باقی قبلی هستند.

## ۶) Migration لازم است؟
خیر؛ schema داده تغییر نکرد. انتشار باید یکپارچه و با restart بعد از کپی همه فایل‌ها باشد؛ تغییر درجا پس از شروع فرایند می‌تواند SRI را رد کند و نسخه قبلی را نگه دارد. برای rollback، فایل `deployment/pwa-cleanup-worker.js` را موقتاً در همان origin به‌عنوان `/service-worker.js` با MIME/no-cache صحیح ارائه و سپس نسخه قبلی/اصلاحی را مستقر کنید. صرف حذف URL worker آن را از دستگاه‌های دیرمتصل پاک نمی‌کند. جزئیات در DEPLOYMENT است؛ cleanup/unregister بدون تغییر navigation در تست مستقل بررسی شد.

## ۷) به‌روزرسانی مستندات
ROADMAP معیارهای پیش از کد و نتایج با checkbox باز نصب فیزیکی؛ PROJECT_STATUS/CHANGELOG وضعیت واقعی؛ ADR-0008 اصلاح مرز cache و ADR-0021 تصمیم جدید؛ SECURITY/KNOWN_LIMITATIONS/DEPLOYMENT/TESTING مرزها و شواهد؛ AGENTS فاز جاری و این گزارش/خلاصه JSON ثبت شدند.

## ۸) قدم بعدی دقیق
روی دامنه HTTPS مورداعتماد گوشی مالک، صفحه «نصب برنامه» باز و نصب/یک بار اجرای آفلاین روی یک Android و یک iPhone واقعی انجام و نتیجه به گزارش اضافه شود.
