// Interop for NxColorGradient. A drag in the saturation and brightness area is followed here: the pointer is
// captured, the thumb moves at once (through the area's CSS variables), and .NET hears where it is at most once a
// frame and only after it answered the last call, so a slow connection never queues up stale positions. The keys
// the thumb uses have their page-scrolling default cancelled.

import { preventKeys, dispose as disposeInput } from './nx-input.js';

const states = new WeakMap();

export function init(area, thumb, keys, dotNet) {
    if (!area || states.has(area)) return;
    const root = area.parentElement;
    let pending = null, busy = false;

    const send = () => {
        if (busy || !pending) return;
        const [s, v] = pending;
        pending = null;
        busy = true;
        dotNet.invokeMethodAsync('OnAreaPick', s, v).finally(() => { busy = false; send(); });
    };
    const pick = e => {
        const r = area.getBoundingClientRect();
        const s = Math.min(1, Math.max(0, (e.clientX - r.left) / r.width));
        const v = Math.min(1, Math.max(0, 1 - (e.clientY - r.top) / r.height));
        root.style.setProperty('--nx-colorgradient-x', `${s * 100}%`);
        root.style.setProperty('--nx-colorgradient-y', `${(1 - v) * 100}%`);
        pending = [Math.round(s * 1000) / 1000, Math.round(v * 1000) / 1000];
        requestAnimationFrame(send);
    };
    const onDown = e => {
        if (e.button !== 0 || root.classList.contains('nx-colorgradient-disabled')) return;
        e.preventDefault();
        area.setPointerCapture(e.pointerId);
        thumb.focus({ preventScroll: true });
        pick(e);
    };
    const onMove = e => { if (area.hasPointerCapture(e.pointerId)) pick(e); };

    area.addEventListener('pointerdown', onDown);
    area.addEventListener('pointermove', onMove);
    preventKeys(thumb, keys);
    states.set(area, { thumb, onDown, onMove });
}

export function dispose(area) {
    const state = area && states.get(area);
    if (!state) return;
    area.removeEventListener('pointerdown', state.onDown);
    area.removeEventListener('pointermove', state.onMove);
    disposeInput(state.thumb);
    states.delete(area);
}
