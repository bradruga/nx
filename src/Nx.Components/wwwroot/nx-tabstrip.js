// NxTabStrip interop. Blazor owns the markup; this module moves focus between tabs with the arrow keys
// (selection follows focus, the ARIA "automatic activation" pattern — done here so fast key presses
// never outrun a server round trip), shows the overflow arrows when the tabs do not fit, scrolls them,
// and keeps the active tab in view. See NxTabStrip.razor.cs, "JS interop".

const states = new WeakMap();
const SCROLL_STEP = 0.8;        // overflow arrows scroll this share of the visible width

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

export function init(header, list) {
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
    list.addEventListener('keydown', onKeyDown);
    list.addEventListener('scroll', onScroll, { passive: true });
    header.addEventListener('click', onArrow);
    states.set(list, { header, onKeyDown, onScroll, onArrow, resize });
    updateOverflow(header, list);
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
    state.header.removeEventListener('click', state.onArrow);
    states.delete(list);
}
