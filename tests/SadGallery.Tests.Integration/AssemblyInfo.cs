using Xunit.Sdk;
using Xunit.v3;

// چرا موازی‌سازی غیرفعال است؟
//   هر کلاس تست یک WebApplicationFactory مستقل می‌سازد و یک میزبان واقعی ASP.NET Core بالا می‌آورد.
//   اجرای هم‌زمان چند میزبان ⇒ رقابت روی منابع مشترک (کلیدهای DataProtection در ~/.aspnet،
//   شمارنده‌های Rate Limit در حافظه، فایل‌های موقت) و در نتیجه شکست ناپایدار (BUG-007).
//   هزینه: چند ثانیه زمان اجرا. سود: نتیجه قطعی و تکرارپذیر. هیچ تستی حذف/غیرفعال نشده است.
//
// نکته API: در xunit v3، ویژگی منسوخ CollectionBehavior.DisableTestParallelization حذف شده و
// جایش [assembly: Parallelization(Mode = ParallelMode.None)] است (آزمون‌شده روی 4.0.1).
[assembly: Parallelization(Mode = ParallelMode.None)]
