<#
.SYNOPSIS
    افزودن دادهٔ پایه به دیتابیس SadGallery (نقش‌ها + کاربران اولیه).

.DESCRIPTION
    ۱) نقش‌های پایه (Customer, Operator, Admin) را ایدِمپوتنت می‌سازد.
    ۲) کاربران تعریف‌شده در بخش تنظیمات SeedUsers را می‌سازد و نقششان را تضمین می‌کند.
    ۳) اجرای دوباره بی‌خطر است: کاربر موجود بازنویسی نمی‌شود و رمزش تغییر نمی‌کند.

    امنیت: رمز با Read-Host امن پرسیده می‌شود، فقط در متغیر محیطیِ همین فرایند می‌ماند،
    در هیچ فایل/لاگ ذخیره نمی‌شود و در پایان پاک می‌شود.

.PARAMETER ConnectionString
    رشته اتصال برنامه. اگر ندهید، از $env:ConnectionStrings__SadGallery خوانده می‌شود.
    پیش‌فرض نمونه: Server=(localdb)\MSSQLLocalDB;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True

.EXAMPLE
    .\scripts\seed.ps1
.EXAMPLE
    .\scripts\seed.ps1 -ConnectionString "Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True"
#>
[CmdletBinding()]
param(
    [string]$ConnectionString = ""
)

$ErrorActionPreference = 'Stop'

Write-Host ""
Write-Host "== SadGallery — افزودن دادهٔ پایه (Seed) ==" -ForegroundColor Cyan

if (-not $ConnectionString) {
    $ConnectionString = $env:ConnectionStrings__SadGallery
}

if (-not $ConnectionString) {
    Write-Host "   [fail] رشته اتصال تعیین نشده است." -ForegroundColor Red
    Write-Host '   نمونه: .\scripts\seed.ps1 -ConnectionString "Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True"' -ForegroundColor Yellow
    exit 2
}

if ($ConnectionString.Contains('<') -or $ConnectionString.Contains('HOST_FROM_ENV') -or -not $ConnectionString.Contains('=')) {
    Write-Host "   [fail] رشته اتصال نامعتبر است (جای‌نگهدار یا بدون کلید=مقدار)." -ForegroundColor Red
    Write-Host "   راهنما: docs\DEPLOYMENT.md §۴.۷" -ForegroundColor Yellow
    exit 2
}

# فقط شکل خلاصه چاپ می‌شود (ممکن است رمز داشته باشد)
$serverPart = ([regex]::Match($ConnectionString, '(?i)(server|data source)\s*=\s*([^;]+)')).Groups[2].Value
$dbPart = ([regex]::Match($ConnectionString, '(?i)(database|initial catalog)\s*=\s*([^;]+)')).Groups[2].Value
Write-Host "   سرور: $serverPart · دیتابیس: $dbPart"

# مسیر پیام‌های راهنما: کاربران نمونه
Write-Host "   کاربران نمونه: admin@ · operator@ · customer@ sadgallery.local" -ForegroundColor Yellow
Write-Host "   (فهرست واقعی از appsettings.Development.json / تنظیمات محیطی خوانده می‌شود)"

$secure = Read-Host -Prompt "   رمز عبور این کاربران (حداقل ۸ نویسه و یک رقم)" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $env:SADGALLERY_SEED_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

if ([string]::IsNullOrWhiteSpace($env:SADGALLERY_SEED_PASSWORD)) {
    Write-Host "   [fail] رمز خالی است." -ForegroundColor Red
    exit 2
}

$env:ConnectionStrings__SadGallery = $ConnectionString

try {
    dotnet run --project src/SadGallery.Web -- --seed
    $status = $LASTEXITCODE
} finally {
    # پاک‌سازی محیط همین فرایند
    Remove-Item Env:\SADGALLERY_SEED_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\ConnectionStrings__SadGallery -ErrorAction SilentlyContinue
}

if ($status -ne 0) {
    Write-Host "   [fail] Seed ناموفق بود (کد خروج $status)." -ForegroundColor Red
    exit $status
}

Write-Host ""
Write-Host "   [ok] دادهٔ پایه در دیتابیس نوشته شد." -ForegroundColor Green
Write-Host "   بازبینی:"
Write-Host '   sqlcmd -S localhost -d SadGallery -Q "SELECT u.UserName, r.Name FROM AspNetUserRoles ur JOIN AspNetUsers u ON u.Id=ur.UserId JOIN AspNetRoles r ON r.Id=ur.RoleId"'
Write-Host "   ورود: http://localhost:5000/Account/Login"
