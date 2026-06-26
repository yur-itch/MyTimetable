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
    if (e.request.mode !== 'navigate') return;
    // Stale-while-revalidate: мгновенно отдаём кэш (без сетевого RTT на критическом пути),
    // параллельно обновляем его из сети — свежесть подтянется к следующему заходу.
    // Кэша нет (первый визит) — ждём сеть. Сеть недоступна — остаётся кэш (офлайн).
    e.respondWith(
        caches.open(CACHE).then(async cache => {
            const cached = await cache.match('/App');
            const network = fetch(e.request)
                .then(r => {
                    // Под фиксированным ключом '/App' — тем же, что читает кэш-хит,
                    // чтобы каждая успешная загрузка обновляла именно его, а не вмёрзший install-снимок.
                    cache.put('/App', r.clone());
                    return r;
                })
                .catch(() => cached);
            return cached || network;
        })
    );
});
