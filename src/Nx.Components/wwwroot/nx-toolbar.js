// Interop for NxToolBar. Measuring what fits needs the browser: on every resize the items are laid end to end
// (an overflowed item keeps its width, invisible) and the keys of those past the end are reported to .NET, which
// moves them into the More menu. Also the toolbar's roving tab stop: one tab stop, the arrows, Home and End move
// between the shown items without a round trip.

const states = new WeakMap();

const itemsOf = bar => [...bar.querySelectorAll(':scope > [data-nx-toolbar-item]')];
const focusablesOf = bar => [...bar.querySelectorAll(':scope > [data-nx-toolbar-item]:not(.nx-toolbar-item-overflow):not(:disabled), :scope > .nx-toolbar-more button')]
    .filter(e => !e.disabled);

function measure(bar) {
    const state = states.get(bar);
    if (!state) return;
    const items = itemsOf(bar);
    const style = getComputedStyle(bar);
    const gap = parseFloat(style.columnGap) || 0;
    const available = bar.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
    const outer = e => { const s = getComputedStyle(e); return e.getBoundingClientRect().width + (parseFloat(s.marginLeft) || 0) + (parseFloat(s.marginRight) || 0); };
    const widths = items.map(i => i.classList.contains('nx-toolbar-spacer') ? 0 : outer(i));
    const total = widths.reduce((sum, w, i) => sum + w + (i > 0 ? gap : 0), 0);
    let count = items.length;
    if (state.overflow && total > available + 0.5) {
        const more = bar.querySelector(':scope > .nx-toolbar-more');
        const room = available - (more ? more.getBoundingClientRect().width + gap : 40);
        let used = 0;
        count = 0;
        for (let i = 0; i < items.length; i++) {
            used += widths[i] + (i > 0 ? gap : 0);
            if (used > room) break;
            count = i + 1;
        }
        // A separator or spacer never ends the shown part.
        while (count > 0 && items[count - 1].matches('.nx-toolbar-separator, .nx-toolbar-spacer')) count--;
    }
    const keys = items.slice(count).map(i => i.dataset.nxToolbarItem);
    const signature = keys.join('|');
    if (signature !== state.reported) {
        state.reported = signature;
        state.dotNet.invokeMethodAsync('OnOverflow', keys);
    }
    updateTabStops(bar);
}

// Exactly one shown item is in the tab order: the last one focused, else the first.
function updateTabStops(bar) {
    const state = states.get(bar);
    const items = focusablesOf(bar);
    const current = items.includes(state.last) ? state.last : items[0];
    for (const item of items) item.tabIndex = item === current ? 0 : -1;
    for (const hidden of bar.querySelectorAll(':scope > .nx-toolbar-item-overflow')) hidden.tabIndex = -1;
}

export function init(bar, overflow, dotNet) {
    if (!bar || states.has(bar)) return;
    const state = { overflow, dotNet, reported: null, last: null };
    state.onKeyDown = e => {
        if (e.altKey || e.ctrlKey || e.metaKey) return;
        if (!['ArrowRight', 'ArrowLeft', 'Home', 'End'].includes(e.key)) return;
        if (e.target.closest('[role=menu]')) return;
        const items = focusablesOf(bar);
        const at = items.indexOf(document.activeElement);
        if (at < 0) return;
        e.preventDefault();
        const next = e.key === 'Home' ? 0
            : e.key === 'End' ? items.length - 1
            : e.key === 'ArrowRight' ? (at + 1) % items.length
            : (at - 1 + items.length) % items.length;
        items[next].focus();
    };
    state.onFocusIn = e => {
        if (focusablesOf(bar).includes(e.target)) {
            state.last = e.target;
            updateTabStops(bar);
        }
    };
    state.observer = new ResizeObserver(() => measure(bar));
    state.observer.observe(bar);
    bar.addEventListener('keydown', state.onKeyDown);
    bar.addEventListener('focusin', state.onFocusIn);
    states.set(bar, state);
    measure(bar);
}

// After .NET renders (items added, hidden or relabelled) the widths may have changed.
export function refresh(bar, overflow) {
    const state = bar && states.get(bar);
    if (!state) return;
    state.overflow = overflow;
    measure(bar);
}

export function dispose(bar) {
    const state = bar && states.get(bar);
    if (!state) return;
    state.observer.disconnect();
    bar.removeEventListener('keydown', state.onKeyDown);
    bar.removeEventListener('focusin', state.onFocusIn);
    states.delete(bar);
}
