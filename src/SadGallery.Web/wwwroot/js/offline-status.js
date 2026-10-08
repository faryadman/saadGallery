// ============================================================================
// offline-status.js — تشخیص قطعی اتصال مرورگر (فاز ۳)
//
// قاعده پروژه: برای «داده ذخیره‌شده» هرگز واژه «لحظه‌ای» استفاده نمی‌شود.
// وقتی اتصال کاربر قطع است، صفحه ممکن است از کش مرورگر نمایش داده شود؛
// این اسکریپت:
//   ۱) نوار هشدار واضح نشان می‌دهد («نرخ‌های نمایش‌داده‌شده ذخیره‌شده‌اند»).
//   ۲) برچسب‌های «لحظه‌ای» را به «آخرین نرخ ثبت‌شده» تغییر می‌دهد تا صادق بماند.
// بدون هیچ منبع خارجی — بخشی از دارایی‌های خود سایت.
// ============================================================================
(function () {
    "use strict";

    var STALE_LABEL = "آخرین نرخ ثبت‌شده";

    function applyOfflineState() {
        var offline = navigator.onLine === false;
        document.body.classList.toggle("sg-offline", offline);

        var badges = document.querySelectorAll("[data-sg-quality='live']");
        for (var i = 0; i < badges.length; i++) {
            if (offline) {
                if (!badges[i].dataset.sgOriginalLabel) {
                    badges[i].dataset.sgOriginalLabel = badges[i].textContent;
                }
                badges[i].textContent = STALE_LABEL;
            } else if (badges[i].dataset.sgOriginalLabel) {
                badges[i].textContent = badges[i].dataset.sgOriginalLabel;
                delete badges[i].dataset.sgOriginalLabel;
            }
        }
    }

    window.addEventListener("online", applyOfflineState);
    window.addEventListener("offline", applyOfflineState);

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", applyOfflineState);
    } else {
        applyOfflineState();
    }
})();
