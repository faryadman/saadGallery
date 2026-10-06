#!/usr/bin/env bash
# scripts/git-push.sh — انتشار ایمن شاخه main روی مخزن رسمی GitHub
#
# اصل امنیتی: توکن هرگز در فایل، Git config، لاگ یا آرگومان‌های خط فرمان نوشته نمی‌شود.
#   • توکن از متغیر محیطی GITHUB_TOKEN خوانده می‌شود (نه آرگومان ⇒ نه در history نه در ps).
#   • فقط در یک فایل موقت با مجوز 600 (بیرون مخزن) قرار می‌گیرد و بلافاصله پس از push پاک می‌شود.
#   • credential.helper فقط برای همان یک دستور فعال است؛ در .git/config چیزی نوشته نمی‌شود.
#   • در صورت خطا، خروجی برای حذف توکن «پاک‌سازی» می‌شود.
#
# استفاده:
#   GITHUB_TOKEN=<توکن> bash scripts/git-push.sh
#   GITHUB_TOKEN=<توکن> bash scripts/git-push.sh --dry-run
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

REMOTE_URL="https://github.com/faryadman/saadGallery.git"
BRANCH="${PUSH_BRANCH:-main}"
DRY_RUN=0
[ "${1:-}" = "--dry-run" ] && DRY_RUN=1

if [ -z "${GITHUB_TOKEN:-}" ]; then
    echo "[push] خطا: متغیر محیطی GITHUB_TOKEN تنظیم نشده است." >&2
    echo "       اجرا:  GITHUB_TOKEN=<توکن> bash scripts/git-push.sh" >&2
    exit 2
fi

# ---------- ۱) پیش‌شرط‌ها ----------
if [ -n "$(git status --porcelain)" ]; then
    echo "[push] خطا: تغییرهای کامیت‌نشده وجود دارد. اول آن‌ها را کامیت کنید (bash scripts/git-commit.sh)." >&2
    exit 3
fi

if ! git remote get-url origin >/dev/null 2>&1; then
    git remote add origin "${REMOTE_URL}"
    echo "[push] ریموت origin بازسازی شد: ${REMOTE_URL}"
fi

echo "[push] شاخه: ${BRANCH} | کامیت محلی: $(git log --oneline -1)"
echo "[push] کامیت‌های آماده انتشار نسبت به origin/${BRANCH}:"
git log --oneline "origin/${BRANCH}..${BRANCH}" 2>/dev/null | sed 's/^/       /' || echo "       (شاخه ریموت محلی موجود نیست — fetch لازم است)"

# ---------- ۲) فایل اعتبارنامه موقت ----------
CRED_FILE="$(mktemp)"
chmod 600 "${CRED_FILE}"
cleanup() {
    # پاک‌سازی مطمئن: ابتدا بازنویسی با داده تصادفی، سپس حذف
    if [ -f "${CRED_FILE}" ]; then
        dd if=/dev/urandom of="${CRED_FILE}" bs=1024 count=1 conv=notrunc status=none 2>/dev/null || true
        rm -f "${CRED_FILE}"
    fi
}
trap cleanup EXIT INT TERM

# قالب credential.helper store: یک خط به شکل URL با کاربر و رمز
umask 077
printf 'https://x-access-token:%s@github.com\n' "${GITHUB_TOKEN}" > "${CRED_FILE}"

sanitize() { sed -e "s/${GITHUB_TOKEN}/***TOKEN-HIDDEN***/g"; }

# ---------- ۳) Push ----------
echo "[push] آدرس ریموت: $(git remote get-url origin | sanitize)"
if [ "${DRY_RUN}" = "1" ]; then
    echo "[push] حالت آزمایش (--dry-run): به GitHub وصل نمی‌شوم."
    echo "[push] آزمون دسترسی ناشناس به مخزن:"
    if git -c credential.helper= ls-remote --heads "${REMOTE_URL}" 2>&1 | sed 's/^/       /'; then
        echo "[push] مخزن در دسترس است."
    else
        echo "[push] هشدار: دسترسی ناشناس برقرار نشد."
    fi
    exit 0
fi

set +e
OUTPUT="$(git -c credential.helper="store --file=${CRED_FILE}" \
             -c core.askPass= \
             push origin "${BRANCH}" 2>&1)"
STATUS=$?
set -e
echo "${OUTPUT}" | sanitize | sed 's/^/       /'

if [ ${STATUS} -ne 0 ]; then
    echo "[push] ناموفق (کد ${STATUS}). توکن در هیچ فایلی باقی نمانده است." >&2
    exit ${STATUS}
fi

# ---------- ۴) تأیید پس از انتشار (اجرای واقعی، بدون اعتبارنامه) ----------
echo "[push] تأیید با دسترسی ناشناس (بدون توکن):"
REMOTE_SHA="$(git -c credential.helper= ls-remote --heads "${REMOTE_URL}" "${BRANCH}" | awk '{print $1}')"
LOCAL_SHA="$(git rev-parse "${BRANCH}")"
echo "       محلی:  ${LOCAL_SHA}"
echo "       ریموت: ${REMOTE_SHA}"
if [ "${REMOTE_SHA}" = "${LOCAL_SHA}" ]; then
    echo "[push] ✅ انتشار تأیید شد."
else
    echo "[push] ⚠️ هشدار: SHA ریموت با محلی یکسان نیست — بررسی لازم است." >&2
    exit 4
fi
