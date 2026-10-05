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
