(() => {
    'use strict';
    const list = document.querySelector('[data-offline-rates]');
    const empty = document.querySelector('[data-offline-empty]');
    const time = document.querySelector('[data-offline-time]');
    const sample = document.querySelector('[data-offline-sample]');
    const format = value => {
        try { return new Intl.DateTimeFormat('fa-IR', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Tehran' }).format(new Date(value)); }
        catch { return value; }
    };
    async function send(type) {
        if (!navigator.serviceWorker?.controller) return null;
        return new Promise(resolve => {
            const channel = new MessageChannel();
            const timer = setTimeout(() => { channel.port1.close(); resolve(null); }, 3000);
            channel.port1.onmessage = event => { clearTimeout(timer); channel.port1.close(); resolve(event.data); };
            navigator.serviceWorker.controller.postMessage({ type }, [channel.port2]);
        });
    }
    function element(tag, text, className) { const node = document.createElement(tag); node.textContent = text; if (className) node.className = className; return node; }
    async function render() {
        const { rates } = await send('READ_RATES') || {};
        list.replaceChildren(); time.textContent = ''; sample.hidden = true;
        const valid = rates?.items?.filter(x => Date.parse(x.expiresAtUtc) > Date.now()) || [];
        empty.hidden = valid.length > 0;
        empty.textContent = rates?.items?.length ? 'اطلاعات ذخیره‌شده منقضی شده است؛ برای دریافت نرخ معتبر به سرور متصل شوید.' : 'هنوز نرخ عمومی مجازی روی این دستگاه ذخیره نشده است. پس از اولین اتصال موفق، نرخ‌ها در دسترس قرار می‌گیرند.';
        if (!valid.length) return;
        sample.hidden = !rates.isSampleData;
        time.textContent = 'آخرین دریافت منبع: ' + format(rates.fetchedAtUtc) + ' · ذخیره عمومی: ' + format(rates.capturedAtUtc);
        for (const rate of valid) {
            const column = element('div', '', 'col-12 col-md-6 col-xl-4');
            const card = element('article', '', 'sg-card h-100');
            card.append(element('h3', rate.title, 'h6'), element('p', rate.amount + ' ' + rate.unit, 'fs-5 fw-bold'),
                element('span', 'ذخیره‌شده / آفلاین', 'badge text-bg-warning'), element('p', 'زمان اعلام: ' + format(rate.quotedAtUtc), 'small text-muted mt-2 mb-0'));
            column.append(card); list.append(column);
        }
    }
    document.querySelector('[data-offline-retry]')?.addEventListener('click', () => window.location.reload());
    document.querySelector('[data-offline-clear]')?.addEventListener('click', async () => { await send('CLEAR_RATES'); await render(); });
    navigator.serviceWorker?.addEventListener('controllerchange', render);
    render();
})();
