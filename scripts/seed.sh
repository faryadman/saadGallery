#!/usr/bin/env bash
# scripts/seed.sh — افزودن دادهٔ پایه به دیتابیس (نقش‌ها + کاربران اولیه)
#
# چه می‌کند؟
#   ۱) نقش‌های پایه (Customer, Operator, Admin) را ایدِمپوتنت می‌سازد.
#   ۲) کاربران تعریف‌شده در بخش تنظیمات SeedUsers را می‌سازد و نقششان را تضمین می‌کند.
#   ۳) اجرای دوباره بی‌خطر است: کاربر موجود بازنویسی نمی‌شود و رمزش تغییر نمی‌کند.
#
# امنیت:
#   • رمز از SADGALLERY_SEED_PASSWORD خوانده می‌شود؛ اگر نبود، مخفیانه پرسیده می‌شود (بدون echo).
#   • رمز در هیچ فایل/لاگ/تاریخچه‌ای ذخیره نمی‌شود و در خروجی چاپ نمی‌شود.
#
# استفاده:
#   bash scripts/seed.sh                                        # رمز پرسیده می‌شود (توصیه‌شده)
#   SADGALLERY_SEED_PASSWORD="..." bash scripts/seed.sh          # برای اتوماسیون/CI
#   ConnectionStrings__SadGallery="Server=..." bash scripts/seed.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

# shellcheck source=lib/env.sh
source "${SCRIPT_DIR}/lib/env.sh"

# ---------- ۱) رشته اتصال برنامه (توجه: نه SADGALLERY_CONNECTION که مخصوص dotnet ef است) ----------
if [ -z "${ConnectionStrings__SadGallery:-}" ] && [ -n "${SADGALLERY_CONNECTION:-}" ]; then
    export ConnectionStrings__SadGallery="${SADGALLERY_CONNECTION}"
    echo "[seed] از SADGALLERY_CONNECTION به‌عنوان رشته اتصال برنامه استفاده می‌شود."
fi

if [ -z "${ConnectionStrings__SadGallery:-}" ]; then
    echo "[seed] خطا: رشته اتصال برنامه تنظیم نشده است." >&2
    echo "       ConnectionStrings__SadGallery=\"Server=localhost;Database=SadGallery;Trusted_Connection=True;TrustServerCertificate=True\" bash scripts/seed.sh" >&2
    echo "       (راهنما: docs/DEPLOYMENT.md §۴.۵)" >&2
    exit 2
fi

# ---------- ۲) رمز کاربران اولیه ----------
if [ -z "${SADGALLERY_SEED_PASSWORD:-}" ]; then
    if [ -t 0 ]; then
        printf 'رمز عبور کاربران اولیه را وارد کنید (حداقل ۸ نویسه و یک رقم، نمایش داده نمی‌شود): '
        read -rs SADGALLERY_SEED_PASSWORD
        printf '\n'
        export SADGALLERY_SEED_PASSWORD
    else
        echo "[seed] خطا: SADGALLERY_SEED_PASSWORD تنظیم نشده و ورودی تعاملی هم در دسترس نیست." >&2
        exit 2
    fi
fi

if [ -z "${SADGALLERY_SEED_PASSWORD}" ]; then
    echo "[seed] خطا: رمز خالی است." >&2
    exit 2
fi

# ---------- ۳) اجرا ----------
# فقط سرور و نام دیتابیس چاپ می‌شود — هرگز کل رشته (ممکن است رمز داشته باشد)
_seed_server="$(printf '%s' "${ConnectionStrings__SadGallery}" | sed -nE 's/.*(^|;)[[:space:]]*(Server|Data Source)[[:space:]]*=[[:space:]]*([^;]*).*/\3/Ip' | head -1)"
_seed_db="$(printf '%s' "${ConnectionStrings__SadGallery}" | sed -nE 's/.*(^|;)[[:space:]]*(Database|Initial Catalog)[[:space:]]*=[[:space:]]*([^;]*).*/\3/Ip' | head -1)"
echo "[seed] اجرای Seed روی سرور = ${_seed_server:-?} · دیتابیس = ${_seed_db:-?} (مقدار کامل رشته چاپ نمی‌شود)"
echo

set +e
dotnet run --project src/SadGallery.Web -- --seed
STATUS=$?
set -e

# رمز بلافاصله از محیط همین فرایند پاک می‌شود
unset SADGALLERY_SEED_PASSWORD

if [ ${STATUS} -ne 0 ]; then
    echo "[seed] ناموفق (کد خروج ${STATUS}). جزئیات در خروجی بالا." >&2
    exit ${STATUS}
fi

echo
echo "[seed] ✅ پایان. برای بازبینی در دیتابیس:"
echo "  sqlcmd -S <سرور> -d SadGallery -Q \"SELECT u.UserName, r.Name FROM AspNetUserRoles ur JOIN AspNetUsers u ON u.Id=ur.UserId JOIN AspNetRoles r ON r.Id=ur.RoleId\""
