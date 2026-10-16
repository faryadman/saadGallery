## گزارش وظیفه: ویرایش متن اعلان قطع و وصل اینترنت توسط مدیر
وضعیت: انجام‌شده — 2026-10-16؛ بازبینی مالک باقی است.

### ۱) چه کاری انجام شد
صفحه `/admin/connection-notice` و لینک آن در پنل و منوی مدیر ساخته شد؛ عنوان/توضیح قطع و بازگشت اتصال جداگانه ذخیره می‌شوند. UI از کارت و فرم‌های فارسی فعلی استفاده می‌کند. متن ساده با سقف 80/200، CSRF، Admin فعال، کنترل مجوز واقعی Store، RowVersion، خطای داخل فرم با حفظ متن و ثبت تغییرات Actor/UTC/نام فیلد دارد. متن‌ها در HTML صفحه جدید encoded هستند و هنگام offline شبکه نمی‌خواهند. کش عمومی 30 ثانیه و fallback پیش‌فرض با deadline دوثانیه‌ای از خرابی صفحات دیگر در اختلال DB جلوگیری می‌کند؛ صفحه مدیر خطا را صریح نشان می‌دهد.

### ۲) فایل‌های تغییر‌یافته (مسیر + نوع تغییر)
- `src/SadGallery.Application/Settings/ConnectionNoticeService.cs`: قرارداد، پیش‌فرض، سقف/اعتبارسنجی و trim متن.
- `src/SadGallery.Infrastructure/Settings/ConnectionNoticeStore.cs`: خواندن/نوشتن SQL، نقش/فعال‌بودن مدیر، نسخه هم‌زمانی و رویداد اتمیک.
- `src/SadGallery.Infrastructure/Persistence/Entities/ConnectionNoticeSetting.cs`، `ConnectionNoticeModel.cs`، `SadGalleryDbContext.cs`: singleton و ثبت ویرایش‌ها، FK/check/seed.
- `src/SadGallery.Infrastructure/Persistence/Migrations/20261016003908_ConnectionNoticeSettings.cs`، Designer و Snapshot؛ `database/scripts/ConnectionNoticeSettings.sql` و `ConnectionNoticeSettingsDown.sql`.
- `src/SadGallery.Infrastructure/DependencyInjection.cs` و `src/SadGallery.Web/Program.cs`: DI سرویس‌ها و کش.
- `src/SadGallery.Web/Areas/Admin/Controllers/ConnectionNoticeController.cs`، `Views/ConnectionNotice/Index.cshtml`، `Views/Dashboard/Index.cshtml` و `Models/ConnectionNoticeForm.cs`: پنل، خطای فارسی، نمایش آخرین ذخیره و مسیر دسترسی.
- `src/SadGallery.Web/Services/ConnectionNoticeDisplay.cs`، `ViewComponents/ConnectionNoticeViewComponent.cs`، `Views/Shared/Components/ConnectionNotice/Default.cshtml` و `_Layout.cshtml`: خواندن عمومی، کش/fallback و text/attribute encoded.
- `src/SadGallery.Web/wwwroot/js/offline-status.js` و `css/site.css`: استفاده از متن دریافت‌شده و اعلان بلند با اسکرول و close قابل دسترس.
- `tests/SadGallery.Tests.Unit/Settings/ConnectionNoticeTests.cs`، `tests/SadGallery.Tests.Integration/Settings/ConnectionNoticeTests.cs` و `ConnectionNoticeSqlTests.cs`: اعتبارسنجی، Policy/CSRF، SQL/هم‌زمانی/رویداد/XSS و fallback.
- مستندات بخش ۷.

### ۳) تست‌های اجراشده و خروجی واقعی (دستور + خلاصه نتیجه)
- `bash /workspace/.sadgallery-tools/install.sh`: restore/build با warnaserror؛ 0 هشدار و 0 خطا.
- `dotnet test --no-build` با اتصال امن SQL آزمایشی: 567 موفق، 0 شکست، 0 Skip؛ شامل 8 مورد تازه، بدون حذف/ضعیف‌کردن تست قبلی.
- تست SQL: دو Admin واقعی، Operator و Customer با کوکی Identity؛ Policy/نوشتن با CSRF، ذخیره چهار مقدار، RowVersion تازه، عدم دسترسی Store برای اپراتور، plain HTML encoded، conflict 409/نسخه تازه/متن حفظ‌شده، invalid 400، سقف طول، ذخیره بدون تغییر، رویداد Actor/زمان/نام فیلد و NoStore صفحه مدیر.
- تست مستقل timeout: Store کند با CancellationToken پس از deadline دوثانیه‌ای قطع و صفحه عمومی با متن پیش‌فرض 200 رندر شد، بدون جزئیات Exception.
- Chromium/Playwright روی Kestrel و SQL واقعی: ذخیره هر چهار متن توسط مدیر، تازه‌کردن صفحه مهمان و نمایش همان متن هنگام offline/reconnect بدون درخواست شبکه، جلوگیری از دسترسی مشتری، فرم کامل در 400/409 و متن حفظ‌شده، XSS فقط متن، اعلان با طول 80/200 در 320×360 قابل اسکرول/بستن و فرم‌های 390/1440 بدون سرریز، موفق. متن آزمایشی Dev در finally به مقدار قبلی بازگردانده شد.
- `dotnet ef database update`: Migration روی Dev، SQL تست و DB جدا rollback موفق. Down تولیدشده روی تنظیم و رویداد آزمایشی اجرا و فقط دو جدول تازه حذف شدند؛ تیکت/پیام موجود ماند. Up idempotent دو بار، تک‌ردیف پیش‌فرض و یک History، موفق.
- `python3 scripts/accessibility-check.py`: 24 جفت‌رنگ موجود و قواعد هدف لمسی موفق؛ ممیزی کامل اسکرین‌ریدر/Safari فیزیکی انجام نشده است.
- اجرای اولیه سه تست Policy POST به دلیل توکن CSRF نداشتن، 400 گرفتند؛ fixture با توکن معتبر فرم عمومی اصلاح شد تا همان انتظار 401/403 برای مجوز بدون تضعیف assertion سنجیده شود. اجرای نهایی 567 موفق شد.

### ۴) مشکلات باقی‌مانده / مسدودکننده‌ها
مسدودکننده‌ای برای درخواست فعلی باقی نیست. متن جدید در صفحه تازه دریافت می‌شود؛ کش دیگر instanceها یا رقابت خواندن/ذخیره می‌تواند تا 30 ثانیه انتشار را عقب بیندازد. این کار تکمیل کل فاز ۶ یا ذخیره آفلاین اولیه نیست. Production و DB مالک تغییر نکردند؛ Migration محیط هدف لازم است.

### ۵) ریسک امنیتی (اجراشده، باقی‌مانده، نیازمند تصمیم مالک)
Admin فعال و CSRF اجباری، نقش واقعی Store دوباره بررسی، RowVersion و نوشتن اتمیک با audit محدود؛ HTML/attribute encoding و textContent در مرورگر. متن‌ها عمومی‌اند؛ راهنمای پنل منع نوشتن Secret دارد. در لاگ جدید فقط نوع Exception و در رویداد فقط نام فیلد است؛ متن یا کلید/رمز در لاگ ثبت نمی‌شود. داده خصوصی به LocalStorage/Cache مرورگر اضافه نشد. پیش‌نویس/فایل، برچسب محافظه‌کارانه نرخ و سایر Policyها حفظ شدند. Audit عمومی/IP/Correlation و نسخه‌بندی کامل متن جزو فاز ۶ آینده‌اند.

### ۶) Migration لازم است؟
بله: `20261016003908_ConnectionNoticeSettings`، فقط افزودن دو جدول و seed پیش‌فرض. راه رفت: اتصال امن و Backup، سپس `bash scripts/ef.sh database update` یا SQL بازبینی‌شده. راه برگشت همراه کد قبلی: `bash scripts/ef.sh database update TicketMessageReads` یا SQL Down. تنظیم سفارشی/رویدادها حذف می‌شوند؛ پیش از rollback backup لازم است. تیکت/پیام/عکس/کاربر باقی‌اند. روی DB جدا رفت/برگشت/رفت و اجرای مجدد Up آزموده شد.

### ۷) به‌روزرسانی مستندات
`PROJECT_STATUS.md`، `ROADMAP.md`، `CHANGELOG.md`، `DATA_MODEL.md`، `KNOWN_LIMITATIONS.md`، `DEPLOYMENT.md`، `DECISIONS/ADR-0019-ConnectionNoticeSettings.md` و این گزارش. وضعیت فاز ۶ فقط برای این بخش محدود به درحال‌پیشرفت اصلاح شد؛ سایر معیارها تیک نخوردند.

### ۸) قدم بعدی دقیق
ورود مالک با نقش Admin و بازکردن «متن اعلان اتصال» در پنل مدیر برای بازبینی و واردکردن متن دلخواه؛ در سرور مالک ابتدا Migration طبق بخش ۶ اعمال شود.
