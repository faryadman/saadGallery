/* Rollback only: serve this file as /service-worker.js on the same HTTPS origin. */
'use strict';
self.addEventListener('install', event => event.waitUntil(self.skipWaiting()));
self.addEventListener('activate', event => event.waitUntil((async () => {
    for (const key of await caches.keys()) {
        if (key.startsWith('sadgallery-static-') || key === 'sadgallery-public-rates-v1') await caches.delete(key);
    }
    await self.registration.unregister();
    // No automatic navigation: drafts in existing tabs must remain intact.
})()));
