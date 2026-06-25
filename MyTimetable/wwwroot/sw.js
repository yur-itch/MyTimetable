const CACHE = 'schedule-v2';

self.addEventListener('install', e => {
    e.waitUntil(caches.open(CACHE).then(c => c.add('/App')));
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
    if (e.request.mode === 'navigate') {
        e.respondWith(
            fetch(e.request)
                .then(r => {
                    const copy = r.clone();
                    // Кэшируем под фиксированным ключом '/App' — тем же, что читает офлайн-фолбэк,
                    // чтобы каждая успешная онлайн-загрузка обновляла именно его, а не вмёрзший install-снимок.
                    caches.open(CACHE).then(c => c.put('/App', copy));
                    return r;
                })
                .catch(() => caches.match('/App'))
        );
    }
});
