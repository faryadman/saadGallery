# مدل داده — SadGallery

> قواعد: پول هرگز `float`/`double` نیست · همه زمان‌ها UTC هستند · هر تغییر با Migration قابل بازگشت · Indexها بر اساس Query واقعی طراحی می‌شوند، نه حدس.

---

## ۱. قواعد نوع داده (الزامی)

| نوع محتوا | نوع C# | نوع SQL | Precision | یادداشت |
| --- | --- | --- | --- | --- |
| مبلغ ریالی/تومانی | `decimal` | `decimal(18,2)` | دقیق | واحد در ستون مجزا یا در همان رکورد مشخص (`Unit`) |
| قیمت اونس/رمزارز جهانی | `decimal` | `decimal(18,4)` | دقیق | مثلاً `4119.6600` |
| درصد تغییر | `decimal` | `decimal(9,4)` | دقیق | منفی مجاز |
| وزن (گرم) | `decimal` | `decimal(9,4)` | دقیق | وزن اسمی سکه = ثابت کد، وزن واقعی محصول = ورودی |
| عیار | `decimal` | `decimal(5,2)` | دقیق | 18.00 ، 24.00 ، 0.900 (سکه) |
| زمان‌ها | `DateTimeOffset` (ذخیره `datetimeoffset`) | — | — | همیشه UTC ذخیره، نمایش محلی |
| شناسه‌ها | `int` (Identity) / `Guid` (رکوردهای عمومی مثل شماره پیگیری) | — | — | GUID جای مجوز نیست |
| متن‌های بلند | `string` با سقف صریح | `nvarchar(n)` | — | `nvarchar(max)` فقط با دلیل مستند |
| ردیف‌ها | `byte[]` کوتاه | `varbinary(n)` | — | Salt/Nonce/Tag |
| توکن هم‌زمانی | `byte[]` | `rowversion` | — | روی رکوردهای پرتغییر |

> `decimal(18,2)` برای ریال امروز کافی است (سقف ~۱۰^۱۶ ریال). اگر روزی تورم از این گذشت، ارتقا با Migration مستند انجام می‌شود.

## ۲. نمودار موجودیت‌ها (متن‌محور)

```
Identity:  AspNetUsers ──1:1── UserProfile
                 │
                 ├──1:N── SupportTicket ──1:N── TicketMessage
                 │                └──────1:N── TicketInternalNote
                 ├──1:N── AuditEvent / SecurityEvent (Actor)
                 └──1:N── ProductImage (UploadedBy)

Catalog:   ProductCategory ──1:N── Product ──1:N── ProductImage
                 │                     └─(optional FK)── Product
                                     └──1:N── TicketMessage (RelatedProductId)

Market:    PriceProvider ──1:N── PriceFetchRun
                 └──1:N── MarketPrice       (آخرین وضعیت هر دارایی)
                 └──1:N── PriceHistory      (سری زمانی)

Reference: AssetDefinition ──1:1── CoinDefinition (فقط برای مسکوکات)
           BubbleCalculation (Snapshots محاسبه‌شده برای گزارش/تاریخچه)

Settings:  StoreSettings (تک‌ردیفی، با RowVersion)
```

## ۳. موجودیت‌ها، ستون‌ها و کلیدها

### ۳.۱ Identity
**ApplicationUser : IdentityUser<int>** (کلید `int` انتخاب شد: کارایی Index و سادگی)
- `DisplayName nvarchar(100)` (اختیاری)، `PhoneNumber` (از Identity)، `CreatedAtUtc`، `LastLoginAtUtc`، `IsActive bit` (تعلیق حساب بدون حذف)، `MustChangePassword bit`.
- مرتبط: `UserProfile` (اختیاری برای داده‌های پروفایل مشتری).

**UserProfile**
- `UserId int PK/FK` · `FirstName nvarchar(60)` · `LastName nvarchar(60)` · `BirthDateUtc date?` · `NationalIdProtected varbinary(512)?` (رمزنگاری‌شده، ADR-0006) · `NationalIdKeyId smallint?` · `PreferencesJson nvarchar(1000)?` (فقط داده غیرحساس) · `RowVersion rowversion`.

**Roles:** `Customer`، `Operator`، `Admin` (Seed ایدمپوتنت). نقش‌های دقیق‌تر در آینده به‌صورت Claim/Policy، نه نقش جدید بی‌دلیل.

### ۳.۲ بازار
**PriceProvider**
- `Id int PK` · `Key nvarchar(50) UNIQUE` · `DisplayName nvarchar(100)` · `BaseUrl nvarchar(300)` · `ApiKeyProtected varbinary(1024)?` + `ApiKeyKeyId smallint?` · `IsEnabled bit` · `Priority int` · `TimeoutSeconds int` · `MinIntervalSeconds int` (نرخ محدودکننده) · `CreatedAtUtc`/`UpdatedAtUtc` · `RowVersion`.
- **قاعده:** API Key هرگز متن‌رو ذخیره نمی‌شود؛ با `ISecretProtector` رمزنگاری و در لاگ Redact می‌شود.
- **قاعده SSRF:** `BaseUrl` فقط `https` و محدود به دامنه‌های مجاز (`AllowedDomains`)؛ اعتبارسنجی در زمان ذخیره **و** زمان فراخوانی.

**AssetDefinition** (دارایی‌های قابل نمایش؛ مرجع واحد)
- `Id int PK` · `Key nvarchar(50) UNIQUE` (مثل `gold-18k-gram`, `coin-emami`, `usd`, `btc`) · `DisplayName fa nvarchar(100)` · `Kind` enum: `Gold`/`Coin`/`Currency`/`Crypto`/`Other` · `Unit` enum: `Gram`/`Mesghal`/`Piece`/`Unit` · `Purity decimal(5,2)?` · `IsPublic bit` · `DisplayOrder int` · `RoundDecimals tinyint`.
- **این جدول «فرهنگ داده» است:** هیچ نرخی بدون اشاره به یک `AssetDefinition` شناخته‌شده ثبت نمی‌شود (جلوگیری از ورود نرخ ناشناس).

**MarketPrice** (آخرین وضعیت هر دارایی از هر منبع — برای خواندن سریع)
- `Id long PK` · `AssetDefinitionId int FK` · `PriceProviderId int FK` · `Value decimal(18,4)` · `Unit` enum `IRR`/`IRT`/`USD`/`USDT` · `QuotedAtUtc datetimeoffset` (اعلام منبع) · `FetchedAtUtc datetimeoffset` (دریافت ما) · `Quality` enum `Live`/`Delayed`/`Stale`/`Invalid` · `SourcePayloadHash char(64)?` · `RawJson nvarchar(max)?` (خام برای رفع اختلاف؛ فقط برای آخرین رکورد) · `CreatedAtUtc`.
- **Unique:** `(AssetDefinitionId, PriceProviderId) WHERE IsCurrent = 1` — پیاده‌سازی با ستون `IsCurrent bit` و Index فیلترشده (`HasFilter`) تا همیشه یک «نرخ جاری» داشته باشیم.
- **Index:** `IX_MarketPrice_Asset_Current (AssetDefinitionId, IsCurrent)` برای صفحه اصلی.

**PriceHistory** (سری زمانی؛ منبع نمودار)
- `Id long PK` · `AssetDefinitionId int FK` · `PriceProviderId int FK` · `Value decimal(18,4)` · `Unit` · `QuotedAtUtc` · `FetchedAtUtc` · `Quality` · `IsAnomaly bit` (مثلاً پرش غیرمنتظره؛ برای عدم آلودگی نمودار).
- **Index:** `IX_PriceHistory_Asset_QuotedAt (AssetDefinitionId, QuotedAtUtc DESC)` — کوئری اصلی «بازه زمانی».
- **سیاست نگهداری:** داده خام ۹۰ روز کامل + پس از آن تجمیع ساعتی/روزانه (`PriceHistoryDaily`) در فاز ۸؛ Job پاک‌سازی مستند و قابل خاموش‌کردن.

**PriceFetchRun** (Observability Job)
- `Id long PK` · `PriceProviderId` · `StartedAtUtc`/`FinishedAtUtc` · `Success bit` · `HttpStatus int?` · `ItemsCount int` · `DurationMs int` · `ErrorMessageSafe nvarchar(500)?` (بدون Secret) · `CorrelationId`.

### ۳.۳ مسکوکات و حباب
**CoinDefinition**
- `Id int PK` · `AssetDefinitionId int FK UNIQUE` · `StandardWeightGram decimal(9,4)` (مثل 8.1330 برای امامی) · `Purity decimal(5,2)` (0.900) · `FormulasVersion nvarchar(20)` (`v1`) · `Notes nvarchar(500)?`.
- **قاعده:** وزن/عیار اسمی، «داده مرجع» است و فقط با استناد مستند تغییر می‌کند؛ در `DECISIONS` یا `DATA_MODEL` مأخذ ثبت می‌شود.

**BubbleCalculation** (Snapshot محاسبه؛ برای تاریخچه و رفع اختلاف)
- `Id long PK` · `AssetDefinitionId` · `MarketPriceId` · `ReferenceGoldPriceId` (نرخ مرجع ۱۸ عیار/خالص استفاده‌شده) · `StandardWeightGram` · `Purity` · `UnitPriceReference decimal(18,4)` · `IntrinsicValue decimal(18,2)` · `MarketValue decimal(18,2)` · `BubbleAmount decimal(18,2)` · `BubblePercent decimal(9,4)` · `CurrencyUnit` · `FormulaVersion nvarchar(20)` · `IsTrusted bit` · `InputQuotedAtUtc` · `ComputedAtUtc`.
- **قاعده:** این جدول فقط Snapshot است. محاسبه همیشه در `Domain` انجام و تست می‌شود؛ جدول «منبع فرمول» نیست.

### ۳.۴ ویترین محصولات
**ProductCategory**
- `Id int PK` · `Slug nvarchar(100) UNIQUE` · `Title nvarchar(100)` · `ParentId int? FK` · `DisplayOrder int` · `IsActive bit` · `CreatedAtUtc`/`UpdatedAtUtc` · `RowVersion`.

**Product**
- `Id int PK` · `Code nvarchar(40) UNIQUE` (کد محصول) · `Title nvarchar(200)` · `Slug nvarchar(200) UNIQUE` · `CategoryId int FK` · `Description nvarchar(4000)` · `WeightGram decimal(9,4)?` · `Purity decimal(5,2)?` · `Status` enum `Draft`/`Published`/`Inactive` · `Availability` enum `InStock`/`OutOfStock`/`MadeToOrder` · `IsFeaturedOnHome bit` · `PricePolicy` enum `QuoteOnly`/`Fixed`/`Computed` · `FixedPriceAmount decimal(18,2)?` + `FixedPriceUnit` · `ComputedBaseAssetId int? FK` · `ComputedWagePercent decimal(9,4)?` (اجرت) · `ComputedProfitPercent decimal(9,4)?` · `PriceCalculatedAtUtc datetimeoffset?` · `PublishedAtUtc?` · `CreatedByUserId`/`UpdatedByUserId` · `CreatedAtUtc`/`UpdatedAtUtc` · `RowVersion`.
- **قواعد:**
  - `Status=Published` فقط با تأیید اپراتور/مدیر؛ هیچ محتوای تأییدنشده منتشر نمی‌شود.
  - `PricePolicy=Computed` ⇒ نمایش قیمت **الزاماً** همراه `PriceCalculatedAtUtc` و هشدار کهنگی.
  - `PricePolicy=QuoteOnly` ⇒ هیچ عددی نمایش داده نمی‌شود، فقط دکمه «استعلام قیمت».

**ProductImage**
- `Id int PK` · `ProductId int FK` · `StoragePath nvarchar(300)` (نسبی؛ بیرون `wwwroot`) · `PublicUrl nvarchar(300)` · `ThumbnailPath nvarchar(300)` · `WidthPx int` · `HeightPx int` · `ByteSize int` · `ContentHash char(64)` (SHA-256 برای تشخیص تکرار/تغییر) · `AltText nvarchar(200)?` · `DisplayOrder int` · `UploadedByUserId` · `UploadedAtUtc`.
- **Index:** `IX_ProductImage_Product_Order (ProductId, DisplayOrder)`.

### ۳.۵ تیکت و استعلام قیمت
**SupportTicket**
- `Id long PK` · `TrackingCode nvarchar(20) UNIQUE` (قابل خواندن برای انسان) · `CustomerUserId int FK` · `Subject nvarchar(150)` · `Type` enum `PriceInquiry`/`ProductQuestion`/`StockAndWeight`/`OrderFollowUp`/`AccountSupport`/`Other` · `RelatedProductId int? FK` · `Status` enum `New`/`InReview`/`AwaitingCustomer`/`Answered`/`Closed` · `Priority` enum `Normal`/`High` · `AssignedOperatorUserId int? FK` · `FirstResponseAtUtc?` · `LastActivityAtUtc` · `ClosedAtUtc?` · `CreatedAtUtc` · `RowVersion`.
- **Index:** `IX_Ticket_Customer (CustomerUserId, LastActivityAtUtc DESC)` · `IX_Ticket_Queue (Status, LastActivityAtUtc)` برای صف اپراتور · `IX_Ticket_Assigned (AssignedOperatorUserId, Status)`.
- **قاعده دسترسی:** مالکیت (`CustomerUserId == currentUser.Id`) یا نقش `Operator`/`Admin`. **شناسه GUID/عدد هیچ‌گاه مجوز نیست.**

**TicketMessage**
- `Id long PK` · `TicketId long FK` · `AuthorUserId int FK` · `AuthorRole` enum `Customer`/`Operator`/`Admin`/`System` · `Body nvarchar(4000)` (متن ساده؛ رندر Encode‌شده) · `BodyIsHtml bit = 0` (در نسخه اول همیشه false) · `CreatedAtUtc` · `IsCustomerVisible bit` (در پیام‌های اپراتور true).
- **Index:** `IX_TicketMessage_Ticket (TicketId, CreatedAtUtc)`.

**TicketInternalNote**
- `Id long PK` · `TicketId long FK` · `AuthorUserId int FK` · `Body nvarchar(2000)` · `CreatedAtUtc`.
- **قاعده:** این جدول هرگز در کوئری‌های مشتری خوانده نمی‌شود (فیلتر در لایه نتایج و تست امنیتی).

**TicketAttachment** (اختیاری، فاز ۵) — با همان قواعد سخت آپلود تصویر؛ نوع فایل محدود، اسکن محتوا، سقف حجم/تعداد.

### ۳.۶ تنظیمات، Audit، امنیت
**StoreSettings** (تک‌ردیفی؛ `Id int PK = 1 CHECK`)
- `StoreName` · `Phone` · `Address` · `WebsiteUrl` · `InstagramUrl` · `WorkingHours` · `AboutText` · `DefaultFetchIntervalSeconds int` · `StaleThresholdMinutes int` · `PublicHistoryDays int` (تاریخچه عمومی) · `MemberHistoryDays int` · `AllowRegistration bit` · `RequirePhoneConfirmation bit` · `QuoteOnlyNoticeText` · `UpdatedAtUtc` · `UpdatedByUserId` · `RowVersion`.
- **قاعده:** فقط کلیدهای «مجاز» اینجا هستند. Secret هیچ‌گاه در این جدول نیست.

**AuditEvent**
- `Id long PK` · `OccurredAtUtc` · `ActorUserId int?` · `ActorRole nvarchar(50)?` · `Action nvarchar(80)` (مثل `Ticket.StatusChanged`) · `EntityType nvarchar(80)` · `EntityId nvarchar(64)` · `ChangesJson nvarchar(2000)?` (فقط فیلدهای غیرحساس؛ مقادیر حساس Redact) · `IpHash char(64)?` · `UserAgentHash char(64)?` · `CorrelationId nvarchar(64)?`.
- **Index:** `IX_Audit_Occurred (OccurredAtUtc DESC)` · `IX_Audit_Actor (ActorUserId, OccurredAtUtc DESC)`.
- **بازداشت حداکثر ۱۸۰ روز** (قابل تنظیم) — سیاست پاک‌سازی Job مستند.

**SecurityEvent**
- `Id long PK` · `OccurredAtUtc` · `Kind` enum `LoginFailed`/`AccountLocked`/`OtpRequestedTooOften`/`OtpInvalidAttempts`/`AccessDenied`/`UploadRejected`/`RateLimitHit`/`SecretAccess`/`ProviderFetchFailed` · `Severity` enum `Info`/`Warning`/`Critical` · `UserId int?` · `IdentifierMasked nvarchar(120)?` (مثل `09******123` — **ماسک‌شده**) · `IpHash char(64)?` · `Details nvarchar(500)?` (بدون Secret/OTP) · `CorrelationId`.

**OtpChallenge** (رمز یک‌بارمصرف؛ ADR-0004/0006)
- `Id long PK` · `UserId int?` (اگر کاربر موجود باشد) · `IdentifierMasked nvarchar(120)` + `IdentifierHash char(64)` (Lookup امن بدون ذخیره متن‌رو) · `Purpose` enum `Login`/`Register`/`PhoneConfirm`/`PasswordReset` · `CodeHash varbinary(64)` (هش HMAC-SHA256 با کلید سرور — کد خام هرگز ذخیره نمی‌شود) · `Salt varbinary(32)?`/`KdfVersion tinyint?` (در صورت استفاده از PBKDF2 برای OTP) · `ExpiresAtUtc` · `AttemptCount tinyint` · `MaxAttempts tinyint` · `ConsumedAtUtc?` · `CreatedAtUtc` · `RequestIpHash char(64)?`.
- **Index:** `IX_Otp_Identifier_Purpose_Created (IdentifierHash, Purpose, CreatedAtUtc DESC)` برای بررسی نرخ ارسال.
- **قاعده:** کد یک‌بارمصرف هرگز در لاگ/پاسخ API/پیام خطا ظاهر نمی‌شود؛ پس از مصرف `ConsumedAtUtc` ثبت و همان کد غیرقابل استفاده می‌شود.

**LoginCredential/Lockout:** طبق Identity (`AccessFailedCount`, `LockoutEnd`) با تنظیم پارامترها در `SecurityOptions`.

## ۴. سازگاری، هم‌زمانی و تراکنش

- **RowVersion (`rowversion`)** روی: `Product`، `ProductCategory`، `PriceProvider`، `StoreSettings`، `SupportTicket`، `UserProfile`. UI پنل هنگام تضاد، پیام «این رکورد توسط کاربر دیگری تغییر کرده» + نمایش مقادیر فعلی می‌دهد.
- **تراکنش** لازم در: ثبت تیکت + اولین پیام (اتمیک)، تغییر وضعیت + پیام سیستمی، درج `MarketPrice` جاری + رکورد `PriceHistory`، انتشار محصول + ترتیب تصاویر.
- **حذف (Soft delete فقط جایی که لازم است):** محصول و دسته `Status=Inactive` می‌شوند (حذف فیزیکی فقط برای تصاویر بی‌ارجاع با ثبت Audit). تیکت‌ها هرگز حذف نمی‌شوند. کاربران غیرفعال می‌شوند (`IsActive=false`)، نه حذف.
- **حذف حساب کاربر (حق فراموش‌شدن):** ناشناس‌سازی رکوردهای مرتبط + حذف داده‌های پروفایل؛ تیکت‌ها با نویسنده «کاربر حذف‌شده» می‌مانند. رویه فاز ۸.

## ۵. برنامه Migration (تدریجی و قابل بازگشت)

| # | فاز | محتوا | راه بازگشت |
| --- | --- | --- | --- |
| `InitialIdentity` | ۱ | `AspNet*` + `ApplicationUser` + `UserProfile` + Seed نقش‌ها | `dotnet ef database update <قبلی>` (بدون داده تولیدی) |
| `MarketSchema` | ۲ | `PriceProvider`, `AssetDefinition`, `MarketPrice`, `PriceHistory`, `PriceFetchRun` | Down migration؛ داده قابل بازسازی از منبع |
| `CatalogSchema` | ۴ | `ProductCategory`, `Product`, `ProductImage` | Down + پاک‌سازی فایل‌های یتیم با اسکریپت مستند |
| `TicketsSchema` | ۵ | `SupportTicket`, `TicketMessage`, `TicketInternalNote` (+`TicketAttachment`) | Down (فقط در محیط غیرتولیدی) |
| `SettingsAuditSecurity` | ۶ | `StoreSettings`, `AuditEvent`, `SecurityEvent`, `OtpChallenge` | Down |
| `CoinAndBubble` | ۳ | `CoinDefinition`, `BubbleCalculation` | Down |

**رویه هر Migration (الزامی):**
1. اسکریپت SQL تولید و **بازبینی** شود: `dotnet ef migrations script <from> <to> --idempotent -o artifacts/migrations/<name>.sql`
2. روی دیتابیس کپی از داده واقعی آزمایش شود (نه فقط دیتابیس خالی).
3. زمان اجرا اندازه‌گیری و اگر روی جدول بزرگ بود، برنامه Migration آنلاین (Expand/Contract) نوشته شود.
4. Backup پیش از اجرا روی محیط تولید الزامی است.

## ۶. پشتیبان‌گیری و بازیابی (خلاصه — جزئیات در `DEPLOYMENT.md`)
- پشتیبان کامل روزانه + پشتیبان Log هر ۱۵ دقیقه (در صورت Full Recovery Model).
- پشتیبان فایل‌های تصاویر به‌صورت جداگانه با همان زمان‌بندی.
- **آزمون بازیابی الزامی** (فاز ۸): بازگردانی در محیط جداگانه + ثبت زمان و نتیجه واقعی.
- پشتیبان‌ها **رمزنگاری‌شده** و دسترسی محدود؛ همچون داده زنده محافظت می‌شوند (شامل تصاویر مشتریان).
