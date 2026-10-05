#!/usr/bin/env bash
# scripts/test.sh — اجرای استاندارد تست‌های SadGallery
#
# • تست‌های Unit همیشه اجرا می‌شوند.
# • تست‌های Integration وابسته به SQL Server فقط وقتی اجرا می‌شوند که متغیر محیطی
#   SADGALLERY_TEST_SQL تنظیم شده باشد؛ در غیر این صورت Skip می‌شوند (نه «سبز کاذب»).
#
# استفاده:
#   bash scripts/test.sh
#   SADGALLERY_TEST_SQL="Server=localhost;Database=SadGallery_Test;Trusted_Connection=True;TrustServerCertificate=True" bash scripts/test.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

if [ -x /opt/dotnet/dotnet ] && ! command -v dotnet >/dev/null 2>&1; then
    export PATH="/opt/dotnet:${PATH}"
    export DOTNET_ROOT="/opt/dotnet"
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "== SadGallery tests =="
dotnet --version

UNIT_PROJ="tests/SadGallery.Tests.Unit/SadGallery.Tests.Unit.csproj"
INTEG_PROJ="tests/SadGallery.Tests.Integration/SadGallery.Tests.Integration.csproj"

if [ ! -f "${UNIT_PROJ}" ]; then
    echo "[warn] پروژه تست واحد وجود ندارد (فاز ۰). چیزی برای اجرا نیست."
    exit 0
fi

echo "--- Unit tests ---"
dotnet test "${UNIT_PROJ}" -c Debug --nologo --logger "console;verbosity=normal"

if [ -f "${INTEG_PROJ}" ]; then
    if [ -n "${SADGALLERY_TEST_SQL:-}" ]; then
        echo "--- Integration tests (SQL Server: تنظیم‌شده) ---"
        dotnet test "${INTEG_PROJ}" -c Debug --nologo --logger "console;verbosity=normal"
    else
        echo "--- Integration tests: SKIPPED ---"
        echo "    SADGALLERY_TEST_SQL تنظیم نشده است. تست‌های نیازمند SQL Server اجرا نشدند."
        echo "    (این خروجی باید در گزارش فاز صادقانه ثبت شود.)"
    fi
else
    echo "[warn] پروژه تست یکپارچه وجود ندارد."
fi
