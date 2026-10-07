# SadGallery — ویترین آنلاین طلا و جواهر صادگالری

سامانه فارسی و راست‌چین نمایش نرخ روز طلا، مسکوکات، ارز و رمزارز، تحلیل حباب، ویترین محصولات و تیکت پشتیبانی/استعلام قیمت، همراه با پنل مدیریت و نصب‌پذیری روی موبایل (PWA).

- **نام فنی:** `SadGallery`
- **پشته:** ASP.NET Core 10 (MVC) · EF Core 10 · SQL Server · ASP.NET Core Identity · Bootstrap 5.3 RTL · PWA
- **زبان رابط کاربری:** فارسی (RTL)
- **وضعیت فعلی:** فاز صفر (بررسی و مستندسازی) — تکمیل‌شده، در انتظار تأیید مالک. کد برنامه هنوز نوشته نشده است.

---

## مخزن رسمی

```
git@github.com / https://github.com/faryadman/saadGallery.git   (شاخه اصلی: main)
```

> ⚠️ اگر ریموت در محیط شما وجود ندارد، با این یک خط بازسازی می‌شود (فایل `.git/config` در snapshot محیط ایجنت ذخیره نمی‌شود، به همین دلیل آدرس در همین فایل ثبت شده است):
> ```bash
> git init -b main && git remote add origin https://github.com/faryadman/saadGallery.git
> ```
> نکته نام‌گذاری: نام مخزن روی GitHub `saadGallery` است و نام فنی پروژه در کد `SadGallery`. تفاوت عمدی و بی‌اثر بر کد است (به `docs/OPEN_QUESTIONS.md` → `Q-REPO-1` مراجعه کنید).

## این مخزن در این لحظه چه چیزی دارد؟

فاز صفر پروژه، یعنی «شناخت و طرح»: مستندات دائمی، معماری، مدل داده، مدل تهدید، Roadmap با معیار پذیرش، و فهرست سؤالات باز. هیچ کد اجرایی، هیچ Migration و هیچ کلید واقعی در مخزن نیست.

```
.
├── AGENTS.md                 # دستورالعمل دائمی برای هر ایجنت/توسعه‌دهنده (اول این را بخوانید)
├── README.md                 # همین فایل
├── .gitignore / .editorconfig
├── docs/
│   ├── PROJECT_STATUS.md     # وضعیت لحظه‌ای، مسدودکننده‌ها، قدم بعدی
│   ├── ROADMAP.md            # فاز ۰ تا ۸ + معیار پذیرش قابل آزمون
│   ├── ARCHITECTURE.md       # لایه‌ها، جریان‌ها، الگوهای Provider/Cache/Job
│   ├── SECURITY.md           # مدل تهدید، کنترل‌ها، رمزنگاری و مدیریت کلید
│   ├── DATA_MODEL.md         # موجودیت‌ها، ستون‌ها، Index/Constraint، برنامه Migration
│   ├── API.md               # قرارداد دادهٔ API نرخ + نمونه JSON موردنیاز + API داخلی
│   ├── DEPLOYMENT.md         # استقرار، Secret، HTTPS، Backup/Restore، پایش
│   ├── TESTING.md            # استراتژی و دستورهای تست، محدودیت‌های سندباکس
│   ├── BUGS.md               # ثبت باگ‌ها (الگو + وضعیت)
│   ├── CHANGELOG.md          # تاریخ تغییرات
│   ├── KNOWN_LIMITATIONS.md  # محدودیت‌های صادقانه و آگاهانه
│   ├── OPEN_QUESTIONS.md     # سؤالات باز از مالک پروژه (پاسخ = ورودی طراحی)
│   ├── PHASE0_REPORT.md      # گزارش کامل فاز صفر و قدم بعدی
│   └── DECISIONS/            # ADR-0001..0009
└── scripts/
    ├── dev-setup.sh          # آماده‌سازی SDK در سندباکس (غیرماندگار)
    └── test.sh               # اجرای تست با تنظیمات استاندارد
```

## شروع کار (پس از فاز یک)

**پیش‌نیاز روی ماشین خودتان:** .NET SDK 10.0.1xx و یک SQL Server (Express / LocalDB / Developer).
اگر دیتابیس هنوز ساخته نشده، هیچ نگران نباشید: دستور `database update` **خودش دیتابیس را می‌سازد**.
راهنمای کامل و گام‌به‌گام: `docs/DEPLOYMENT.md` §۴.۵.

```bash
# ۱) ساخت و تست (بدون نیاز به دیتابیس)
dotnet restore
dotnet build -warnaserror
dotnet test

# ۲) ساخت دیتابیس + اعمال مهاجرت (فقط یک‌بار)
#    ویندوز PowerShell:
#      dotnet tool install --global dotnet-ef --version 10.*
#      $env:SADGALLERY_CONNECTION = "Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True"
#      dotnet ef database update --project src/SadGallery.Infrastructure --startup-project src/SadGallery.Web
#    لینوکس/macOS/Git Bash:
#      SADGALLERY_CONNECTION="Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True" bash scripts/ef.sh database update

# ۳) اجرا — توجه: خودِ برنامه نام متغیر دیگری می‌خواهد (تله رایج!)
#    PowerShell:  $env:ConnectionStrings__SadGallery = "Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True"
#    یا با User Secrets:
#      cd src/SadGallery.Web
#      dotnet user-secrets set "ConnectionStrings:SadGallery" "<connection-string>"
#      cd ../..
dotnet run --project src/SadGallery.Web -- --seed    # ساخت سه نقش (ایدِمپوتنت)
dotnet run --project src/SadGallery.Web              # اجرا
```

> در سندباکس ایجنت، SDK و SQL Server در دسترس نیستند؛ برای جزئیات و روش کار به `AGENTS.md` بخش ۶ و `docs/TESTING.md` مراجعه کنید.

## امنیت در یک نگاه

- رمز عبور با ASP.NET Core Identity (هرگز با کلید اختصاصی پروژه رمزنگاری نمی‌شود).
- OTP با مولد تصادفی امن، هش‌شده در پایگاه داده، محدودیت تلاش/ارسال مجدد/نرخ.
- داده حساس قابل بازیابی: AES-256-GCM با کلید مشتق‌شده از عبارت محرمانه (PBKDF2 + HKDF، نسخه‌بندی‌شده، قابلیت چرخش کلید).
- کنترل دسترسی تمام‌وقت سمت سرور با Policy؛ دسترسی به تیکت بر اساس مالکیت، نه GUID.
- آپلود تصویر با اعتبارسنجی واقعی محتوا، نام تصادفی، جداسازی مسیر عمومی/خصوصی.
- هیچ Secret در Git نیست: `docs/SECURITY.md` و `docs/DECISIONS/ADR-0006`.

## مستندات کلیدی برای شروع مطالعه

1. `docs/PHASE0_REPORT.md` — چه بررسی شد، چه تصمیمی گرفته شد، چه چیزی از مالک لازم است.
2. `docs/ARCHITECTURE.md` — نقشه فنی.
3. `docs/ROADMAP.md` — چه زمانی چه چیزی تحویل داده می‌شود و چگونه پذیرفته می‌شود.
4. `docs/DECISIONS/` — چرا این‌طور طراحی شده (برای ادامه کار توسط ایجنت بعدی).
