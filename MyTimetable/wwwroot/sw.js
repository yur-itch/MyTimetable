const CACHE = 'schedule-v3';

self.addEventListener('install', e => {
    e.waitUntil(caches.open(CACHE).then(c => c.addAll(['/App', '/App/Plan'])));
    self.skipWaiting();
});

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))
        )
    );
    self.clients.claim();
});

self.addEventListener('fetch', e => {
    if (e.request.mode !== 'navigate') return;
    // Network-first: при наличии сети всегда отдаём свежую страницу и обновляем офлайн-копию.
    // Кэш используется только как фолбэк, когда сеть недоступна (офлайн).
    e.respondWith(
        caches.open(CACHE).then(async cache => {
            try {
                const r = await fetch(e.request);
                cache.put(e.request, r.clone()); // держим офлайн-копию свежей
                return r;
            } catch {
                const cached = await cache.match(e.request);
                if (cached) return cached;
                throw new Error('offline and no cached page');
            }
        })
    );
});
