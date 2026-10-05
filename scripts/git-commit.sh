#!/usr/bin/env bash
# scripts/git-commit.sh — کامیت با هویت ثابت پروژه SadGallery
#
# چرا این اسکریپت وجود دارد؟
#   در محیط ایجنت، فایل .git/config بین فراخوانی‌ها حفظ نمی‌شود (بخشی از snapshot نیست).
#   بنابراین `git config user.name ...` در یک فراخوانی، در فراخوانی بعدی از دست می‌رود و
#   کامیت با خطای «Author identity unknown / empty ident name» شکست می‌خورد.
#   راه‌حل: هویت را در همان دستور کامیت با `-c` بدهیم. این اسکریپت همین کار را می‌کند
#   و همچنین ریموت رسمی را در صورت نبود، بازسازی می‌کند.
#
# استفاده:
#   bash scripts/git-commit.sh "docs(status): به‌روزرسانی وضعیت فاز"
#   bash scripts/git-commit.sh "feat(market): افزودن Provider" -- file1 file2
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [ -z "${REPO_ROOT}" ]; then
    echo "[git-commit] خطا: در یک مخزن Git نیستیم." >&2
    exit 2
fi
cd "${REPO_ROOT}"

if [ $# -lt 1 ]; then
    echo "استفاده: bash scripts/git-commit.sh \"پیام کامیت\" [-- <مسیرها>]" >&2
    exit 2
fi

MSG="$1"; shift

# ریموت رسمی را در صورت نبود بازسازی کن (idempotent، بدون هیچ توکنی)
if ! git remote get-url origin >/dev/null 2>&1; then
    git remote add origin https://github.com/faryadman/saadGallery.git
    echo "[git-commit] ریموت origin بازسازی شد."
fi

# هویت و تنظیمات امنیتی در همان دستور (بدون نوشتن در .git/config)
git -c user.name="SadGallery Agent" \
    -c user.email="agent@sadgallery.local" \
    -c commit.gpgsign=false \
    commit -m "${MSG}" "$@"

echo "[git-commit] انجام شد: $(git log --oneline -1)"
