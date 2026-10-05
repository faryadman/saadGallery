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

### Verified (خروجی واقعی، نه ادعا)
- `scripts/dev-setup.sh` در سندباکس با موفقیت **.NET SDK 10.0.401** (Host 10.0.12، ~۶۲۲MB) را در `/opt/dotnet` نصب کرد.
- قالب `dotnet new mvc` در دسترس است.
- restore موفق `Microsoft.EntityFrameworkCore.SqlServer 10.0.12` از NuGet در ۳.۴۶ ثانیه ⇒ فاز ۱ در سندباکس قابل Build و Unit Test است.
- SQL Server/Docker در سندباکس موجود نیست ⇒ تست‌های Integration با Trait `RequiresSqlServer` و Skip صریح.

### Notes
- نسخه‌های بررسی‌شده (2026-10-05): .NET 10 LTS (آخرین Patch 10.0.12، EOL 2028-11-14)، Bootstrap 5.3.8.
- هیچ کد اجرایی، Migration یا Secret در این مرحله ایجاد نشده است؛ فاز صفر صرفاً طراحی و مستندسازی است.
- منتظر پاسخ `docs/OPEN_QUESTIONS.md` (به‌ویژه `Q-API-1`) و تأیید مالک برای شروع فاز ۱.
