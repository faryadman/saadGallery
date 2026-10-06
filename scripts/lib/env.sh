#!/usr/bin/env bash
# scripts/lib/env.sh — محیط مشترک ابزارها برای اسکریپت‌های SadGallery
#
# چرا این فایل؟ سه اسکریپت (dev-setup / test / ef) هرکدام نسخه‌ای از منطق مسیرها را داشتند
# و در یک مورد ناهماهنگ بودند: بررسی «وجود» پوشه کش به‌جای «قابل‌نوشتن بودن» آن (BUG-006).
# یک منبع واحد = رفتار یکسان و رفع تکرار.
#
# استفاده:
#   source "$(dirname "${BASH_SOURCE[0]}")/lib/env.sh"

sadgallery_env() {
    # ---------- ۱) SDK دات‌نت ----------
    # در سندباکس ایجنت، SDK بیرون $HOME نصب می‌شود (غیرماندگار، هر جلسه نصب مجدد).
    if [ -x /opt/dotnet/dotnet ]; then
        case ":${PATH}:" in *":/opt/dotnet:"*) ;; *) export PATH="/opt/dotnet:${PATH}" ;; esac
        export DOTNET_ROOT="${DOTNET_ROOT:-/opt/dotnet}"
    fi

    # ابزار Entity Framework (در dev-setup نصب می‌شود)
    if [ -x /opt/dotnet/tools/dotnet-ef ]; then
        case ":${PATH}:" in *":/opt/dotnet/tools:"*) ;; *) export PATH="/opt/dotnet/tools:${PATH}" ;; esac
    fi

    export DOTNET_NOLOGO=1
    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

    # ---------- ۲) کش NuGet ----------
    # قاعده: کش باید «قابل نوشتن» باشد، نه فقط «موجود». اگر در $HOME باشد وارد snapshot
    # فضای کار می‌شود (صدها مگابایت) و ممکن است اشتباهاً کامیت شود — پس ترجیح: بیرون $HOME.
    local cache="${NUGET_PACKAGES:-}"
    if [ -z "${cache}" ]; then
        if [ -w /opt/nuget-packages ]; then
            cache=/opt/nuget-packages
        elif mkdir -p /opt/nuget-packages 2>/dev/null \
             || { sudo -n mkdir -p /opt/nuget-packages >/dev/null 2>&1 \
                  && sudo -n chown "$(id -u):$(id -g)" /opt/nuget-packages >/dev/null 2>&1; }; then
            cache=/opt/nuget-packages
        else
            cache=/tmp/nuget-packages
        fi
    fi

    # اعتبارسنجی نهایی: اگر مسیر داده‌شده قابل ساخت/نوشتن نبود، به /tmp منتقل شو (سقوط امن)
    if ! mkdir -p "${cache}" 2>/dev/null || [ ! -w "${cache}" ]; then
        cache=/tmp/nuget-packages
        mkdir -p "${cache}" 2>/dev/null || true
    fi

    export NUGET_PACKAGES="${cache}"
    # کش HTTP باید «داخل» مسیر قابل‌نوشتن باشد (BUG-005)
    export NUGET_HTTP_CACHE_PATH="${NUGET_HTTP_CACHE_PATH:-${cache}/.http-cache}"
    mkdir -p "${NUGET_HTTP_CACHE_PATH}" 2>/dev/null || true
}

sadgallery_env
