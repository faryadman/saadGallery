<#
.SYNOPSIS
    راه‌اندازی کامل SadGallery روی ویندوز: ساخت دیتابیس، اعمال مهاجرت، Seed کاربران و اجرای برنامه.

.DESCRIPTION
    این اسکریپت کلاس خطای «Format of the initialization string does not conform to specification
    starting at index 0» را حذف می‌کند، چون رشته اتصال را خودش درست در همان نشست تنظیم می‌کند
    (دیگر لازم نیست دستی کپی/پیست کنید).

    نکات امنیتی:
      • هیچ رمزی در این فایل یا در مخزن ذخیره نمی‌شود.
      • رمز کاربران اولیه فقط با Read-Host امن پرسیده می‌شود و فقط در متغیر محیطیِ همین نشست می‌ماند.
      • رشته اتصال چاپ نمی‌شود (ممکن است رمز دیتابیس داشته باشد)، فقط شکل خلاصه‌اش.

.PARAMETER ConnectionString
    رشته اتصال SQL Server. پیش‌فرض: LocalDB محلی.
    نمونه‌ها:
      LocalDB      : Server=(localdb)\MSSQLLocalDB;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True
      SQLEXPRESS   : Server=localhost\SQLEXPRESS;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True
      سرور شبکه    : Server=localhost;Database=SadGallery;User Id=sa;Password=<رمز>;TrustServerCertificate=True

.PARAMETER SkipSeed
    کاربران اولیه ساخته نشوند (فقط مهاجرت).

.PARAMETER Run
    پس از پایان، برنامه را اجرا کن (dotnet run).

.EXAMPLE
    .\scripts\windows-setup.ps1
.EXAMPLE
    .\scripts\windows-setup.ps1 -ConnectionString "Server=localhost\SQLEXPRESS;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True" -Run
#>
[CmdletBinding()]
param(
    [string]$ConnectionString = "Server=(localdb)\MSSQLLocalDB;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True",
    [switch]$SkipSeed,
    [switch]$Run
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$Text) { Write-Host "== $Text" -ForegroundColor Cyan }
function Write-Ok([string]$Text)   { Write-Host "   [ok] $Text" -ForegroundColor Green }
function Write-Fail([string]$Text) { Write-Host "   [fail] $Text" -ForegroundColor Red }

function Test-ConnectionStringShape([string]$Value) {
    # همان قواعد ConnectionStringGuard در کد (اعتبارسنجی سریع پیش از اجرا)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "خالی است" }
    if ($Value.StartsWith('"') -or $Value.StartsWith("'")) { return "با کوتیشن شروع می‌شود (کوتیشن‌ها را حذف کنید)" }
    if ($Value.Contains('<') -or $Value.Contains('>')) { return "متن جای‌نگهدار دارد" }
    if (-not $Value.Contains('=')) { return "هیچ جفت کلید=مقدار ندارد" }
    if ($Value -notmatch '(?i)(server|data source)\s*=') { return "کلید Server/Data Source ندارد" }
    if ($Value -notmatch '(?i)(database|initial catalog)\s*=') { return "کلید Database ندارد" }
    return $null
}

Write-Host ""
Write-Step "SadGallery — راه‌اندازی ویندوز"
Write-Host "   مسیر مخزن: $(Get-Location)"

# ---------- ۱) پیش‌نیاز: .NET SDK 10 ----------
Write-Step "بررسی .NET SDK"
$sdkVersion = (dotnet --version) 2>$null
if (-not $sdkVersion) {
    Write-Fail "dotnet یافت نشد. .NET SDK 10 را از https://dotnet.microsoft.com/download/dotnet/10.0 نصب کنید."
    exit 2
}
if (-not $sdkVersion.StartsWith('10.')) {
    Write-Fail "نسخهٔ نصب‌شده $sdkVersion است؛ این پروژه به SDK نسخهٔ 10 نیاز دارد."
    exit 2
}
Write-Ok "SDK $sdkVersion"

# ---------- ۲) اعتبارسنجی رشته اتصال ----------
Write-Step "اعتبارسنجی رشته اتصال"
$shapeError = Test-ConnectionStringShape $ConnectionString
if ($shapeError) {
    Write-Fail "رشته اتصال نامعتبر است: $shapeError"
    Write-Host "   نمونه صحیح:" -ForegroundColor Yellow
    Write-Host '   -ConnectionString "Server=(localdb)\MSSQLLocalDB;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True"'
    exit 2
}
# فقط شکل خلاصه چاپ می‌شود، نه خود رشته (ممکن است رمز داشته باشد)
$serverPart = ([regex]::Match($ConnectionString, '(?i)(server|data source)\s*=\s*([^;]+)')).Groups[2].Value
$dbPart = ([regex]::Match($ConnectionString, '(?i)(database|initial catalog)\s*=\s*([^;]+)')).Groups[2].Value
Write-Ok "سرور: $serverPart · دیتابیس: $dbPart (مقدار کامل چاپ نمی‌شود)"

# ---------- ۳) ابزار dotnet-ef ----------
Write-Step "بررسی ابزار dotnet-ef"
$efInstalled = $false
try { dotnet ef --version *> $null; $efInstalled = $LASTEXITCODE -eq 0 } catch { $efInstalled = $false }
if (-not $efInstalled) {
    Write-Host "   نصب ابزار dotnet-ef (یک‌بار برای همیشه)…"
    dotnet tool install --global dotnet-ef --version 10.* | Out-Null
    $env:PATH = "$env:PATH;$env:USERPROFILE\.dotnet\tools"
}
dotnet ef --version *> $null
if ($LASTEXITCODE -ne 0) { Write-Fail "نصب dotnet-ef ناموفق بود."; exit 3 }
Write-Ok "dotnet-ef آماده است"

# ---------- ۴) تنظیم متغیرهای محیطی فقط برای همین نشست ----------
Write-Step "تنظیم رشته اتصال در همین نشست PowerShell (در هیچ فایلی نوشته نمی‌شود)"
$env:SADGALLERY_CONNECTION = $ConnectionString          # ابزار dotnet ef
$env:ConnectionStrings__SadGallery = $ConnectionString  # خود برنامه
Write-Ok "دو متغیر تنظیم شد (ابزار مهاجرت + برنامه)"

# ---------- ۵) مهاجرت (ساخت دیتابیس در همین مرحله انجام می‌شود) ----------
Write-Step "اعمال مهاجرت‌ها (اگر دیتابیس نباشد، ساخته می‌شود)"
dotnet ef database update --project src/SadGallery.Infrastructure --startup-project src/SadGallery.Web
if ($LASTEXITCODE -ne 0) {
    Write-Fail "مهاجرت ناموفق بود. متن خطای بالا را ببینید و در صورت نیاز به docs\DEPLOYMENT.md §۴.۷ مراجعه کنید."
    exit 4
}
Write-Ok "مهاجرت کامل شد"

# ---------- ۶) Seed کاربران ----------
if (-not $SkipSeed) {
    Write-Step "ساخت کاربران اولیه (Seed)"
    Write-Host "   کاربران نمونه: admin@ · operator@ · customer@ sadgallery.local" -ForegroundColor Yellow
    $secure = Read-Host -Prompt "   یک رمز عبور برای این کاربران وارد کنید (حداقل ۸ نویسه و یک رقم)" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        $env:SADGALLERY_SEED_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }

    if ([string]::IsNullOrWhiteSpace($env:SADGALLERY_SEED_PASSWORD)) {
        Write-Fail "رمز خالی است؛ Seed انجام نشد."
        exit 5
    }

    dotnet run --project src/SadGallery.Web -- --seed
    $seedExit = $LASTEXITCODE

    Remove-Item Env:\SADGALLERY_SEED_PASSWORD -ErrorAction SilentlyContinue  # رمز در نشست هم نمی‌ماند

    if ($seedExit -ne 0) {
        Write-Fail "Seed ناموفق بود (کد خروج $seedExit)."
        exit $seedExit
    }
    Write-Ok "کاربران ساخته/بررسی شدند (رمز هرگز چاپ نمی‌شود)"
}

Write-Host ""
Write-Host "================ نتیجه ================" -ForegroundColor Green
Write-Host " • دیتابیس: $dbPart روی $serverPart"
Write-Host " • ورود:    http://localhost:5000/Account/Login"
Write-Host " • کاربران: admin@ / operator@ / customer@ sadgallery.local (با رمزی که خودتان دادید)"
Write-Host " • هر بار باز کردن ترمینال جدید: دوباره همین اسکریپت یا فقط خط زیر را اجرا کنید:"
Write-Host '   $env:ConnectionStrings__SadGallery = "<رشته اتصال شما>"' -ForegroundColor Yellow
Write-Host "======================================"

if ($Run) {
    Write-Step "اجرای برنامه (Ctrl+C برای خروج)"
    dotnet run --project src/SadGallery.Web
}
