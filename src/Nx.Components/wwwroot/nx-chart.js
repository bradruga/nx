// Interop for NxChart. The chart is drawn by .NET; here: the plot's size as it resizes (a ResizeObserver), the point
// under the pointer — the column, bar or slice hit, else the line point nearest in the category under it — reported
// only when it changes, a click or tap on it, and the page-scroll default of the keys that move between points.

import { preventKeys, dispose as disposeInput } from './nx-input.js';

const states = new WeakMap();

const sizeOf = plot => {
    const r = plot.getBoundingClientRect();
    return [Math.round(r.width), Math.round(r.height)];
};

const keyOf = el => el?.getAttribute('data-p')?.split('-').map(Number) ?? null;

/** The point under the pointer: a shape hit, else the nearest marker within half a category of it. */
function pointAt(plot, e) {
    const shape = e.target.closest?.('rect[data-p], path[data-p]');
    if (shape && plot.contains(shape)) return keyOf(shape);
    const svg = plot.querySelector('svg');
    if (!svg) return null;
    const r = svg.getBoundingClientRect();
    const x = e.clientX - r.left, y = e.clientY - r.top;
    const band = parseFloat(plot.dataset.band) || 40;
    let best = null, bestDx = Infinity, bestDy = Infinity;
    for (const m of plot.querySelectorAll('circle[data-p]')) {
        // A drawn marker wins over a series drawn without markers (a limit line) that runs within a few pixels.
        const dx = Math.abs(m.cx.baseVal.value - x), dy = Math.abs(m.cy.baseVal.value - y) + (m.r.baseVal.value === 0 ? 6 : 0);
        if (dx > band / 2 + 1) continue;
        if (dx < bestDx - 0.5 || (Math.abs(dx - bestDx) <= 0.5 && dy < bestDy)) {
            best = m; bestDx = dx; bestDy = dy;
        }
    }
    return keyOf(best);
}

export function observe(plot, keys, dotNet) {
    if (!plot) return [0, 0];
    const state = { dotNet, hovered: null, frame: 0 };
    const report = key => {
        const id = key ? key.join('-') : '';
        if (id === state.hovered) return;
        state.hovered = id;
        dotNet.invokeMethodAsync('OnHover', key ? key[0] : -1, key ? key[1] : -1);
    };
    state.observer = new ResizeObserver(() => {
        cancelAnimationFrame(state.frame);
        state.frame = requestAnimationFrame(() => {
            const [w, h] = sizeOf(plot);
            dotNet.invokeMethodAsync('OnResize', w, h);
        });
    });
    state.observer.observe(plot);
    state.onMove = e => report(pointAt(plot, e));
    state.onLeave = e => { if (e.pointerType === 'mouse') report(null); };
    state.onClick = e => {
        const key = pointAt(plot, e);
        if (key) dotNet.invokeMethodAsync('OnPointClick', key[0], key[1]);
    };
    plot.addEventListener('pointermove', state.onMove);
    plot.addEventListener('pointerdown', state.onMove);
    plot.addEventListener('pointerleave', state.onLeave);
    plot.addEventListener('click', state.onClick);
    preventKeys(plot, keys);
    states.set(plot, state);
    return sizeOf(plot);
}

export function measure(plot) {
    return plot ? sizeOf(plot) : [0, 0];
}

export function dispose(plot) {
    const state = plot && states.get(plot);
    if (!state) return;
    state.observer.disconnect();
    cancelAnimationFrame(state.frame);
    plot.removeEventListener('pointermove', state.onMove);
    plot.removeEventListener('pointerdown', state.onMove);
    plot.removeEventListener('pointerleave', state.onLeave);
    plot.removeEventListener('click', state.onClick);
    disposeInput(plot);
    states.delete(plot);
}
