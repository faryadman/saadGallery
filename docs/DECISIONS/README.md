# تصمیم‌های معماری (ADR) — SadGallery

هر تصمیم مهم معماری/امنیتی یک فایل با الگوی زیر دارد. ADR **هرگز حذف نمی‌شود**؛ اگر تصمیمی عوض شد، همان فایل با وضعیت `Superseded by ADR-XXXX` به‌روزرسانی می‌شود.

الگو:
```markdown
# ADR-000N — <عنوان>
- وضعیت: Proposed | Accepted | Superseded by ADR-XXXX | Deprecated
- تاریخ: YYYY-MM-DD
- زمینه / تصمیم / دلیل / پیامدها / جایگزین‌های بررسی‌شده / بازبینی
```

| ADR | عنوان | وضعیت |
| --- | --- | --- |
| 0001 | ASP.NET Core MVC (نه Razor Pages/Blazor) | Accepted |
| 0002 | SQL Server به‌عنوان تنها پایگاه‌داده | Accepted |
| 0003 | استفاده از DbContext بدون لایه Repository | Accepted |
| 0004 | ASP.NET Core Identity + مدل ورود دوگانه | Accepted |
| 0005 | الگوی Provider/Adapter برای منابع نرخ | Accepted |
| 0006 | عبارت محرمانه، KDF و AES-256-GCM + چرخش کلید | Accepted |
| 0007 | پردازش درون‌فرایندی با BackgroundService + قفل | Accepted |
| 0008 | PWA دست‌ساز (Manifest + Service Worker) | Accepted |
| 0009 | مدل واحد پول، وزن و عیار | Accepted |
