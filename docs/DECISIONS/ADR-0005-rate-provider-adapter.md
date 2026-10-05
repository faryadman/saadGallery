# ADR-0005 — الگوی Provider/Adapter برای منابع نرخ

- **وضعیت:** Accepted
- **تاریخ:** 2026-10-05
- **وابستگی:** منتظر `Q-API-1` برای جزئیات نگاشت

## زمینه
الزام: «از ساختار Provider یا Adapter استفاده کن تا منابع مختلف نرخ مستقل باشند» + مدیریت جایگزین، Cache، تاریخچه، وضعیت سلامت، Timeout و Rate Limit. هنوز شکل JSON منبع مشخص نیست.

## تصمیم
```csharp
public interface IRateProvider
{
    string Key { get; }                              // یکتا و پایدار
    RateProviderCapabilities Capabilities { get; }    // دارایی‌های پشتیبانی‌شده، ارز/رمزارز، پشتیبانی تاریخچه
    Task<ProviderFetchResult> FetchAsync(RateRequest request, CancellationToken ct);
}
```
- پیاده‌سازی‌ها در `Infrastructure/RateProviders/<ProviderKey>/` هرکدام شامل: `Client`، `ResponseDto`، `Mapper` (قابل تست)، `Options`.
- انتخاب منابع از جدول `PriceProviders` (فعال/غیرفعال/اولویت/MinInterval) خوانده می‌شود؛ **هیچ منبعی در کد هاردکد نمی‌شود** نقش «اصلی» را ندارد.
- `FetchedAtUtc` در لایه Application مهر می‌شود؛ Provider فقط `QuotedAtUtc` منبع را می‌دهد (تفکیک زمان اعلام و زمان دریافت — الزام سند اصلی).
- Register در DI با `AddHttpClient<IRateProvider, XProvider>()` برای مدیریت صحیح Socket/DNS.
- استفاده از `Microsoft.Extensions.Http.Resilience`: Timeout، Retry محدود (فقط خطاهای گذرا)، Circuit Breaker.

## دلیل
- افزودن/حذف منبع بدون تغییر در Domain یا Web ⇒ استقلال واقعی منابع (الزام سند اصلی).
- نگاشت قابل تست با Fixture، ریسک تفسیر نادرست واحد/فیلد را از «کد تولید» به «تست» منتقل می‌کند.

## قواعد الزامی هر Provider
1. اعتبارسنجی پاسخ با DTO + بررسی بازه منطقی مقادیر (`MinReasonableValue`/`MaxReasonableValue`).
2. کلید دارایی ناشناس ⇒ رد رکورد + لاگ `Warning` (بدون حدس).
3. `item.Unit` همیشه صریح؛ تبدیل ریال⇄تومان فقط با ضریب صریح و ثبت رخداد.
4. خطای شبکه/Timeout/JSON نامعتبر ⇒ `ProviderFetchResult.Success = false` با پیام بی‌خطر (بدون افشای جزئیات داخلی به UI).
5. **امنیت SSRF:** HTTPS الزامی، Allowlist دامنه، عدم Follow Redirect، بلاک IP خصوصی/loopback/metadata، سقف حجم پاسخ.
6. لاگ بدون API Key (Redaction نام Headerها و Query حساس).

## پیامدها
- **مثبت:** تاب‌آوری در برابر قطع/تغییر منبع؛ تست بدون اینترنت؛ امکان «منبع جایگزین» واقعی.
- **منفی:** کد هر Provider جداگانه نگهداری می‌شود (هزینه‌ای که با سود استقلال موجه است).
- **مبهم باقی‌مانده:** تا دریافت نمونه JSON، `Mapper` و DTO واقعی نوشته نمی‌شوند؛ فقط قرارداد و Fixture ساختاری.

## بازبینی
پس از دریافت `Q-API-1`، این ADR با بخش «نگاشت واقعی» تکمیل می‌شود.
