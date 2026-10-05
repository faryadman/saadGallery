#!/usr/bin/env bash
# scripts/dev-setup.sh
# آماده‌سازی محیط توسعه SadGallery.
# • روی ماشین توسعه (Windows/macOS/Linux با دسترسی Admin): فقط بررسی پیش‌نیازها و راهنمایی نصب.
# • در سندباکس ایجنت (بدون SDK): نصب SDK در /opt/dotnet که «غیرماندگار» است و هر جلسه تکرار می‌شود.
#
# استفاده:  bash scripts/dev-setup.sh
set -euo pipefail

REQUIRED_MAJOR="10"
SDK_CHANNEL="10.0"

# انتخاب مسیر نصب: مسیر دلخواه ⇒ /opt/dotnet ⇒ $HOME/.local/dotnet (هر دو «غیرماندگار» در snapshot)
pick_dotnet_dir() {
    if [ -n "${DOTNET_DIR:-}" ]; then echo "${DOTNET_DIR}"; return; fi
    if mkdir -p /opt/dotnet 2>/dev/null || sudo -n mkdir -p /opt/dotnet 2>/dev/null; then
        sudo -n chown "$(id -u):$(id -g)" /opt/dotnet 2>/dev/null || true
        if [ -w /opt/dotnet ]; then echo "/opt/dotnet"; return; fi
    fi
    echo "${HOME}/.local/dotnet"   # .local از snapshot حذف می‌شود ⇒ ماندگار نیست (مطلوب)
}
DOTNET_DIR="$(pick_dotnet_dir)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

log()  { printf '\033[1;33m[setup]\033[0m %s\n' "$*"; }
ok()   { printf '\033[1;32m[ ok ]\033[0m %s\n' "$*"; }
fail() { printf '\033[1;31m[fail]\033[0m %s\n' "$*" >&2; }

echo "== SadGallery dev-setup =="
echo "مخزن: ${REPO_ROOT}"
echo "تاریخ: $(date -u '+%Y-%m-%d %H:%M:%SZ')"

# ---------- 1) بررسی پیش‌نیاز ----------
if command -v dotnet >/dev/null 2>&1; then
    VER="$(dotnet --version)"
    MAJOR="${VER%%.*}"
    log "dotnet یافت شد: ${VER}"
    if [ "${MAJOR}" != "${REQUIRED_MAJOR}" ]; then
        fail "نسخه SDK ${VER} با نیاز پروژه (${REQUIRED_MAJOR}.x) ناسازگار است."
        fail "نصب SDK 10 الزامی است. اگر نسخه‌های متعدد دارید، از global.json در ریشه پروژه استفاده کنید."
        exit 2
    fi
    ok "SDK سازگار است."
else
    if [ -w "$(dirname "${DOTNET_DIR}")" ] || [ "$(id -u)" = "0" ] || [ "${ALLOW_LOCAL_DOTNET_INSTALL:-0}" = "1" ]; then
        log "dotnet یافت نشد ⇒ نصب SDK ${SDK_CHANNEL} در ${DOTNET_DIR} (غیرماندگار)"
        if [ ! -x "${DOTNET_DIR}/dotnet" ]; then
            TMP="$(mktemp -d)"
            curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${TMP}/dotnet-install.sh"
            bash "${TMP}/dotnet-install.sh" --channel "${SDK_CHANNEL}" --install-dir "${DOTNET_DIR}" --no-path
            rm -rf "${TMP}"
        else
            log "SDK از قبل نصب است."
        fi
        export PATH="${DOTNET_DIR}:${PATH}"
        export DOTNET_ROOT="${DOTNET_DIR}"
        ok "نصب شد: $(dotnet --version)"
        log "توجه: این نصب بیرون از \$HOME است و در snapshot سندباکس ذخیره نمی‌شود."
    else
        fail "dotnet نصب نیست و امکان نصب خودکار وجود ندارد."
        echo "  راه‌حل: SDK 10 را از https://dotnet.microsoft.com/download/dotnet/10.0 نصب کنید"
        echo "  یا اسکریپت را با ALLOW_LOCAL_DOTNET_INSTALL=1 اجرا کنید."
        exit 2
    fi
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# کش NuGet بیرون از $HOME نگه داشته می‌شود تا snapshot فضای کار (سندباکس) با صدها مگابایت
# بسته پر نشود و پوشه‌های جانبی وارد Git نشوند. روی ماشین مالک، مقدار پیش‌فرض NuGet حفظ می‌شود.
case "${DOTNET_DIR}" in
  "${HOME}"/*) export NUGET_PACKAGES="${NUGET_PACKAGES:-${HOME}/.nuget/packages}" ;;
  *)           export NUGET_PACKAGES="${NUGET_PACKAGES:-$(dirname "${DOTNET_DIR}")/nuget-packages}" ;;
esac
export NUGET_HTTP_CACHE_PATH="${NUGET_HTTP_CACHE_PATH:-${NUGET_PACKAGES}.http}"
log "NUGET_PACKAGES=${NUGET_PACKAGES}"

# ---------- 2) بررسی SQL Server (فقط اطلاع‌رسانی) ----------
if command -v sqlcmd >/dev/null 2>&1; then
    ok "sqlcmd موجود است (SQL Server محلی احتمالی)."
else
    log "sqlcmd یافت نشد ⇒ تست‌های یکپارچه RequiresSqlServer در این محیط Skip می‌شوند."
    log "این محیط برای Build و Unit Test کافی است؛ تست DB روی ماشین شما/CI اجرا می‌شود."
fi

# ---------- 3) Build/Test (اگر Solution وجود دارد) ----------
if [ -f "${REPO_ROOT}/SadGallery.sln" ] || ls "${REPO_ROOT}"/*.sln >/dev/null 2>&1; then
    log "Solution یافت شد ⇒ restore و build"
    (cd "${REPO_ROOT}" && dotnet restore && dotnet build -warnaserror --nologo)
    ok "Build انجام شد. برای تست: bash scripts/test.sh"
else
    log "هنوز Solution وجود ندارد (فاز ۰). محیط آماده است تا فاز ۱ آغاز شود."
fi
ok "پایان dev-setup."
