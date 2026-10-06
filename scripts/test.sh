#!/usr/bin/env bash
# scripts/test.sh — اجرای استاندارد تست‌های SadGallery
#
# نکته مهم درباره ابزار:
#   پروژه‌ها روی xunit v3 و Microsoft.Testing.Platform (MTP) اجرا می‌شوند.
#   opt-in تجربه جدید «dotnet test» از طریق فایل global.json انجام شده است.
#   بنابراین گزینه‌های مخصوص VSTest (مثل --logger "console;verbosity=...") اینجا استفاده نمی‌شوند.
#
# رفتار تست‌های دیتابیس:
#   تست‌های نیازمند SQL Server با [RequiresSqlServerFact] علامت‌گذاری شده‌اند و در نبود
#   متغیر محیطی SADGALLERY_TEST_SQL به‌صورت صریح Skip می‌شوند (نه «سبز کاذب»).
#
# استفاده:
#   bash scripts/test.sh
#   SADGALLERY_TEST_SQL="Server=localhost;Database=SadGallery_Test;Trusted_Connection=True;TrustServerCertificate=True" bash scripts/test.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

# ابزارها و کش بیرون $HOME (سندباکس/CI) — منطق مشترک در scripts/lib/env.sh
# shellcheck source=lib/env.sh
source "${SCRIPT_DIR}/lib/env.sh"

echo "== SadGallery tests =="
echo "   SDK: $(dotnet --version)"
echo "   SQL Server test DB: ${SADGALLERY_TEST_SQL:+تنظیم‌شده}${SADGALLERY_TEST_SQL:-تنظیم‌نشده ⇒ تست‌های دیتابیس Skip می‌شوند}"

UNIT_PROJ="tests/SadGallery.Tests.Unit/SadGallery.Tests.Unit.csproj"
INTEG_PROJ="tests/SadGallery.Tests.Integration/SadGallery.Tests.Integration.csproj"

if [ ! -f "${UNIT_PROJ}" ]; then
    echo "[warn] پروژه تست واحد وجود ندارد. چیزی برای اجرا نیست."
    exit 0
fi

echo
echo "--- Unit tests ---"
dotnet test "${UNIT_PROJ}" -c Debug

if [ -f "${INTEG_PROJ}" ]; then
    echo
    echo "--- Integration tests ---"
    dotnet test "${INTEG_PROJ}" -c Debug

    if [ -z "${SADGALLERY_TEST_SQL:-}" ]; then
        echo
        echo "یادآوری: تست‌های RequireSqlServer در این اجرا Skip شدند."
        echo "برای اجرای واقعی آن‌ها (مهاجرت روی دیتابیس خالی، Seed نقش‌ها):"
        echo "  export SADGALLERY_TEST_SQL=\"Server=localhost;Database=SadGallery_Test;Trusted_Connection=True;TrustServerCertificate=True\""
        echo "و دوباره همین اسکریپت را اجرا کنید."
    fi
else
    echo "[warn] پروژه تست یکپارچه وجود ندارد."
fi
