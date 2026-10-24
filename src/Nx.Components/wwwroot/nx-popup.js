// Popup interop shared by NxComboBox, NxDatePicker, NxDateRangePicker and NxTimePicker. Blazor owns
// the markup; this module only measures the viewport to place a popup, closes it on an outside
// pointer press, locks page scroll under a full-screen sheet, and reports when the viewport crosses
// a component's mobile breakpoint.

const GAP = 4;          // px between the field and the popup
const EDGE = 8;         // px kept clear of the viewport edge
const placements = new WeakMap();
const viewports = new WeakMap();

// Places the popup under the field, or above it when there is not room below and there is more
// room above. The choice is made once per open: as filtering shrinks a list, the popup keeps the
// edge that touches the field (top when below, bottom when above) so it never drifts away.
// `body` is the part that scrolls (a list) or simply the popup's content (a calendar).
// With `dotNet` and `outsideMethod`, a pointer press outside field and popup calls that method.
export function open(field, popup, body, maxHeight, dotNet, outsideMethod) {
    const place = () => {
        const rect = field.getBoundingClientRect();
        const viewportH = window.innerHeight;
        const viewportW = document.documentElement.clientWidth;
        const spaceBelow = viewportH - rect.bottom - GAP - EDGE;
        const spaceAbove = rect.top - GAP - EDGE;

        if (!popup.dataset.placement) {
            const wanted = Math.min(maxHeight, body.scrollHeight + (popup.offsetHeight - body.clientHeight));
            popup.dataset.placement = spaceBelow < wanted && spaceAbove > spaceBelow ? 'above' : 'below';
        }
        const above = popup.dataset.placement === 'above';

        body.style.maxHeight = Math.max(80, Math.min(maxHeight, above ? spaceAbove : spaceBelow)) + 'px';
        popup.style.minWidth = rect.width + 'px';

        const width = popup.offsetWidth;
        let left = rect.left;
        if (left + width > viewportW - EDGE) left = Math.max(EDGE, viewportW - EDGE - width);
        popup.style.left = left + 'px';

        if (above) {
            popup.style.top = '';
            popup.style.bottom = (viewportH - rect.top + GAP) + 'px';
        } else {
            popup.style.bottom = '';
            popup.style.top = (rect.bottom + GAP) + 'px';
        }
        popup.style.visibility = 'visible';
    };

    close(field);
    place();
    const onScroll = e => { if (!popup.contains(e.target)) place(); };
    window.addEventListener('scroll', onScroll, true);
    window.addEventListener('resize', place);
    let onPointerDown = null;
    if (dotNet && outsideMethod) {
        onPointerDown = e => {
            if (!field.contains(e.target) && !popup.contains(e.target))
                dotNet.invokeMethodAsync(outsideMethod);
        };
        document.addEventListener('pointerdown', onPointerDown, true);
    }
    placements.set(field, { onScroll, place, onPointerDown });
    return popup.dataset.placement;
}

// Keyed by the field, which outlives the popup: by the time Blazor calls this the popup is gone.
export function close(field) {
    const state = field && placements.get(field);
    if (state) {
        window.removeEventListener('scroll', state.onScroll, true);
        window.removeEventListener('resize', state.place);
        if (state.onPointerDown) document.removeEventListener('pointerdown', state.onPointerDown, true);
        placements.delete(field);
    }
}

// Tells the component whether the viewport is at or below the breakpoint, now and whenever that
// changes, so a tap can open the full-screen sheet instead of the popup.
export function watchViewport(field, dotNet, breakpoint) {
    unwatchViewport(field);
    const query = window.matchMedia(`(max-width: ${breakpoint}px)`);
    const onChange = e => dotNet.invokeMethodAsync('OnViewportChanged', e.matches);
    query.addEventListener('change', onChange);
    viewports.set(field, { query, onChange });
    return query.matches;
}

export function unwatchViewport(field) {
    const viewport = field && viewports.get(field);
    if (viewport) {
        viewport.query.removeEventListener('change', viewport.onChange);
        viewports.delete(field);
    }
}

// A full-screen sheet covers the page; the page behind it must not scroll under the user's finger.
// Counted, so nested sheets and windows unlock only when the last one closes.
let lockCount = 0;
export function lockScroll(lock) {
    lockCount = Math.max(0, lockCount + (lock ? 1 : -1));
    document.documentElement.classList.toggle('nx-scroll-locked', lockCount > 0);
}
