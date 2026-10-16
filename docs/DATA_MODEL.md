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

### ۳.۲.۱ جدول‌های پیاده‌شدهٔ فاز ۲ (مهاجرت `AddMarketRates`)

| جدول | هدف | نکات کلیدی |
| --- | --- | --- |
| `MarketRates` | تاریخچه افزودنی نرخ‌ها | `AssetCode`, `Amount decimal(18,4)`, `QuoteUnit`, `ProviderId`, `QuotedAtUtc`, `FetchedAtUtc`, `Quality`, `ProviderRawValue`, `ScaleApplied`, `IsAnomalySuspected`, `FetchRunId`. همه ستون‌های هویتی **اجباری** (معیار پذیرش فاز ۲). ایندکس‌ها: `(AssetCode, Id)` و `FetchedAtUtc`. |
| `MarketRateFetchRuns` | رکورد هر اجرای دریافت | `ProviderId`, `Trigger`, `StartedAtUtc`, `FinishedAtUtc`, `DurationMs`, `Outcome`, `HttpStatusCode`, `AcceptedCount`, `FlaggedCount`, `RejectedCount`, `ErrorCode`, `Notes`. هیچ داده حساسی ذخیره نمی‌شود. |
| `MarketFetchLease` | قفل چند-نمونه‌ای | یک ردیف (`Id = 1`) با `OwnerId`, `AcquiredAtUtc`, `ExpiresAtUtc`. تصاحب اتمیک با UPDATE شرطی؛ کرش ⇒ انقضای خودکار. |

**قاعده معنا:** «آخرین نرخ هر دارایی» = **بزرگ‌ترین شناسه** در آن دارایی، و رکوردهای `IsAnomalySuspected` از انتشار عمومی حذف می‌شوند. بازنویسی/حذف رکورد تاریخچه ممنوع است.
**اسکریپت آماده:** `database/scripts/AddMarketRates.sql` (idempotent) و اسکریپت کامل `artifacts/migrations/SadGallery-Full.sql`.

### ۳.۳ مسکوکات و حباب
**CoinDefinition**
- `Id int PK` · `AssetDefinitionId int FK UNIQUE` · `StandardWeightGram decimal(9,4)` (مثل 8.1330 برای امامی) · `Purity decimal(5,2)` (0.900) · `FormulasVersion nvarchar(20)` (`v1`) · `Notes nvarchar(500)?`.
- **قاعده:** وزن/عیار اسمی، «داده مرجع» است و فقط با استناد مستند تغییر می‌کند؛ در `DECISIONS` یا `DATA_MODEL` مأخذ ثبت می‌شود.

**BubbleCalculation** (Snapshot محاسبه؛ برای تاریخچه و رفع اختلاف)
- `Id long PK` · `AssetDefinitionId` · `MarketPriceId` · `ReferenceGoldPriceId` (نرخ مرجع ۱۸ عیار/خالص استفاده‌شده) · `StandardWeightGram` · `Purity` · `UnitPriceReference decimal(18,4)` · `IntrinsicValue decimal(18,2)` · `MarketValue decimal(18,2)` · `BubbleAmount decimal(18,2)` · `BubblePercent decimal(9,4)` · `CurrencyUnit` · `FormulaVersion nvarchar(20)` · `IsTrusted bit` · `InputQuotedAtUtc` · `ComputedAtUtc`.
- **قاعده:** این جدول فقط Snapshot است. محاسبه همیشه در `Domain` انجام و تست می‌شود؛ جدول «منبع فرمول» نیست.

### ۳.۴ ویترین محصولات — **پیاده‌شده در فاز ۴** (مهاجرت `AddProductsAndMedia`)

> این بخش جایگزینِ طراحیِ اولیه شده و **وضعیتِ واقعیِ کد** را نشان می‌دهد.
> تغییراتِ مهم نسبت به طراحیِ اولیه در انتهای همین بخش فهرست شده‌اند.

**ProductCategory** (جدول `ProductCategories`)
- `Id int PK` · `Name nvarchar` · `Description nvarchar?` · `DisplayOrder int`.

**Product** (جدول `Products`)
- `Id int PK` · `Title nvarchar` · `Summary nvarchar?` · `Description nvarchar?` · `CategoryId int? FK`.
- **سیاست قیمت:** `PricePolicy` enum `Fixed` / `Computed` / `QuoteOnly` ·
  `FixedPriceIrt decimal?` (فقط برای `Fixed`).
- **ورودی‌های محاسبه** (فقط برای `Computed`): `WeightGrams` · `Karat` ·
  `MakingChargePercent` · `ProfitPercent` · `TaxPercent`.
- **وضعیت:** `IsInStock bit` · `IsPublished bit` · `IsDeleted bit` (حذفِ نرم).
- **ممیزی:** `CreatedAtUtc` · `UpdatedAtUtc?` · `PublishedAtUtc?` · `DeletedAtUtc?` ·
  `CreatedByUserId` · `UpdatedByUserId?` · `DeletedByUserId?`.
- **عکسِ فوریِ قیمت** (`Price*`): مبلغِ کل و اجزا (`PriceGoldValueIrt`، `PriceMakingIrt`،
  `PriceProfitIrt`، `PriceTaxIrt`)، مبنا (`PriceRateAmountIrt` + `PriceRateQuotedAtUtc`)،
  ورودی‌های استفاده‌شده (`PriceWeightGrams`، `PriceKarat`، درصدها)،
  `PriceComputedAtUtc`، `PriceFormulaVersion`، `PriceReason`.

  > چرا عکسِ فوری ذخیره می‌شود؟ چون معیارِ پذیرش می‌گوید «برای `Computed` مبنای محاسبه
  > و زمان محاسبه ذخیره و نمایش داده شود». نگه‌داریِ عدد بدون مبنا، عددی است که
  > نمی‌توان از آن دفاع کرد.

**ProductImage** (جدول `ProductImages`)
- `Id int PK` · `ProductId int FK` · `StoredFileName nvarchar` · `ThumbnailFileName nvarchar` ·
  `DisplayOrder int` · `SizeBytes bigint` · `Width int` · `Height int` · `DetectedFormat nvarchar`.
- **ردیابی برای بررسیِ امنیتی:** `OriginalFileName nvarchar?` (نامی که کاربر فرستاده) ·
  `ClaimedContentType nvarchar?` (نوعی که کاربر ادعا کرده — فقط برای گزارش).
- **ممیزی:** `UploadedByUserId` · `UploadedAtUtc`.

**قواعدِ اعمال‌شده در کد (نه فقط در مستند):**
- محصولِ منتشرنشده یا حذف‌شده در **هیچ** فهرست یا جزئیاتِ عمومی دیده نمی‌شود؛
  شرط در خودِ پرس‌وجو اعمال می‌شود (پاسخ ۴۰۴، نه ۴۰۳ — تا وجودِ شناسه فاش نشود).
- `PricePolicy=QuoteOnly` ⇒ هیچ عددی نمایش داده نمی‌شود.
- `PricePolicy=Computed` با مبنای ناقص یا کهنه ⇒ **عددی نمایش داده نمی‌شود**؛
  به‌جای آن دلیل گفته می‌شود (معیارِ «منسوخ بدون هشدار نمایش داده نشود»).
- نامِ فایلِ ذخیره‌شده هرگز از ورودی کاربر نمی‌آید (ADR-0014 §۵).

**تغییرات نسبت به طراحیِ اولیهٔ این سند (ثبت برای بازبینیِ مالک):**
1. از `Status` (سه‌حالته) به دو پرچمِ `IsPublished` + `IsDeleted` ساده شد؛ حالتِ
   `Inactive` در عمل با «منتشرنشده» یکی بود و فقط ابهام می‌آفرید.
2. ستون‌های `Code` و `Slug` فعلاً حذف شدند؛ نگه‌داریِ شناسهٔ متنی نیازمند مدیریتِ
   یکتایی و تغییرِ نشانی است که در این فاز ارزش افزوده نداشت (در صورت نیاز، با
   مهاجرتِ جدا اضافه می‌شود).
3. `ContentHash` (SHA-256) برای تصویر فعلاً پیاده نشد؛ تشخیصِ تکرار در این مرحله
   نیاز نبود و افزودنش بدون مصرف‌کننده پیچیدگیِ بی‌دلیل بود (مغایر با ADR-0003).
4. سلسله‌مراتبِ دسته (`ParentId`) پیاده نشد؛ درختِ دسته‌بندی نیازمند رابطِ مدیریتی
   است که در فاز ۶ می‌آید.


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
**StoreSettings** — واقعی، فاز ۶ (`AdministrationSettingsAudit`)
- تک‌ردیفی `Id int PK=1 CHECK`؛ `RatesPaused bit`؛ `FetchIntervalSeconds int? CHECK NULL OR 10..3600`؛ `UpdatedAtUtc datetimeoffset?`؛ `UpdatedByUserId int? FK Restrict`؛ `RowVersion rowversion`.
- Seed: دریافت متوقف نیست و فاصله NULL است (از RateOptions استقرار پیروی می‌کند). پنل حداقل فاصله پیکربندی‌شده را کاهش نمی‌دهد؛ Secret و URL در جدول نیست. فیلدهای تماس/معرفی/سیاست ثبت‌نام طرح اولیه هنوز پیاده نشده‌اند.

**AuditEvent** — واقعی، جدول `AuditEvents`، فاز ۶
- `Id long PK` · `OccurredAtUtc datetimeoffset` · `ActorUserId int?` · `Action nvarchar(80)` · `EntityType nvarchar(80)` · `EntityId nvarchar(64)` · `Fields nvarchar(1000)` فقط نام فیلدها · `IpAddress nvarchar(45)?` · `CorrelationId nvarchar(64)`.
- Index زمان و `(ActorUserId, OccurredAtUtc)`؛ Actor عمداً FK حذف‌شونده ندارد تا شواهد باقی بمانند. IP واقعی اتصال سرور است؛ خواندن سرآیند دلخواه Forwarded انجام نمی‌شود.
- در SaveChanges همان تراکنش EF افزوده می‌شود و تغییر/حذف در برنامه ممنوع است؛ نقش، فعال‌بودن، تنظیمات نرخ/اعلان، حذف/ویرایش محصول، حذف تصویر، ویرایش گروه، وضعیت/ارجاع تیکت را پوشش می‌دهد. هیچ مقدار فیلد، متن پیام، مسیر فایل، رمز یا کلید ذخیره نمی‌شود.
- فقط درخواست دارای هویت HTTP؛ Seed/مهاجرت/CLI به کاربر جعلی نسبت داده نمی‌شود. شروع ایجاد محصول/گروه/تصویر با شناسه موقت در Audit عمومی ثبت نمی‌شود؛ ستون‌های Actor/time آن موجودیت‌ها و رویدادهای تیکت حفظ‌اند.
- سیاست نگهداری ۱۸۰ روز و مجوز SQL محدود/backup در استقرار لازم است؛ Job حذف خودکار هنوز پیاده نیست. CSV حداکثر ۵۰۰ ردیف و بدون IP/CorrelationId؛ صفحه مدیر این دو را برای پیگیری نمایش می‌دهد.

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
| `InitialIdentity` ✅ **ساخته‌شده (2026-10-05)** | ۱ | ۷ جدول `AspNet*` (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens`) + جدول `__EFMigrationsHistory`؛ ۸ ایندکس از جمله `IX_AspNetUsers_IsActive`. (جدول `UserProfile` به فاز ۶ موکول شد تا داده بی‌استفاده ذخیره نشود.) | `Down()` شامل DropTableها؛ اسکریپت: `database/scripts/InitialIdentity.sql` |
| `MarketSchema` | ۲ | `PriceProvider`, `AssetDefinition`, `MarketPrice`, `PriceHistory`, `PriceFetchRun` | Down migration؛ داده قابل بازسازی از منبع |
| `CatalogSchema` | ۴ | `ProductCategory`, `Product`, `ProductImage` | Down + پاک‌سازی فایل‌های یتیم با اسکریپت مستند |
| `TicketsSchema` | ۵ | `SupportTicket`, `TicketMessage`, `TicketInternalNote` (+`TicketAttachment`) | Down (فقط در محیط غیرتولیدی) |
| `AdministrationSettingsAudit` ✅ | ۶ | `StoreSettings`, `AuditEvents`؛ SecurityEvent تیکت موجود است؛ OTP هنوز پیاده نیست | `AdministrationSettingsAuditDown.sql`؛ حذف فقط دو جدول جدید |
| `CoinAndBubble` | ۳ | `CoinDefinition`, `BubbleCalculation` | Down |

**رویه هر Migration (الزامی):**
1. اسکریپت SQL تولید و **بازبینی** شود: `bash scripts/ef.sh migrations script --idempotent --output database/scripts/<name>.sql`
   (پوشش `scripts/ef.sh` مسیر پروژه‌ها و ابزار را ثابت می‌کند؛ خروجی در `database/scripts/` نگهداری و کامیت می‌شود.)
2. روی دیتابیس کپی از داده واقعی آزمایش شود (نه فقط دیتابیس خالی).
3. زمان اجرا اندازه‌گیری و اگر روی جدول بزرگ بود، برنامه Migration آنلاین (Expand/Contract) نوشته شود.
4. Backup پیش از اجرا روی محیط تولید الزامی است.

## ۶. پشتیبان‌گیری و بازیابی (خلاصه — جزئیات در `DEPLOYMENT.md`)
- پشتیبان کامل روزانه + پشتیبان Log هر ۱۵ دقیقه (در صورت Full Recovery Model).
- پشتیبان فایل‌های تصاویر به‌صورت جداگانه با همان زمان‌بندی.
- **آزمون بازیابی الزامی** (فاز ۸): بازگردانی در محیط جداگانه + ثبت زمان و نتیجه واقعی.
- پشتیبان‌ها **رمزنگاری‌شده** و دسترسی محدود؛ همچون داده زنده محافظت می‌شوند (شامل تصاویر مشتریان).

## افزودهٔ اجراشدهٔ فاز ۵ (2026-10-13)
مدل نهایی در `Persistence/TicketModel.cs` و ADR-0016 است. SupportTicket.Id یک GUID و Number یک bigint Identity یکتا است؛ TrackingCode از Number ساخته می‌شود. پیام‌ها عمومی و یادداشت‌ها جدول جدا هستند؛ BodyIsHtml/IsCustomerVisible ستون جدا ندارند چون در این نسخه هیچ پیام HTML/خصوصی وجود ندارد. نوع نقش پیام با IsOperator نشان داده می‌شود (تفکیک Admin/Operator برای رندر لازم نیست). Priority و Attachment اختیاری فعلاً اضافه نشدند. TicketEvents تاریخچه و SecurityEvent محدود به تیکت را ذخیره می‌کند. FK کاربران/محصول و RowVersion و ایندکس‌های صف/شماره/موضوع/تاریخ اعمال شدند. مهاجرت و Down روی SQL آزمایشی اجرا شدند؛ روی داده مالک اجرا نشده‌اند.

## عکس پیام تیکت — 2026-10-14
TicketPhotos: Id uniqueidentifier PK؛ MessageId bigint FK به TicketMessages با Restrict؛ StoredFileName nvarchar(64) با ایندکس یکتا؛ Format int؛ Width/Height int؛ SizeBytes bigint. OriginalFileName یا مسیر عمومی ذخیره نمی‌شود. وجود پیوست فقط به پیام عمومی مشتری مربوط است؛ یادداشت داخلی جدا می‌ماند. Migration `20261013214044_TicketPhotos` افزودنی است و پیام‌های قبلی بدون عکس باقی می‌مانند. جزئیات امنیت/پاک‌سازی در ADR-0017.

## رسید خواندن پیام تیکت — 2026-10-14
TicketMessageReads: UserId int + OperatorView bit + MessageId bigint کلید مرکب؛ ReadAtUtc datetimeoffset؛ FKهای Restrict به AspNetUsers و TicketMessages، ایندکس MessageId. رسید دقیق پیام، بدون تغییر RowVersion تیکت؛ قابل خواندن برای کاربر/نقش مستقل. Migration `20261013222157_TicketMessageReads`؛ Down رسیدها را حذف می‌کند و تیکت/پیام/عکس باقی می‌ماند؛ با ارتقای دوباره شمارش از نو شروع می‌شود. ADR-0018 و SQL `TicketMessageReads.sql`/`TicketMessageReadsDown.sql`.

## تنظیم متن اعلان اتصال — 2026-10-16
بخش محدود مدیر، طبق ADR-0019؛ StoreSettings مجاز نیز در فاز ۶ اضافه شد؛ متن‌های اعلان همچنان در جدول تخصصی خود هستند و ویرایش HTTP آن‌ها علاوه بر ConnectionNoticeEdits، Audit عمومی دارد.
- ConnectionNoticeSettings: تک‌ردیف Id=1 با ValueGeneratedNever و CHECK؛ OfflineTitle/OnlineTitle nvarchar(80)، OfflineDescription/OnlineDescription nvarchar(200)، UpdatedAtUtc datetimeoffset nullable، UpdatedByUserId int nullable FK Restrict و RowVersion. Seed فقط چهار متن پیش‌فرض فعلی؛ Actor/زمان قبل از نخستین ذخیره null.
- ConnectionNoticeEdits: Id bigint Identity، ActorUserId int FK Restrict، CreatedAtUtc datetimeoffset، Fields nvarchar(100)؛ ایندکس Actor و زمان. متن و راز در رویداد ثبت نمی‌شود، فقط نام فیلدهای تغییرکرده. ثبت متن/metadata/رویداد با یک SaveChanges اتمیک.
- Migration 20261016003908_ConnectionNoticeSettings افزودنی؛ Down دو جدول تازه و ویرایش‌ها را حذف می‌کند و به تیکت/پیام/عکس/کاربر دست نمی‌زند. قبل از بازگشت از متن‌های سفارشی/تاریخچه backup گرفته شود؛ ارتقای دوباره پیش‌فرض‌ها را برمی‌گرداند.
