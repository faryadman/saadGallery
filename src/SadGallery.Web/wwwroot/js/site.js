// ============================================================================
// SadGallery — اسکریپت پایه سایت
// قاعده پروژه: منطق تجاری در JavaScript نوشته نمی‌شود.
// این فایل فقط بهبود تجربه کاربری (UX) را انجام می‌دهد و هرگز مبنای تصمیم‌های
// امنیتی یا محاسبات مالی نیست؛ آن‌ها در سرور (Domain/Application) انجام می‌شوند.
// ============================================================================

(function () {
    "use strict";

    // نمایش وضعیت اتصال (پیش‌نیاز رفتار آفلاین در فاز ۷ - PWA)
    function updateConnectionState() {
        const offline = !navigator.onLine;
        document.body.classList.toggle("sg-offline", offline);

        if (offline) {
            // هشدار سراسری فقط یک‌بار ساخته می‌شود
            if (!document.getElementById("sg-offline-banner")) {
                const banner = document.createElement("div");
                banner.id = "sg-offline-banner";
                banner.className = "alert alert-secondary text-center mb-0 rounded-0";
                banner.setAttribute("role", "status");
                banner.textContent = "اتصال اینترنت قطع است. نرخ‌ها در حالت آفلاین «ذخیره‌شده» هستند و نرخ لحظه‌ای محسوب نمی‌شوند.";
                document.body.prepend(banner);
            }
        } else {
            const existing = document.getElementById("sg-offline-banner");
            if (existing) {
                existing.remove();
            }
        }
    }

    window.addEventListener("online", updateConnectionState);
    window.addEventListener("offline", updateConnectionState);
    updateConnectionState();
})();
