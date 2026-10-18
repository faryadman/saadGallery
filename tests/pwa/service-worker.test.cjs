const test = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const origin = 'https://gallery.example';
const template = fs.readFileSync(path.resolve(__dirname, '../../src/SadGallery.Web/Pwa/service-worker.js'), 'utf8');
const assets = [{ path: '/offline.html', hash: 'offline-hash', integrity: 'sha256-test' }, { path: '/css/site.css', hash: 'css-hash', integrity: 'sha256-test' }];
function harness(fetcher = async () => new Response('network')) {
    const events = {}, data = new Map(), requests = []; let activated = 0;
    class LocalRequest extends Request { constructor(url, init) { super(typeof url === 'string' ? new URL(url, origin) : url, init); } }
    const caches = {
        async keys() { return [...data.keys()]; }, async delete(key) { return data.delete(key); },
        async open(key) {
            if (!data.has(key)) data.set(key, new Map()); const entries = data.get(key);
            return { async match(request) { return entries.get(new URL(typeof request === 'string' ? request : request.url, origin).href)?.clone(); },
                async put(request, response) { entries.set(new URL(typeof request === 'string' ? request : request.url, origin).href, response.clone()); } };
        }
    };
    const self = { location: new URL(origin), clients: { async claim() {} }, addEventListener(name, handler) { events[name] = handler; }, async skipWaiting() { activated++; } };
    vm.runInNewContext(template.replace('__SG_BUILD__', JSON.stringify('test-build')).replace('__SG_ASSETS__', JSON.stringify(assets)),
        { self, caches, URL, Request: LocalRequest, Response, TextEncoder, AbortController, setTimeout, clearTimeout,
            fetch: async (request, options) => { requests.push({ request, options }); return fetcher(request, options); } });
    async function message(type, source = origin + '/tickets/new') { const waits = []; let reply; events.message({ data: { type }, source: { url: source }, ports: [{ postMessage(value) { reply = value; } }], waitUntil(p) { waits.push(p); } }); await Promise.all(waits); return reply; }
    async function navigate(url) { let result; events.fetch({ request: { method: 'GET', mode: 'navigate', url: origin + url, signal: new AbortController().signal }, respondWith(p) { result = p; } }); return result; }
    return { events, data, caches, requests, message, navigate, activated: () => activated };
}
const snapshot = () => ({ schema: 1, capturedAtUtc: '2026-10-19T10:00:00Z', fetchedAtUtc: '2026-10-19T09:00:00Z', isSampleData: false,
    items: [{ assetCode: 'GOLD_GRAM_18', title: 'طلای ۱۸ عیار', amount: '۱٬۲۳۴', unit: 'تومان', quotedAtUtc: '2026-10-19T09:00:00Z', expiresAtUtc: '2026-10-20T09:00:00Z' }] });

test('POST, cross-origin, private API and images never enter worker cache handling', () => {
    const h = harness();
    for (const request of [{ method: 'POST', url: origin + '/tickets' }, { method: 'GET', url: origin + '/tickets/private.png' },
        { method: 'GET', url: origin + '/api/rates/GOLD_GRAM_18/history' }, { method: 'GET', url: 'https://elsewhere.example/css/site.css' }])
        h.events.fetch({ request, respondWith() { assert.fail('Private request intercepted for caching'); } });
    assert.equal(h.data.size, 0);
});
test('live private HTML is network-only and a failed navigation uses only the independent fallback', async () => {
    const h = harness(async () => new Response('PrivateCanary-token-123')); const response = await h.navigate('/tickets/123');
    assert.equal(await response.text(), 'PrivateCanary-token-123'); assert.equal(h.data.size, 0); assert.equal(h.requests[0].options.cache, 'no-store');
    const unavailable = harness(async () => new Response('server-error', { status: 503 }));
    await (await unavailable.caches.open('sadgallery-static-test-build')).put('/offline.html', new Response('public offline shell'));
    assert.equal(await (await unavailable.navigate('/admin/users')).text(), 'public offline shell');
    assert.equal(unavailable.data.get('sadgallery-static-test-build').size, 1);
});
test('public snapshot uses no cookie and strips extra properties and original response headers', async () => {
    const h = harness(async () => new Response(JSON.stringify({ ...snapshot(), email: 'PrivateCanary@example.invalid', token: 'PrivateCanary-token' }),
        { headers: { 'X-SadGallery-Public': 'rates-v1', 'Set-Cookie': 'SecretCookieCanary' } }));
    await h.message('REFRESH_RATES'); assert.equal(h.requests[0].request.credentials, 'omit');
    const response = await (await h.caches.open('sadgallery-public-rates-v1')).match('/pwa/rates');
    assert.equal(response.headers.has('Set-Cookie'), false); const value = await response.text(); assert.equal(value.includes('PrivateCanary'), false);
    assert.deepEqual(JSON.parse(value), snapshot());
});
test('unmarked or malformed public responses preserve the last known safe snapshot', async () => {
    for (const response of [new Response(JSON.stringify(snapshot())), new Response('{"schema":1,"items":[]}', { headers: { 'X-SadGallery-Public': 'rates-v1' } })]) {
        const h = harness(async () => response); const cache = await h.caches.open('sadgallery-public-rates-v1');
        await cache.put('/pwa/rates', new Response(JSON.stringify(snapshot()))); await h.message('REFRESH_RATES');
        assert.deepEqual(await (await cache.match('/pwa/rates')).json(), snapshot());
    }
});
test('failed installation discards only the incomplete new release and never activates it', async () => {
    let count = 0;
    const h = harness(async () => { if (++count === 2) throw new Error('partial deployment'); const r = new Response('asset'); Object.defineProperty(r, 'type', { value: 'basic' }); return r; });
    await (await h.caches.open('sadgallery-static-previous')).put('/offline.html', new Response('previous'));
    let pending; h.events.install({ waitUntil(p) { pending = p; } }); await assert.rejects(pending);
    assert.equal(h.data.has('sadgallery-static-test-build'), false); assert.equal(h.data.has('sadgallery-static-previous'), true); assert.equal(h.activated(), 0);
});
test('activation requires a same-origin user message and only own obsolete static caches are removed', async () => {
    const h = harness(); await h.message('ACTIVATE_UPDATE', 'https://elsewhere.example/'); assert.equal(h.activated(), 0);
    await h.message('ACTIVATE_UPDATE'); assert.equal(h.activated(), 1);
    for (const name of ['sadgallery-static-previous', 'sadgallery-static-test-build', 'sadgallery-public-rates-v1', 'unrelated-origin-app']) await h.caches.open(name);
    let pending; h.events.activate({ waitUntil(p) { pending = p; } }); await pending;
    assert.equal(h.data.has('sadgallery-static-previous'), false); assert.equal(h.data.has('sadgallery-static-test-build'), true);
    assert.equal(h.data.has('sadgallery-public-rates-v1'), true); assert.equal(h.data.has('unrelated-origin-app'), true);
});
test('rollback worker unregisters and removes only SadGallery public caches without navigation', async () => {
    const names = new Set(['sadgallery-static-old', 'sadgallery-public-rates-v1', 'unrelated-app']); const events = {}; let unregistered = false, activated = false;
    vm.runInNewContext(fs.readFileSync(path.resolve(__dirname, '../../deployment/pwa-cleanup-worker.js'), 'utf8'), {
        self: { addEventListener(k, v) { events[k] = v; }, async skipWaiting() { activated = true; }, registration: { async unregister() { unregistered = true; } } },
        caches: { async keys() { return [...names]; }, async delete(key) { names.delete(key); } }
    });
    let pending; events.install({ waitUntil(p) { pending = p; } }); await pending; assert.equal(activated, true);
    events.activate({ waitUntil(p) { pending = p; } }); await pending; assert.equal(unregistered, true); assert.deepEqual([...names], ['unrelated-app']);
});
