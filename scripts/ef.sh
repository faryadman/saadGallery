#!/usr/bin/env bash
# scripts/ef.sh — پوشش استاندارد برای دستورهای Entity Framework Core
#
# چرا این اسکریپت؟ تا مسیر پروژه‌ها، ابزار و متغیرها یک‌جا و تکرارنشدنی باشند.
# مسیر ابزار dotnet-ef بیرون از $HOME است (غیرماندگار) و در scripts/dev-setup.sh نصب می‌شود.
#
# استفاده:
#   bash scripts/ef.sh migrations add <Name>          # ساخت مهاجرت جدید
#   bash scripts/ef.sh migrations script --idempotent --output database/scripts/<Name>.sql
#   bash scripts/ef.sh migrations list
#   bash scripts/ef.sh migrations remove             # حذف آخرین مهاجرت (پیش از اعمال روی دیتابیس)
#   bash scripts/ef.sh database update               # اعمال مهاجرت‌ها (نیازمند SADGALLERY_CONNECTION)
#
# نکته‌ها:
#   • دستورهای add/script/remove به دیتابیس وصل نمی‌شوند.
#   • دستور database update واقعاً وصل می‌شود و اگر دیتابیس وجود نداشته باشد
#     **خودش آن را می‌سازد** (CREATE DATABASE) و سپس مهاجرت‌ها را اعمال می‌کند.
#   • رشته اتصال را از متغیر محیطی بدهید (هرگز در فایل مخزن):
#       export SADGALLERY_CONNECTION="Server=...;Database=SadGallery;..."
#     نکته: ابزار EF از SADGALLERY_CONNECTION می‌خواند و خودِ برنامه از
#     ConnectionStrings__SadGallery. برای راحتی، اگر اولی تنظیم نشده باشد این
#     اسکریپت دومی را (که مقدارش در محیط توسعه معمولاً همان است) جایگزین می‌کند.
#   • اگر هیچ‌کدام تنظیم نشود، کارخانه زمان طراحی به LocalDB روی ویندوز برمی‌گردد.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

# ابزارها بیرون $HOME (سندباکس/CI) — منطق مشترک در scripts/lib/env.sh
# shellcheck source=lib/env.sh
source "${SCRIPT_DIR}/lib/env.sh"

if ! command -v dotnet-ef >/dev/null 2>&1; then
    echo "[ef] ابزار dotnet-ef یافت نشد." >&2
    echo "     نصب: dotnet tool install --global dotnet-ef --version 10.*" >&2
    echo "     یا: bash scripts/dev-setup.sh" >&2
    exit 2
fi

if [ $# -eq 0 ]; then
    echo "استفاده: bash scripts/ef.sh <migrations|database> <زیر‌دستور> [گزینه‌ها]" >&2
    exit 2
fi

# پل زدن بین دو نام متغیر: ابزار EF از SADGALLERY_CONNECTION می‌خواند، برنامه از
# ConnectionStrings__SadGallery. اگر اولی نبود و دومی بود، همان را استفاده کن تا کاربر
# مجبور نباشد یک رشته را دو بار با دو نام وارد کند (تله‌ای که در آزمون واقعی دیده شد).
if [ -z "${SADGALLERY_CONNECTION:-}" ] && [ -n "${ConnectionStrings__SadGallery:-}" ]; then
    export SADGALLERY_CONNECTION="${ConnectionStrings__SadGallery}"
    echo "[ef] استفاده از ConnectionStrings__SadGallery به‌عنوان رشته اتصال." >&2
fi

if [ -n "${SADGALLERY_CONNECTION:-}" ]; then
    echo "[ef] رشته اتصال تنظیم شده است (مقدار آن چاپ نمی‌شود)." >&2
elif [ "$1" = "database" ]; then
    echo "[ef] هشدار: SADGALLERY_CONNECTION تنظیم نشده؛ کارخانه زمان طراحی به LocalDB برمی‌گردد." >&2
    echo "     روی ویندوز با Visual Studio معمولاً کار می‌کند؛ روی لینوکس/سرور، متغیر را تنظیم کنید." >&2
fi

exec dotnet-ef "$@" \
    --project src/SadGallery.Infrastructure \
    --startup-project src/SadGallery.Web
