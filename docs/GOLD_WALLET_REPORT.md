# گزارش وظیفه: کنترل‌پنل مشتری ویژه و کیف طلای آب‌شده
وضعیت: انجام‌شده و آزموده در محیط توسعه/SQL آزمایشی · تاریخ: 2026-10-19

اصلاح بعدی BUG-016 (Version فعال‌سازی اولیه) و رگرسیون634 تست در [WALLET_VERSION_FIX_REPORT.md](WALLET_VERSION_FIX_REPORT.md) ثبت شده است.

### ۱) چه کاری انجام شد
بر اساس پاسخ‌های مالک، کیف دو مانده تومان و گرم معادل عیار۷۵۰، شارژ تومان/طلا با دلیل توسط مدیر، کارمزد خرید افزوده بر اصل طلا، خرید از نرخ آنلاین معتبر سایت و نهایی‌شدن با تأیید مدیر یا اپراتور پیاده شد. مشتری ابتدا پیش‌نمایش مبلغ را می‌بیند؛ ثبت فقط تومان را رزرو می‌کند. تأیید همان مبلغ/وزن ثابت را اتمیک ثبت می‌کند؛ رد با توضیح و لغو مالک، رزرو را آزاد می‌کنند. سوابق قطعی و درخواست‌ها جدول، وضعیت، رسید و عملیات دارند؛ فیلترهای موبایل با برنامه همسان‌اند.

شناسه عملیات از تکرار شارژ/ثبت جلوگیری می‌کند؛ قفل مشترک SQL، RowVersion و دفتر افزودنی با trigger جلوی خرج مضاعف، مانده منفی و بازنویسی دفتر را می‌گیرند. داده مالی آفلاین کش یا صف نمی‌شود. نمونه نرخ و داده آزمایشی فقط در دیتابیس‌های جدا به کار رفتند؛ نرخ/درصد/مانده واقعی ساخته نشد.

مسیر استفاده: مدیر از `/admin/users` وارد «کیف مشتری ویژه» شود، مشتری فعال را ویژه و با دلیل شارژ کند. کارمزد و فعال‌بودن خرید در `/admin/gold/settings` است. مشتری از `/gold` خرید می‌کند؛ مدیر/اپراتور از `/operator/gold` بررسی می‌کند.

### ۲) فایل‌های تغییر‌یافته (مسیر + نوع تغییر)
- `src/SadGallery.Domain/Trading/GoldPurchaseMath.cs` و `GoldWalletBalance.cs`: فرمول مستقل gold-buy-v1، گردکردن تومان، دقت سوت، سقف و قواعد مانده/رزرو/خرید.
- `src/SadGallery.Application/Trading/`: قراردادهای پنل/مخزن، قواعد نرخ قابل معامله، پارس/نمایش فارسی و پیام‌های وضعیت/خطا.
- `src/SadGallery.Infrastructure/Trading/GoldTradingStore.cs`: کوئری مالکیت/صفحه‌بندی، تراکنش و قفل SQL، کنترل نقش واقعی، snapshot، idempotency و ثبت دفتر.
- `src/SadGallery.Infrastructure/Persistence/Entities/GoldTrading.cs`، `GoldTradingModel.cs`، `SadGalleryDbContext.cs`، `AdministrationAudit.cs`: جداول/precision/constraint/FK/index، محافظ دفتر/snapshot و Audit اتمیک؛ `DependencyInjection.cs` و عنوان‌های Audit در Application به‌روز شدند.
- `src/SadGallery.Infrastructure/Persistence/Migrations/20261018214550_GoldTradingWallets*` و snapshot، `database/scripts/GoldTradingWallets.sql` و `.Down.sql`: ساخت/بازگشت چهار جدول و trigger.
- `src/SadGallery.Web/Controllers/GoldController.cs`، `Models/GoldForms.cs`، `Views/Gold/`: پنل مشتری، تنظیمات/شارژ مدیر، صف و رسید/بررسی؛ اعتبارسنجی، CSRF، نقش و خطای فارسی۴۰۰/۴۰۹/۵۰۳.
- `Views/Shared/_Layout.cshtml`، `Views/Member/Index.cshtml`، `Areas/Admin/Views/Management/Users.cshtml`، `wwwroot/css/site.css`، `Program.cs` و `Security/RateLimitPolicies.cs`: ورودی‌های منو/پنل، ظاهر همسان و محدودیت نرخ مالی.
- `tests/SadGallery.Tests.Unit/Trading/`، `tests/SadGallery.Tests.Integration/Trading/` و `tests/pwa/service-worker.test.cjs`: آزمون محاسبات، مانده، مجوز، SQL، هم‌زمانی، شکست میانه ثبت، بازگشت و عدم کش مالی.

### ۳) تست‌های اجراشده و خروجی واقعی
- `dotnet restore` و `dotnet build --no-restore -warnaserror -m:1 --disable-build-servers`: موفق، صفر هشدار و صفر خطا در مخزن. محیط SDK10.0.401 و SQL Server2022 Developer است.
- `dotnet test --no-build` با تزریق امن `SADGALLERY_TEST_SQL`: **633 موفق، صفر شکست، صفر Skip، 28.369 ثانیه** در اجرای نهایی. 32 تست به 601 تست قبلی اضافه شد؛ تست‌های SQL دیتابیس یکتا را در پایان حذف می‌کنند.
- SQL واقعی: شارژ تکراری و هم‌زمان، رزرو دو خرید با اعتبار کافی فقط برای یکی، رقابت تأیید/لغو، رد و لغو، حفظ قیمت درخواست با تغییر کارمزد، ابطال پیش‌نمایش منقضی، رد Fixture/Disabled/ریال/جهش/نرخ نامعتبر/آینده/کهنه، مالکیت دو کاربر و ثبت فقط یک دفتر آزموده شد.
- trigger شکست موقت روی درج خرید: خطای SQL باعث rollback تومان، طلا، رزرو و وضعیت شد؛ دفتر ایجاد نشد و همان درخواست بعد از رفع خطا موفق ثبت شد.
- Identity و کوکی واقعی: نقش مشتری/اپراتور برای عملیات مدیر رد شد؛ POST بدون CSRF و شارژ بدون شناسه عملیات۴۰۰؛ بررسی بدون انتخاب نتیجه۴۰۰ با حفظ متن؛ نسخه قدیمی۴۰۹؛ دفتر در EF و UPDATE مستقیم SQL تغییرناپذیر بود؛ پاسخ کیف no-store است.
- `node --test tests/pwa/service-worker.test.cjs`: **7 موفق**؛ مسیرهای کیف/سفارش/صف/تنظیمات و POST مالی هیچ کش خصوصی نمی‌سازند و fallback فقط صفحه مستقل عمومی است.
- `dotnet ef migrations has-pending-model-changes`: بدون اختلاف مدل. Up/Down EF روی SQL واقعی با حفظ داده قدیمی آزموده شد؛ SQL تولیدشده نیز با sqlcmd واقعی، Up دوبار → داده مالی/constraint/trigger → Down → حفظ Identity/تیکت/تنظیمات قبلی → Up دوبار اجرا و موفق شد.
- Chromium154/Playwright با Identity واقعی، provider شبکه Disabled و نرخ صریح مصنوعی در DB جدا: شارژ طلا، ورود وزن فارسی، پیش‌نمایش/ثبت/تأیید، رسید، اعداد مانده، لغو، حفظ توضیح انتخاب خالی، حفظ درصد در تعارض تنظیمات، escape متن بررسی و جلوگیری از ارسال/صف آفلاین موفق بودند. عرض‌های **320،390،1440** بدون سرریز صفحه؛ فیلتر موبایل جمع‌شده، «همه وضعیت‌ها» شامل دو خرید تأیید/لغو، صفر خطای JavaScript. تصاویر توسعه در artifacts ابزار خارج Git نگهداری شدند.
- Migration روی SadGallery_Dev اعمال شد؛ هیچ حساب ویژه یا مبلغ واقعی در آن seed نشد. اجرای جدید `/health/ready`، `/pwa/version`، `/pwa/install` و `/service-worker.js` همگی۲۰۰ شدند؛ مهمان `/gold` به ورود هدایت شد و no-store داشت. اجرای عمومی جدید، منبع نرخ واقعی و گوشی فیزیکی در این وظیفه آزموده نشدند.

### ۴) مشکلات باقی‌مانده / مسدودکننده‌ها
کد و آزمون مسدودکننده ندارند. فعال‌سازی عملیاتی نیازمند تعیین کارمزد مدیر، مشتری واقعی و شارژ با مرجع واقعی، منبع آنلاین معتبر و کنترل‌های قبلی استقرار است. توسعه فعلی Fixture دارد که عمداً برای خرید پذیرفته نمی‌شود. نسخه اول فروش/برداشت/بدهکاری/درگاه/تحویل فیزیکی و اصلاح دفتر از UI ندارد؛ درخواست در انتظار تا بررسی یا لغو رزرو می‌ماند. آزمون ظرفیت Production، retention و گوشی فیزیکی باقی‌اند؛ نصب نهایی PWA و فاز۸ با این کار تکمیل اعلام نشده‌اند.

### ۵) ریسک امنیتی (اجراشده، باقی‌مانده، نیازمند تصمیم مالک)
اجراشده: مجوز و مالکیت سرور، بازخوانی نقش فعال زیر قفل مشترک با مدیریت دسترسی، CSRF، سقف درخواست، اعداد decimal و واحد صریح، نرخ معتبر، snapshot/نسخه فرمول، اتمیک‌بودن و idempotency، RowVersion، constraint و دفتر فقط افزودنی، Audit بدون کپی متن/مبالغ و no-store/عدم صف آفلاین. مانده کل و نرخ/اجزای مبلغ سفارش از مرورگر پذیرفته نمی‌شوند؛ شارژ مدیر فقط افزایش دارای دلیل است.
باقی‌مانده: کنترل DBA/مجوزهای DB، backup/بازیابی عملیاتی، TLS/دامنه و MFA/OTP طبق برنامه قبلی. sms.ir غیرفعال می‌ماند. قفل سراسری معاملات باید پیش از بار زیاد سنجیده شود. پاک‌سازی نرخ‌های مورد استفاده سفارش با FK محدود است و به سیاست retention هماهنگ نیاز دارد.

### ۶) Migration لازم است؟
بله: **20261018214550_GoldTradingWallets**. چهار جدول `GoldWallets`، `GoldTradingSettings`، `GoldOrders`، `GoldLedger` و trigger `TR_GoldLedger_AppendOnly`. جداول قبلی بازنویسی نمی‌شوند؛ تنظیم اولیه خرید=false و کارمزد=null است. SQL Up ایدمپوتنت و Down هر دو آزموده شدند.
برای بازگشت معمول، خرید را در پنل خاموش و داده را حفظ کنید. Down فقط پس از توقف، تعیین تکلیف رزروها و backup/export امن چهار جدول؛ Down اطلاعات مالی را حذف می‌کند و بازیابی به restore نیاز دارد. برای انتشار PWA برنامه پس از deploy یکپارچه restart شود.

### ۷) به‌روزرسانی مستندات
`AGENTS.md`، `PROJECT_STATUS.md`، `ROADMAP.md`، `CHANGELOG.md`، `OPEN_QUESTIONS.md`، `DATA_MODEL.md`، `ARCHITECTURE.md`، `API.md`، `SECURITY.md`، `DEPLOYMENT.md`، `TESTING.md`، `KNOWN_LIMITATIONS.md`، `BUGS.md`، `DECISIONS/ADR-0022-vip-gold-wallet.md` و این گزارش. BUG-015 برای فیلتر همه وضعیت‌ها با رگرسیون رفع شد. قواعد جدید پذیرش با اجرای واقعی علامت خوردند.

### ۸) قدم بعدی دقیق
مدیر در `/admin/gold/settings` کارمزد موردنظر را ثبت کند؛ فعال‌سازی خرید واقعی پس از در دسترس‌بودن نرخ آنلاین معتبر Tgn انجام شود.
