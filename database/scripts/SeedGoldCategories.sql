/*
  گروه‌های اولیهٔ ویترین طلا و جواهر — idempotent
  پیش‌نیاز: اعمال migration فاز ۴ (ProductCategories).

  این اسکریپت فقط گروه می‌سازد؛ محصول، قیمت، وزن، عیار، درصد اجرت/سود/مالیات، موجودی
  یا تصویر تولید نمی‌کند. ستون‌های درصد قیمت در schema فعلی NOT NULL هستند؛ چون
  مقادیر واقعی این کالاها از مالک دریافت نشده، درج draft محصول با صفرِ ساختگی مجاز نیست.
  پس از دریافت اطلاعات واقعی، محصول را از پنل اپراتور بسازید و قبل از انتشار بررسی کنید.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

INSERT INTO dbo.ProductCategories (Name, Description, DisplayOrder)
SELECT seed.Name, seed.Description, seed.DisplayOrder
FROM (VALUES
    (N'انگشتر', N'گروه انگشترهای طلا؛ مشخصات دقیق هر کالا جداگانه ثبت می‌شود.', 10),
    (N'گردنبند و زنجیر', N'گروه گردنبند و زنجیر طلا.', 20),
    (N'دستبند', N'گروه دستبندهای طلا.', 30),
    (N'گوشواره', N'گروه گوشواره‌های طلا.', 40),
    (N'پلاک و آویز', N'گروه پلاک و آویز طلا.', 50),
    (N'نیم‌ست و ست', N'گروه نیم‌ست و ست‌های طلا.', 60)
) AS seed(Name, Description, DisplayOrder)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.ProductCategories AS existing
    WHERE existing.Name = seed.Name
);

COMMIT TRANSACTION;
