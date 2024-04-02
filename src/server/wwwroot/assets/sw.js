
// Not a huge benefit in this code, with the huge downside that we now introduce
// caching problems.

const cacheName = 'cpaidb-2022.12.22-2'

self.addEventListener('install', (e) => {
    console.log('[Service Worker] Install');
    e.waitUntil((async () => {
        const cache = await caches.open(cacheName);
        console.log('[Service Worker] Caching all: app shell and content');

        await cache.add('dashboard.js');
        await cache.add('server.css');
        await cache.add('assets/bootstrap-dark.min.css');
        await cache.add('assets/bootstrap.min.css');
    })());
});

self.addEventListener('fetch', (e) => {
    e.respondWith((async () => {

        // Don't cache our server API calls
        if (e.request.url.indexOf('/v1/') >= 0)
            return await fetch(e.request);

        const resource = await caches.match(e.request);
        console.log(`[Service Worker] Fetching resource: ${e.request.url}`);
        if (resource) return resource;
        
        const response = await fetch(e.request);
        const cache    = await caches.open(cacheName);

        console.log(`[Service Worker] Caching new resource: ${e.request.url}`);
        cache.put(e.request, response.clone());
        return response;
    })());
});

self.addEventListener('activate', (e) => {
    e.waitUntil(caches.keys().then((keyList) => {
        return Promise.all(keyList.map((key) => {
            if (key === cacheName) return;
            return caches.delete(key);
        }));
    }));
});