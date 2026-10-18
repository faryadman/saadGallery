(() => {
    'use strict';
    const banner = document.getElementById('sg-pwa-update');
    const heading = banner?.querySelector('[data-pwa-update-title]');
    const description = banner?.querySelector('[data-pwa-update-text]');
    const apply = banner?.querySelector('[data-pwa-update]');
    const later = banner?.querySelector('[data-pwa-update-later]');
    const recall = document.querySelector('[data-pwa-show-update]');
    const install = document.querySelector('[data-pwa-install]');
    const status = document.querySelector('[data-pwa-install-status]');
    const check = document.querySelector('[data-pwa-check-update]');
    let registration, waitingBuild, updateTarget, deferredInstall, dirty = false, confirmed = false, reloadRequested = false;
    let lastCheck = 0, dismissedBuild = '', checkInFlight = false;
    const pageBuild = document.body.dataset.pwaBuild;
    const setStatus = text => { if (status) status.textContent = text; };
    const dirtyForm = () => dirty || [...document.querySelectorAll('input[type="file"]')].some(x => x.files?.length) ||
        [...document.querySelectorAll('form[data-ticket-compose] textarea')].some(x => x.value.trim());
    document.addEventListener('input', event => { if (event.target.closest('form')) dirty = true; }, true);
    document.addEventListener('change', event => { if (event.target.closest('form')) dirty = true; }, true);
    document.addEventListener('submit', event => {
        const form = event.target;
        if (form instanceof HTMLFormElement && form.method.toLowerCase() === 'post' && navigator.onLine === false) {
            event.preventDefault(); event.stopImmediatePropagation();
            let error = form.querySelector('[data-pwa-offline-form]');
            if (!error) { error = document.createElement('p'); error.className = 'alert alert-warning'; error.dataset.pwaOfflineForm = ''; error.setAttribute('role', 'alert'); form.prepend(error); }
            error.textContent = 'اتصال برقرار نیست؛ ارسال انجام نشد. متن و فایل انتخاب‌شده در همین صفحه حفظ شده‌اند. پس از اتصال دوباره ارسال کنید.';
            error.tabIndex = -1; error.focus();
        }
    }, true);
    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault(); deferredInstall = event;
        if (install) install.hidden = false;
        setStatus('نصب برنامه برای این مرورگر آماده است.');
    });
    install?.addEventListener('click', async () => {
        if (!deferredInstall) return;
        install.disabled = true;
        try { await deferredInstall.prompt(); const choice = await deferredInstall.userChoice; setStatus(choice.outcome === 'accepted' ? 'درخواست نصب ثبت شد.' : 'می‌توانید بعداً دوباره نصب کنید.'); }
        finally { deferredInstall = null; install.hidden = true; install.disabled = false; }
    });
    window.addEventListener('appinstalled', () => { if (install) install.hidden = true; deferredInstall = null; setStatus('برنامه نصب شد؛ آن را از صفحه اصلی دستگاه باز کنید.'); });
    function ask(worker, type) {
        return new Promise(resolve => {
            if (!worker) { resolve(null); return; }
            const channel = new MessageChannel();
            const timer = setTimeout(() => { channel.port1.close(); resolve(null); }, 3000);
            channel.port1.onmessage = event => { clearTimeout(timer); channel.port1.close(); resolve(event.data); };
            try { worker.postMessage({ type }, [channel.port2]); } catch { clearTimeout(timer); channel.port1.close(); resolve(null); }
        });
    }
    async function showUpdate(worker, alreadyActive = false) {
        if (!banner || !worker) return;
        const info = await ask(worker, 'GET_BUILD');
        if (!info?.build || info.build === dismissedBuild) return;
        waitingBuild = info.build; updateTarget = alreadyActive ? null : worker; confirmed = false;
        heading.textContent = alreadyActive ? 'نسخه جدید فعال شده است' : 'نسخه جدید آماده است';
        description.textContent = 'هر زمان آماده بودید، صفحه را تازه کنید تا نسخه جدید را ببینید.';
        apply.textContent = alreadyActive ? 'تازه‌سازی صفحه' : 'به‌روزرسانی برنامه';
        later.textContent = 'بعداً'; apply.disabled = false;
        banner.hidden = false;
        if (recall) recall.hidden = false;
    }
    recall?.addEventListener('click', () => { dismissedBuild = ''; showUpdate(registration?.waiting || navigator.serviceWorker.controller, !registration?.waiting); });
    later?.addEventListener('click', () => { dismissedBuild = waitingBuild; confirmed = false; banner.hidden = true; });
    apply?.addEventListener('click', async () => {
        if (dirtyForm() && !confirmed) {
            confirmed = true;
            heading.textContent = 'متن یا فایل ذخیره‌نشده دارید';
            description.textContent = 'با تازه‌سازی، ورودی‌های ذخیره‌نشده این صفحه پاک می‌شوند. ابتدا آن‌ها را ارسال یا ذخیره کنید، یا آگاهانه ادامه دهید.';
            apply.textContent = 'ذخیره نشده؛ تازه‌سازی شود'; later.textContent = 'بازگشت به فرم';
            return;
        }
        apply.disabled = true; reloadRequested = true;
        if (updateTarget) {
            const worker = registration?.waiting;
            if (!worker || worker !== updateTarget) { reloadRequested = false; apply.disabled = false; await showUpdate(navigator.serviceWorker.controller, true); return; }
            worker.postMessage({ type: 'ACTIVATE_UPDATE' });
            setTimeout(() => {
                if (!reloadRequested) return;
                reloadRequested = false; apply.disabled = false;
                description.textContent = 'به‌روزرسانی هنوز کامل نشده است؛ ورودی‌ها حفظ شده‌اند. دوباره تلاش کنید.';
            }, 10000);
        } else window.location.reload();
    });
    if (!('serviceWorker' in navigator) || !window.isSecureContext) {
        setStatus('نصب و اجرای آفلاین به HTTPS معتبر نیاز دارد. روی HTTP آی‌پی شبکه خانه فقط اجرای عادی سایت ممکن است.');
        if (check) check.disabled = true;
        return;
    }
    navigator.serviceWorker.addEventListener('controllerchange', async () => {
        if (reloadRequested) { window.location.reload(); return; }
        const info = await ask(navigator.serviceWorker.controller, 'GET_BUILD');
        // Other tabs must keep their drafts, even when another tab activates the update.
        if (pageBuild && info?.build && info.build !== pageBuild) await showUpdate(navigator.serviceWorker.controller, true);
    });
    async function checkUpdate(force = false) {
        if (!registration || checkInFlight || !navigator.onLine || document.hidden || (!force && Date.now() - lastCheck < 60000)) return;
        checkInFlight = true; lastCheck = Date.now();
        try {
            if (force) { dismissedBuild = ''; setStatus('در حال بررسی نسخه جدید…'); }
            await registration.update();
            if (registration.waiting) { await showUpdate(registration.waiting); if (force) setStatus('نسخه جدید آماده است؛ از اعلان به‌روزرسانی استفاده کنید.'); }
            else if (force) setStatus('بررسی انجام شد؛ در صورت آماده‌شدن نسخه جدید اعلان نمایش داده می‌شود.');
        } catch { if (force) setStatus('بررسی نسخه ممکن نشد؛ اتصال به سرور را بررسی کنید.'); }
        finally { checkInFlight = false; }
    }
    check?.addEventListener('click', () => checkUpdate(true));
    navigator.serviceWorker.register('/service-worker.js', { scope: '/', updateViaCache: 'none' }).then(async value => {
        registration = value;
        const installed = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
        setStatus(installed ? 'برنامه در حالت نصب‌شده اجرا می‌شود.' : 'می‌توانید برنامه را از منوی مرورگر نصب کنید؛ راهنمای دستگاه را در ادامه ببینید.');
        const watch = () => {
            const worker = registration.installing;
            worker?.addEventListener('statechange', () => { if (worker.state === 'installed' && registration.waiting && navigator.serviceWorker.controller) showUpdate(registration.waiting); });
        };
        watch(); registration.addEventListener('updatefound', watch);
        if (registration.waiting) await showUpdate(registration.waiting);
        const ready = await navigator.serviceWorker.ready;
        if (navigator.onLine) ready.active?.postMessage({ type: 'REFRESH_RATES' });
        setInterval(() => {
            checkUpdate();
            if (!document.hidden && navigator.onLine) registration.active?.postMessage({ type: 'REFRESH_RATES' });
        }, 60000);
    }).catch(() => setStatus('آماده‌سازی نصب و حالت آفلاین ممکن نشد؛ اتصال و آدرس امن سایت را بررسی کنید.'));
    window.addEventListener('online', () => { checkUpdate(); registration?.active?.postMessage({ type: 'REFRESH_RATES' }); });
    document.addEventListener('visibilitychange', () => { if (!document.hidden) checkUpdate(); });
})();
