// Single owner of the connection notice and conservative rate freshness labels.
// Browser online/offline signals describe network connectivity, not server health.
(() => {
    'use strict';
    const notice = document.getElementById('sg-connection-notice');
    const title = notice?.querySelector('[data-connection-title]');
    const description = notice?.querySelector('[data-connection-description]');
    const dismiss = notice?.querySelector('[data-connection-dismiss]');
    const messages = notice?.dataset;
    let wasOffline = false;
    let hideTimer;

    function markRatesStored() {
        document.querySelectorAll('[data-sg-quality="live"]').forEach(badge => {
            badge.textContent = 'آخرین نرخ ثبت‌شده';
            badge.dataset.sgQuality = 'offline';
            badge.classList.remove('text-bg-success');
            badge.classList.add('text-bg-warning');
        });
    }
    function show(state, heading, detail) {
        clearTimeout(hideTimer);
        if (!notice || !title || !description) return;
        notice.dataset.state = state;
        notice.hidden = false;
        title.textContent = heading;
        description.textContent = detail;
    }
    function applyConnectionState() {
        const offline = navigator.onLine === false;
        document.body.classList.toggle('sg-offline', offline);
        if (offline) {
            markRatesStored();
            if (!wasOffline && messages) show('offline', messages.offlineTitle, messages.offlineDescription);
        } else if (wasOffline && messages) {
            // Do not label old values live merely because the browser reconnected.
            show('online', messages.onlineTitle, messages.onlineDescription);
            hideTimer = setTimeout(() => {
                if (notice && !notice.contains(document.activeElement)) notice.hidden = true;
            }, 5000);
        }
        wasOffline = offline;
    }
    dismiss?.addEventListener('click', () => {
        clearTimeout(hideTimer);
        notice.hidden = true;
    });
    window.addEventListener('online', applyConnectionState);
    window.addEventListener('offline', applyConnectionState);
    window.addEventListener('pageshow', applyConnectionState);
    applyConnectionState();
})();
