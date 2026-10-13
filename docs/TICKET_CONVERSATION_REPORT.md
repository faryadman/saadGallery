## گزارش وظیفه: رفع خطای فرم، بهبود گفت‌وگو و نشان پیام جدید
وضعیت: انجام‌شده — 2026-10-14؛ بازبینی مالک باقی است.

### ۱) چه کاری انجام شد
پاسخ اپراتور بدون تغییر وضعیت ثبت می‌شود و وضعیت حفظ می‌شود. خطای فرم خالی، اطلاعات نامعتبر و تعارض با پیام فارسی داخل گفت‌وگوی کامل می‌آید؛ متن/یادداشت/ارجاع حفظ و RowVersion تازه استفاده می‌شود. خطای عملیات جدول نیز در صف نمایش داده می‌شود. چیدمان با سربرگ، خلاصه زمان‌ها، پیام‌های جدا و کارت پاسخ/یادداشت/تاریخچه، مطابق پالت قبلی و RTL اصلاح شد.

نشان منو، دکمه منوی موبایل و ردیف جدول تعداد پیام عمومی خوانده‌نشده را می‌دهد. در تب فعال آنلاین، شمارش هر 30 ثانیه تازه می‌شود. پیام تازه در گفت‌وگو با دکمه نمایش می‌آید و صفحه خودکار reload نمی‌شود؛ متن و عکس در حال انتخاب باقی می‌مانند. باز کردن گفت‌وگو فقط پیام‌های snapshot نمایش‌داده‌شده را برای همان کاربر/نقش خوانده می‌کند. پیام خود کاربر و یادداشت داخلی شمارش نمی‌شوند.

### ۲) فایل‌های تغییر‌یافته (مسیر + نوع تغییر)
- `src/SadGallery.Application/Tickets/TicketContracts.cs`: شناسه پیام و تعداد خوانده‌نشده در خلاصه.
- `src/SadGallery.Application/Tickets/TicketInboxService.cs`: قرارداد شمارش و ثبت خواندن دقیق snapshot.
- `src/SadGallery.Infrastructure/Tickets/TicketStore.cs`: پاسخ مستقل از وضعیت و تعداد خوانده‌نشده در صفحه 25تایی.
- `src/SadGallery.Infrastructure/Tickets/TicketInboxStore.cs`: query مالکیت، شمارش و INSERT پارامتری با قفل باز کردن هم‌زمان.
- `src/SadGallery.Infrastructure/DependencyInjection.cs`: ثبت سرویس‌های scoped.
- `src/SadGallery.Infrastructure/Persistence/Entities/SupportTicket.cs` و `TicketModel.cs`: رسید به تفکیک کاربر/نقش/پیام.
- `src/SadGallery.Infrastructure/Persistence/Migrations/20261013222157_TicketMessageReads.cs`، Designer و Snapshot: Migration افزودنی.
- `database/scripts/TicketMessageReads.sql` و `TicketMessageReadsDown.sql`: SQL اجرا/بازگشت.
- `src/SadGallery.Web/Controllers/TicketsController.cs`: endpoint شمارش، خواندن GET، خطای پاسخ داخل View، NoStore.
- `src/SadGallery.Web/Areas/Operator/Controllers/TicketsController.cs`: خطاهای فرم/جدول داخل View و خواندن GET، NoStore.
- `src/SadGallery.Web/Views/Shared/_TicketConversation.cshtml`، `_TicketTable.cshtml`، `_Layout.cshtml`: چیدمان و جایگاه نشان.
- `src/SadGallery.Web/Views/Tickets/Details.cshtml`، `Areas/Operator/Views/Tickets/Details.cshtml` و `Index.cshtml`: فرم/خطای کامل و متن حفظ‌شده.
- `src/SadGallery.Web/ViewComponents/TicketBadgeViewComponent.cs` و `Views/Shared/Components/TicketBadge/Default.cshtml`: نشان SSR با شمارش مشترک در هر request و خطای کنترل‌شده.
- `src/SadGallery.Web/wwwroot/js/ticket-notifications.js` و `css/site.css`: بررسی دوره‌ای، نشان/هشدار و ظاهر هماهنگ.
- `tests/SadGallery.Tests.Integration/Tickets/TicketInboxSqlTests.cs`: دو تست SQL واقعی با Identity/CSRF، فرم و محرمانگی/هم‌زمانی خواندن.
- مستندات فهرست بخش ۷.

### ۳) تست‌های اجراشده و خروجی واقعی (دستور + خلاصه نتیجه)
- `bash /workspace/.sadgallery-tools/install.sh`: restore و build با `-warnaserror`؛ 0 Warning / 0 Error.
- `dotnet test --no-build` با تزریق امن اتصال SQL آزمایشی: **559 موفق، 0 شکست، 0 Skip**. تست‌های قبلی حذف/تضعیف نشدند؛ قواعد 25 زوج وضعیت و HTTP 400/409 پابرجا هستند.
- `TicketInboxSqlTests`: پاسخ بدون وضعیت و حفظ New/زمان پاسخ اول؛ فرم خالی 400 با HTML کامل؛ متن نامعتبر/XSS encoded، یادداشت و پیش‌نویس حفظ‌شده؛ stale 409/نسخه تازه/اصلاح دوباره؛ اطلاعات malformed؛ اعلان مشتری/اپراتور مستقل؛ snapshot قدیمی و پیام تازه؛ خواندن موازی idempotent؛ IDOR خواندن/شمارش؛ نویسنده/یادداشت خارج از شمارش؛ نقش‌های مشتری/اپراتور در یک حساب مستقل؛ مهمان/مشتری بدون مجوز صف رد؛ صفحات و JSON بدون cache.
- مرورگر Chromium/Playwright روی Kestrel واقعی با دو کاربر آزمایشی: پاسخ بدون وضعیت، خالی 400 و stale 409 داخل صفحه؛ نشان منو/ردیف هر دو سمت با گذشت زمان 30 ثانیه؛ باز کردن و صفرشدن نشان؛ هشدار پیام جدید همراه حفظ متن و فایل انتخاب‌شده؛ یادداشت فقط اپراتور؛ دسکتاپ 1440 و موبایل 390 بدون سرریز و خطای JavaScript؛ نشان بیرون منوی بسته موبایل.
- `dotnet ef database update`: Migration روی Dev و DB مجزای `SadGallery_InboxRollback` موفق. SQL تولیدشده Down با رسید/تیکت/پیام آزمایشی اجرا شد: فقط رسید حذف و پیام/تیکت باقی ماند؛ SQL idempotent Up دو بار بدون تکرار History/خطا اجرا شد.
- `python3 scripts/accessibility-check.py`: 23 جفت‌رنگ موجود و قواعد هدف لمسی موفق؛ این ممیزی کامل اسکرین‌ریدر نیست.
- `GET /health/ready`: HTTP 200 روی سرویس تازه.
- در اجرای اولیه، analyzer استفاده از Assert.Equal برای اندازهٔ مجموعه را رد کرد؛ با Assert.Empty اصلاح شد. یک اجرای پیش از build شامل Migration تازه، PendingModelChanges را آشکار کرد؛ build کامل با Migration و اجرای نهایی تمام تست‌ها موفق شدند.

### ۴) مشکلات باقی‌مانده / مسدودکننده‌ها
برای این درخواست مسدودکننده‌ای باقی نیست. بازبینی بصری مالک و نصب Migration روی سرور مالک جداست؛ Production یا داده واقعی تغییر نکرد. اعلام پیام با polling در صفحه فعال است؛ Push/پیامک نیست. بدون JavaScript شمارش با پیمایش بعدی تازه می‌شود.

### ۵) ریسک امنیتی (اجراشده، باقی‌مانده، نیازمند تصمیم مالک)
Policy حساب فعال، نقش Operator/Admin، مالکیت در query و INSERT، CSRF ارسال، limiter، RowVersion، Razor Encoding و جداسازی یادداشت/عکس خصوصی حفظ شدند. endpoint فقط تعداد و شناسه‌های مجاز خواسته‌شده (حداکثر 25) را می‌دهد؛ بدنه/نام/یادداشت ندارد. صفحات و شمارش NoStore؛ لاگ جدید فقط نوع Exception، بدون متن/راز. رسیدها با تعداد مشاهده‌کنندگان رشد می‌کنند؛ سیاست Retention در سخت‌سازی لازم است. هیچ SMS ارسال نشد و sms.ir غیرفعال/اطلاعات الگو خالی ماند.

### ۶) Migration لازم است؟
بله: `20261013222157_TicketMessageReads`. افزودنی، بدون تغییر تیکت‌های قبلی. پیش از استقرار مالک Backup بگیرد و Migration را با اتصال Secret خودش اجرا کند. راه بازگشت: کد قبل همراه `dotnet ef database update TicketPhotos` یا SQL Down بازبینی‌شده. Down فقط رسید خواندن را حذف می‌کند؛ پیام/عکس/تیکت باقی‌اند و با ارتقای دوباره پیام‌های طرف مقابل از نو خوانده‌نشده می‌شوند. SQL رفت/برگشت و اجرای مجدد روی DB جدا آزموده شد.

### ۷) به‌روزرسانی مستندات
`PROJECT_STATUS.md`، `ROADMAP.md` (معیارهای پذیرش پیش از اجرا و سپس تیک موفق)، `CHANGELOG.md`، `BUGS.md` (BUG-012)، `KNOWN_LIMITATIONS.md`، `DATA_MODEL.md`، `API.md`، `DECISIONS/ADR-0016` (اصلاح شرط پاسخ)، `DECISIONS/ADR-0018-TicketUnread.md` و این گزارش.

### ۸) قدم بعدی دقیق
بازبینی چیدمان گفت‌وگو و نشان پیام توسط مالک؛ سپس اعمال Migration در محیط مالک طبق روند استقرار و Backup موجود.
