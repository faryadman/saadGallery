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
#   • دستور database update واقعاً وصل می‌شود؛ رشته اتصال را از متغیر محیطی بدهید:
#       export SADGALLERY_CONNECTION="Server=...;Database=SadGallery;..."
#     و هرگز آن را در فایل مخزن نگذارید.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

# ابزارها بیرون $HOME (سندباکس/CI) — روی ماشین توسعه، dotnet/ef در PATH سیستم هستند
[ -x /opt/dotnet/dotnet ] && export PATH="/opt/dotnet:${PATH}" && export DOTNET_ROOT=/opt/dotnet
[ -x /opt/dotnet/tools/dotnet-ef ] && export PATH="/opt/dotnet/tools:${PATH}"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
[ -d /opt/nuget-packages ] && export NUGET_PACKAGES="${NUGET_PACKAGES:-/opt/nuget-packages}"

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

exec dotnet-ef "$@" \
    --project src/SadGallery.Infrastructure \
    --startup-project src/SadGallery.Web
