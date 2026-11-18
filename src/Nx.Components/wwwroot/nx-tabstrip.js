// NxTabStrip interop. Blazor owns the markup; this module moves focus between tabs with the arrow keys
// (selection follows focus, the ARIA "automatic activation" pattern — done here so fast key presses
// never outrun a server round trip), shows the overflow arrows when the tabs do not fit, scrolls them,
// keeps the active tab in view, and (Reorderable) lets a tab be dragged along the strip while the others make
// room; .NET is told the old and new place on release. See NxTabStrip.razor.cs, "JS interop".

const states = new WeakMap();
const SCROLL_STEP = 0.8;        // overflow arrows scroll this share of the visible width
const DRAG_START = 5;           // px the pointer moves before a press on a tab becomes a drag (a click still activates)

function enabledTabs(list) {
    return [...list.querySelectorAll('[role=tab]')].filter(t => t.getAttribute('aria-disabled') !== 'true');
}

function vertical(list) {
    return list.getAttribute('aria-orientation') === 'vertical';
}

function updateOverflow(header, list) {
    const v = vertical(list) && list.scrollHeight > list.clientHeight + 1;
    const start = v ? list.scrollTop : list.scrollLeft;
    const size = v ? list.clientHeight : list.clientWidth;
    const total = v ? list.scrollHeight : list.scrollWidth;
    const overflows = total > size + 1;
    header.toggleAttribute('data-overflow', overflows);
    header.toggleAttribute('data-can-scroll-start', overflows && start > 1);
    header.toggleAttribute('data-can-scroll-end', overflows && start + size < total - 1);
}

export function init(header, list, dotNet) {
    if (!header || !list || states.has(list)) return;
    const onKeyDown = e => {
        if (e.altKey || e.ctrlKey || e.metaKey || !e.target.matches('[role=tab]')) return;
        const v = vertical(list);
        const keys = v ? ['ArrowDown', 'ArrowUp', 'Home', 'End'] : ['ArrowRight', 'ArrowLeft', 'Home', 'End'];
        if (e.key === ' ' || e.key === 'Enter') {
            e.preventDefault();
            e.target.click();
            return;
        }
        if (!keys.includes(e.key)) return;
        const tabs = enabledTabs(list);
        const current = tabs.indexOf(e.target);
        if (tabs.length === 0 || current < 0) return;
        e.preventDefault();
        const forward = e.key === 'ArrowRight' || e.key === 'ArrowDown';
        const next = e.key === 'Home' ? 0 : e.key === 'End' ? tabs.length - 1
            : forward ? (current + 1) % tabs.length : (current - 1 + tabs.length) % tabs.length;
        tabs[next].focus();
        reveal(list, tabs[next]);
        // Always click: after fast key presses aria-selected may still show a tab the server has since left.
        tabs[next].click();
    };
    const onScroll = () => updateOverflow(header, list);
    const onArrow = e => {
        const arrow = e.target.closest('.nx-tabstrip-scroll');
        if (!arrow) return;
        const back = arrow.classList.contains('nx-tabstrip-scroll-start') ? -1 : 1;
        const v = vertical(list) && list.scrollHeight > list.clientHeight + 1;
        list.scrollBy(v ? { top: back * list.clientHeight * SCROLL_STEP, behavior: 'smooth' }
                        : { left: back * list.clientWidth * SCROLL_STEP, behavior: 'smooth' });
    };
    const resize = new ResizeObserver(onScroll);
    resize.observe(list);
    for (const tab of list.children) resize.observe(tab);
    // A mouse or pen press on a tab may become a drag; touch is left to scroll the tabs.
    const onPointerDown = e => {
        if (e.button !== 0 || e.pointerType === 'touch' || !list.hasAttribute('data-reorderable')) return;
        const tab = e.target.closest('[role=tab]');
        if (tab && !e.target.closest('.nx-tabstrip-close')) dragTab(list, tab, e, dotNet);
    };
    list.addEventListener('keydown', onKeyDown);
    list.addEventListener('scroll', onScroll, { passive: true });
    list.addEventListener('pointerdown', onPointerDown);
    header.addEventListener('click', onArrow);
    states.set(list, { header, onKeyDown, onScroll, onArrow, onPointerDown, resize });
    updateOverflow(header, list);
}

// The tab lifts and follows the pointer along the strip, as a browser's tabs do; each neighbour it passes slides
// into the space it left, so the strip shows the new order the whole time. On release the tab settles into its
// place and .NET is told; the transforms stay until Blazor has moved the elements, so nothing flashes back.
// Escape drops the drag, and the click that ends a real drag does not also activate the tab.
function dragTab(list, tab, e, dotNet) {
    const v = vertical(list);
    const at = ev => v ? ev.clientY : ev.clientX;
    const lo = r => v ? r.top : r.left, hi = r => v ? r.bottom : r.right;
    const shift = px => px ? (v ? `translateY(${px}px)` : `translateX(${px}px)`) : '';
    const tabs = [...list.querySelectorAll('[role=tab]')];
    const from = tabs.indexOf(tab);
    const rects = tabs.map(t => t.getBoundingClientRect());
    const box = list.getBoundingClientRect();
    const size = hi(rects[from]) - lo(rects[from]);
    const start = at(e);
    let dragging = false, to = from;

    const move = ev => {
        if (!dragging) {
            if (Math.abs(at(ev) - start) < DRAG_START) return;
            dragging = true;
            tab.classList.add('nx-tabstrip-tab-dragging');
            list.classList.add('nx-tabstrip-list-dragging');
        }
        // The tab stays inside the strip.
        const d = Math.max(lo(box) - lo(rects[from]), Math.min(at(ev) - start, hi(box) - hi(rects[from])));
        tab.style.transform = shift(d);
        // It passes a neighbour when its leading edge crosses that neighbour's middle. (Comparing middles would
        // never let a wide tab, stopped at the strip's edge, get past a narrower first or last one.)
        const head = lo(rects[from]) + d, tail = hi(rects[from]) + d;
        to = tabs.reduce((n, t, i) => {
            const middle = (lo(rects[i]) + hi(rects[i])) / 2;
            return i < from && head < middle ? n - 1 : i > from && tail > middle ? n + 1 : n;
        }, from);
        tabs.forEach((t, i) => {
            if (i !== from) t.style.transform = shift(i > from && i <= to ? -size : i < from && i >= to ? size : 0);
        });
    };
    const reset = () => {
        list.classList.remove('nx-tabstrip-list-dragging', 'nx-tabstrip-list-settling');
        tab.classList.remove('nx-tabstrip-tab-dragging');
        tabs.forEach(t => { t.style.transform = ''; });
    };
    const finish = commit => {
        document.removeEventListener('pointermove', move);
        document.removeEventListener('pointerup', up);
        document.removeEventListener('pointercancel', cancel);
        document.removeEventListener('keydown', key, true);
        if (!dragging) return;
        const swallow = ev => { ev.stopPropagation(); ev.preventDefault(); };
        list.addEventListener('click', swallow, { capture: true, once: true });
        setTimeout(() => list.removeEventListener('click', swallow, true), 0);
        if (!commit || to === from) { reset(); return; }
        // Settle into the gap, then let go of every transform in the same frame Blazor reorders the elements.
        list.classList.add('nx-tabstrip-list-settling');
        tab.style.transform = shift(to > from ? hi(rects[to]) - hi(rects[from]) : lo(rects[to]) - lo(rects[from]));
        const observer = new MutationObserver(() => done());
        const timer = setTimeout(() => done(), 1000);
        const done = () => { observer.disconnect(); clearTimeout(timer); reset(); };
        observer.observe(list, { childList: true });
        dotNet.invokeMethodAsync('OnTabDrop', from, to);
    };
    const up = () => finish(true);
    const cancel = () => finish(false);
    const key = ev => {
        if (ev.key !== 'Escape' || !dragging) return;
        ev.preventDefault();
        ev.stopPropagation();
        finish(false);
    };
    document.addEventListener('pointermove', move);
    document.addEventListener('pointerup', up);
    document.addEventListener('pointercancel', cancel);
    document.addEventListener('keydown', key, true);
}

/** After a keyboard move the tab's element changed place and lost focus: give it back. */
export function focusTab(list, id) {
    const tab = list?.querySelector(`#${CSS.escape(id)}`);
    tab?.focus();
    reveal(list, tab);
}

/**
 * Scrolls the list (not the page) so the tab is fully visible. Measured against the list's own box: the
 * tab's offsetLeft counts from the header, which includes the start arrow.
 */
function reveal(list, tab) {
    if (!tab) return;
    const t = tab.getBoundingClientRect(), l = list.getBoundingClientRect();
    if (vertical(list) && list.scrollHeight > list.clientHeight + 1) {
        if (t.top < l.top) list.scrollTop += Math.floor(t.top - l.top);
        else if (t.bottom > l.top + list.clientHeight) list.scrollTop += Math.ceil(t.bottom - l.top - list.clientHeight);
    } else {
        if (t.left < l.left) list.scrollLeft += Math.floor(t.left - l.left);
        else if (t.right > l.left + list.clientWidth) list.scrollLeft += Math.ceil(t.right - l.left - list.clientWidth);
    }
}

/** After a render: re-observe the tabs (some may be new), keep the active one in view, refresh the arrows. */
export function refresh(list) {
    const state = list && states.get(list);
    if (!state) return;
    state.resize.disconnect();
    state.resize.observe(list);
    for (const tab of list.children) state.resize.observe(tab);
    reveal(list, list.querySelector('[role=tab][aria-selected=true]'));
    updateOverflow(state.header, list);
}

export function dispose(list) {
    const state = list && states.get(list);
    if (!state) return;
    state.resize.disconnect();
    list.removeEventListener('keydown', state.onKeyDown);
    list.removeEventListener('scroll', state.onScroll);
    list.removeEventListener('pointerdown', state.onPointerDown);
    state.header.removeEventListener('click', state.onArrow);
    states.delete(list);
}
