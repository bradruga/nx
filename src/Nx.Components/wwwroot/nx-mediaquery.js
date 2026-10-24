// Interop for NxMediaQuery: a CSS media query's match state, reported now and on every change. The browser
// knows (matchMedia); .NET cannot ask without it.

const queries = new Map();
let nextId = 0;

export function watch(media, dotNet) {
    const list = window.matchMedia(media);
    const onChange = e => dotNet.invokeMethodAsync('OnMediaChanged', e.matches);
    list.addEventListener('change', onChange);
    const id = ++nextId;
    queries.set(id, { list, onChange });
    return { id, matches: list.matches };
}

export function unwatch(id) {
    const query = queries.get(id);
    if (!query) return;
    query.list.removeEventListener('change', query.onChange);
    queries.delete(id);
}
