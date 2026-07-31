const CACHE_NAMES = {
    viewer: 'schedule-viewer-v1',
    editor: 'schedule-editor-v1',
};
const CACHEABLE_PATHS = new Set(['/App', '/App/Plan']);
let activeMode = null;

function cacheName(mode) {
    return CACHE_NAMES[mode] ?? null;
}

self.addEventListener('install', e => {
    self.skipWaiting();
});

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys
                .filter(k => !Object.values(CACHE_NAMES).includes(k))
                .map(k => caches.delete(k)))
        )
    );
    self.clients.claim();
});

self.addEventListener('message', e => {
    if (e.data?.type !== 'set-mode') return;
    activeMode = e.data.mode === 'viewer' || e.data.mode === 'editor'
        ? e.data.mode
        : null;
});

self.addEventListener('fetch', e => {
    if (e.request.mode !== 'navigate') return;

    const url = new URL(e.request.url);
    if (!CACHEABLE_PATHS.has(url.pathname)) return;

    e.respondWith((async () => {
        try {
            const response = await fetch(e.request);
            const responseMode = response.headers.get('X-UI-Mode');
            const mode = responseMode === 'viewer' || responseMode === 'editor'
                ? responseMode
                : activeMode;

            if (response.status === 401 || response.status === 403) {
                activeMode = null;
                return response;
            }

            if (response.ok && mode && (response.headers.get('content-type') || '').includes('text/html')) {
                activeMode = mode;
                const cache = await caches.open(cacheName(mode));
                await cache.put(e.request, response.clone());
            }
            return response;
        } catch {
            const name = cacheName(activeMode);
            if (name) {
                const cached = await caches.open(name).then(cache => cache.match(e.request));
                if (cached) return cached;
            }

            return new Response('Офлайн-версия недоступна', {
                status: 503,
                headers: { 'Content-Type': 'text/plain; charset=utf-8' },
            });
        }
    })());
});
