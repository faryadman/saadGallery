/* Only public, explicit assets and the sanitized public-rate DTO may enter CacheStorage. */
'use strict';
const BUILD = __SG_BUILD__;
const ASSETS = __SG_ASSETS__;
const STATIC = 'sadgallery-static-' + BUILD;
const RATES = 'sadgallery-public-rates-v1';
const RATE_PATH = '/pwa/rates';
const allowed = new Map(ASSETS.map(asset => [asset.path, asset]));
let refreshInFlight;

async function network(request, milliseconds = 5000) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), milliseconds);
    const abort = () => controller.abort();
    request.signal?.addEventListener('abort', abort, { once: true });
    try { return await fetch(request, { signal: controller.signal, cache: 'no-store' }); }
    finally { clearTimeout(timer); request.signal?.removeEventListener('abort', abort); }
}
function validRates(value) {
    const date = x => typeof x === 'string' && Number.isFinite(Date.parse(x));
    const text = (x, max) => typeof x === 'string' && x.length > 0 && x.length <= max;
    return value && value.schema === 1 && date(value.capturedAtUtc) && date(value.fetchedAtUtc) &&
        typeof value.isSampleData === 'boolean' && Array.isArray(value.items) && value.items.length > 0 && value.items.length <= 100 &&
        value.items.every(x => x && /^[A-Z0-9_]{1,64}$/.test(x.assetCode) && text(x.title, 100) && text(x.amount, 100) &&
            text(x.unit, 20) && date(x.quotedAtUtc) && date(x.expiresAtUtc) && Date.parse(x.expiresAtUtc) > Date.parse(x.quotedAtUtc));
}
async function refreshRates() {
    if (refreshInFlight) return refreshInFlight;
    refreshInFlight = (async () => {
        try {
            const response = await network(new Request(RATE_PATH, { credentials: 'omit', redirect: 'error' }));
            if (!response.ok || response.headers.get('X-SadGallery-Public') !== 'rates-v1') return;
            const body = await response.text();
            if (new TextEncoder().encode(body).length > 65536) return;
            const value = JSON.parse(body);
            if (!validRates(value)) return;
            // Copy allowed fields into a new response: never persist original headers or extra fields.
            const safe = { schema: 1, capturedAtUtc: value.capturedAtUtc, fetchedAtUtc: value.fetchedAtUtc,
                isSampleData: value.isSampleData, items: value.items.map(x => ({ assetCode: x.assetCode, title: x.title,
                    amount: x.amount, unit: x.unit, quotedAtUtc: x.quotedAtUtc, expiresAtUtc: x.expiresAtUtc })) };
            const cache = await caches.open(RATES);
            await cache.put(RATE_PATH, new Response(JSON.stringify(safe), { headers: { 'Content-Type': 'application/json', 'X-SadGallery-Public': 'rates-v1' } }));
        } catch { /* Existing public data is retained, with its original timestamps and expiry. */ }
    })().finally(() => { refreshInFlight = null; });
    return refreshInFlight;
}
self.addEventListener('install', event => {
    event.waitUntil((async () => {
        const cache = await caches.open(STATIC);
        try {
            for (const asset of ASSETS) {
                const request = new Request(asset.path + '?pwa=' + asset.hash, { credentials: 'omit', redirect: 'error', integrity: asset.integrity });
                const response = await network(request, 15000);
                if (!response.ok || response.type !== 'basic') throw new Error('Incomplete public asset release');
                await cache.put(asset.path, response);
            }
        } catch (error) { await caches.delete(STATIC); throw error; }
        // No skipWaiting here: a new release must wait for the user's choice.
    })());
});
self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        for (const key of await caches.keys()) if (key.startsWith('sadgallery-static-') && key !== STATIC) await caches.delete(key);
        await self.clients.claim();
    })());
});
self.addEventListener('message', event => {
    // Only controlled same-origin windows may trigger a supported operation; no arbitrary URLs.
    if (!event.source || !event.source.url || new URL(event.source.url).origin !== self.location.origin) return;
    if (event.data?.type === 'GET_BUILD') event.ports[0]?.postMessage({ build: BUILD });
    if (event.data?.type === 'ACTIVATE_UPDATE') event.waitUntil(self.skipWaiting());
    if (event.data?.type === 'REFRESH_RATES') event.waitUntil(refreshRates());
    if (event.data?.type === 'READ_RATES') event.waitUntil((async () => {
        let value = null;
        try {
            const response = await (await caches.open(RATES)).match(RATE_PATH);
            if (response) { const stored = await response.json(); if (validRates(stored)) value = stored; }
        } catch { /* Empty state remains explicit. */ }
        event.ports[0]?.postMessage({ rates: value });
    })());
    if (event.data?.type === 'CLEAR_RATES') event.waitUntil((async () => {
        await caches.delete(RATES); event.ports[0]?.postMessage({ cleared: true });
    })());
});
self.addEventListener('fetch', event => {
    const request = event.request;
    const url = new URL(request.url);
    if (request.method !== 'GET' || url.origin !== self.location.origin) return;
    // No live HTML is stored, including guest pages that may contain antiforgery tokens.
    if (request.mode === 'navigate') {
        event.respondWith((async () => {
            try {
                const response = await network(request);
                if (response.status < 500) return response;
            } catch { /* A disconnected or failed server uses a public independent fallback. */ }
            return (await (await caches.open(STATIC)).match('/offline.html')) ||
                new Response('اتصال به سرور برقرار نیست؛ دوباره تلاش کنید.', { status: 503, headers: { 'Content-Type': 'text/plain; charset=utf-8' } });
        })());
        return;
    }
    const asset = allowed.get(url.pathname);
    if (!asset) return; // API, uploads, private images, version checks and POST are never cached.
    const keys = [...url.searchParams.keys()];
    const matchingVersion = keys.length === 0 || (keys.length === 1 &&
        (url.searchParams.get('v') === asset.hash || url.searchParams.get('pwa') === asset.hash));
    if (!matchingVersion) return;
    event.respondWith((async () => (await (await caches.open(STATIC)).match(asset.path)) ||
        network(new Request(request, { credentials: 'omit' })))());
});
