// NxSplitter interop. Blazor owns the markup; this module drags the split bars and moves them with the
// keyboard — in the browser, so the panes follow the pointer without a server round trip per move — and
// reports the final size of the pane that changed. Collapsing and the bound sizes are C#.
// See NxSplitter.razor.cs, "JS interop".

const states = new WeakMap();
const STEP = 10, BIG_STEP = 50;     // px per arrow key; with Shift

const horizontal = root => root.dataset.orientation === 'horizontal';
const isBar = (root, el) => el?.classList.contains('nx-splitter-bar') && el.parentElement === root;

function length(value, total) {
    if (!value) return null;
    return value.endsWith('%') ? total * parseFloat(value) / 100 : parseFloat(value);
}

/** The pane a bar moves: the one before it if that has a size (or neither has), else the one after. */
function target(bar) {
    const prev = bar.previousElementSibling, next = bar.nextElementSibling;
    if (!prev || !next) return null;
    if (prev.dataset.collapsed === 'true' || next.dataset.collapsed === 'true') return null;
    const usePrev = prev.dataset.size || !next.dataset.size;
    return { pane: usePrev ? prev : next, sign: usePrev ? 1 : -1, prev };
}

function measure(root, pane) {
    const h = horizontal(root);
    const total = h ? root.clientWidth : root.clientHeight;
    const current = h ? pane.getBoundingClientRect().width : pane.getBoundingClientRect().height;
    const min = length(pane.dataset.min, total) ?? 0;
    const max = length(pane.dataset.max, total) ?? total;
    return { total, current, min, max };
}

function apply(pane, px) {
    pane.style.flex = `0 0 ${px}px`;
}

function sizeText(pane, px, total) {
    return pane.dataset.unit === '%' ? `${Math.round(px / total * 10000) / 100}%` : `${Math.round(px)}px`;
}

function updateAria(root) {
    for (const bar of root.children) {
        if (!isBar(root, bar)) continue;
        const prev = bar.previousElementSibling;
        if (!prev) continue;
        const h = horizontal(root);
        const total = h ? root.clientWidth : root.clientHeight;
        const size = h ? prev.getBoundingClientRect().width : prev.getBoundingClientRect().height;
        bar.setAttribute('aria-valuenow', total > 0 ? String(Math.round(size / total * 100)) : '0');
    }
}

function report(state, pane, px, total) {
    updateAria(state.root);
    state.dotNet.invokeMethodAsync('OnPaneResized', parseInt(pane.dataset.index, 10), sizeText(pane, px, total));
}

function onPointerDown(state, e) {
    const bar = e.target.closest('.nx-splitter-bar');
    if (!isBar(state.root, bar) || e.target.closest('button') || e.button !== 0) return;
    const t = target(bar);
    if (!t || t.pane.dataset.resizable === 'false') return;
    e.preventDefault();
    const h = horizontal(state.root);
    const start = h ? e.clientX : e.clientY;
    const m = measure(state.root, t.pane);
    let px = m.current;
    bar.setPointerCapture?.(e.pointerId);
    state.root.classList.add('nx-splitter-dragging');
    bar.focus({ preventScroll: true });
    const move = ev => {
        const delta = (h ? ev.clientX : ev.clientY) - start;
        px = Math.min(m.max, Math.max(m.min, m.current + t.sign * delta));
        apply(t.pane, px);
    };
    const up = () => {
        state.root.classList.remove('nx-splitter-dragging');
        document.removeEventListener('pointermove', move);
        document.removeEventListener('pointerup', up);
        document.removeEventListener('pointercancel', up);
        if (Math.abs(px - m.current) >= 0.5) report(state, t.pane, px, m.total);
    };
    document.addEventListener('pointermove', move);
    document.addEventListener('pointerup', up);
    document.addEventListener('pointercancel', up);
}

function onKeyDown(state, e) {
    const bar = e.target;
    if (!isBar(state.root, bar)) return;
    const h = horizontal(state.root);
    const back = h ? 'ArrowLeft' : 'ArrowUp', forward = h ? 'ArrowRight' : 'ArrowDown';
    if (![back, forward, 'Home', 'End'].includes(e.key)) return;
    const t = target(bar);
    if (!t || t.pane.dataset.resizable === 'false') return;
    e.preventDefault();
    const m = measure(state.root, t.pane);
    // Keys move the bar; the target pane grows or shrinks depending on which side of the bar it is.
    const step = e.shiftKey ? BIG_STEP : STEP;
    const barDelta = e.key === forward ? step : e.key === back ? -step : 0;
    let px = m.current + t.sign * barDelta;
    if (e.key === 'Home') px = t.sign > 0 ? m.min : m.max;
    if (e.key === 'End') px = t.sign > 0 ? m.max : m.min;
    px = Math.min(m.max, Math.max(m.min, px));
    if (Math.abs(px - m.current) < 0.5) return;
    apply(t.pane, px);
    report(state, t.pane, px, m.total);
}

export function init(root, dotNet) {
    if (!root || states.has(root)) return;
    const state = { root, dotNet };
    state.onPointerDown = e => onPointerDown(state, e);
    state.onKeyDown = e => onKeyDown(state, e);
    state.resize = new ResizeObserver(() => updateAria(root));
    root.addEventListener('pointerdown', state.onPointerDown);
    root.addEventListener('keydown', state.onKeyDown);
    state.resize.observe(root);
    states.set(root, state);
    updateAria(root);
}

/** After a render (sizes or collapse changed): refresh the bars' aria-valuenow. */
export function refresh(root) {
    if (root && states.has(root)) updateAria(root);
}

export function dispose(root) {
    const state = root && states.get(root);
    if (!state) return;
    root.removeEventListener('pointerdown', state.onPointerDown);
    root.removeEventListener('keydown', state.onKeyDown);
    state.resize.disconnect();
    states.delete(root);
}
