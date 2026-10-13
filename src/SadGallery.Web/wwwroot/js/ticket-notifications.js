(() => {
    'use strict';
    const badges = [...document.querySelectorAll('[data-ticket-badge]')];
    const watch = document.querySelector('[data-ticket-watch]');
    const scopes = [...new Set(badges.map(el => el.dataset.ticketScope))];
    const digits = new Intl.NumberFormat('fa-IR');
    let running = false;
    async function refresh() {
        if (running || document.hidden || !navigator.onLine) return;
        running = true;
        try {
            for (const scope of scopes) {
                const targets = badges.filter(el => el.dataset.ticketScope === scope);
                const ids = new Set(targets.map(el => el.dataset.ticketId).filter(Boolean));
                if (watch?.dataset.ticketScope === scope) ids.add(watch.dataset.ticketWatch);
                const query = new URLSearchParams({ operatorQueue: String(scope === 'operator') });
                [...ids].slice(0, 25).forEach(id => query.append('ticketIds', id));
                const response = await fetch('/tickets/unread?' + query, { credentials: 'same-origin', cache: 'no-store', redirect: 'error' });
                if (!response.ok) continue;
                const data = await response.json();
                for (const el of targets) {
                    const count = el.dataset.ticketId ? (data.tickets[el.dataset.ticketId] ?? 0) : data.total;
                    if (!Number.isInteger(count) || count < 0) continue;
                    el.hidden = count === 0;
                    el.textContent = digits.format(count) + (el.dataset.ticketId ? ' پیام جدید' : '');
                    el.setAttribute('aria-label', digits.format(count) + ' پیام خوانده‌نشده');
                }
                if (watch?.dataset.ticketScope === scope && data.tickets[watch.dataset.ticketWatch] > 0) watch.hidden = false;
            }
        } catch { /* Keep the last known badge during offline, login expiry or server errors. */ }
        finally { running = false; }
    }
    // Never reload the page automatically: drafted text and selected photos must remain intact.
    setInterval(refresh, 30000);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(); });
    window.addEventListener('online', refresh);
})();
