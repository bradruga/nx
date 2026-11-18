// Interop for NxPopup. Placing needs the browser: the anchor's and the popup's boxes and the viewport. The popup opens
// on its side of the anchor, flips to the other side when that has more room, and shifts along the anchor to stay on
// screen; it follows scrolling, resizing and its own size changes. A press outside (popup and anchor) or Escape is
// reported to .NET, which closes it. State is keyed by the popup's id.
//
// A hover card (OpenOnHover) is also watched while it is closed: the pointer resting on an element that matches the
// anchor selector, or the keyboard focusing one, asks .NET to open it for that element; the pointer leaving both the
// anchor and the popup asks to close it. Many elements may match: the popup goes to the one in use.

const EDGE = 8;     // px kept clear of the viewport edge
const popups = new Map();
const hovers = new Map();

// The element the popup belongs to now: the hovered one, else the first that matches.
function anchorOf(id, options) {
    const hovered = hovers.get(id)?.anchor;
    return hovered?.isConnected ? hovered : document.querySelector(options.anchor);
}

export function open(id, popup, dotNet, options) {
    if (!popup) return;
    close(id);
    const state = { popup, dotNet, options };
    state.place = () => place(id);
    state.onPress = e => {
        if (!state.options.closeOnOutside) return;
        const anchor = anchorOf(id, state.options);
        if (state.popup.contains(e.target) || anchor?.contains(e.target)) return;
        dotNet.invokeMethodAsync('OnDismiss');
    };
    state.onKey = e => {
        if (e.key !== 'Escape' || !state.options.closeOnEscape || e.defaultPrevented) return;
        // A hover card that took the keyboard gives it back to its anchor.
        if (state.options.hover && state.popup.contains(document.activeElement)) anchorOf(id, state.options)?.focus();
        dotNet.invokeMethodAsync('OnDismiss');
    };
    state.resize = new ResizeObserver(state.place);
    state.resize.observe(popup);
    window.addEventListener('scroll', state.place, true);
    window.addEventListener('resize', state.place);
    document.addEventListener('pointerdown', state.onPress, true);
    document.addEventListener('keydown', state.onKey);
    popups.set(id, state);
    place(id);
}

export function update(id, popup, dotNet, options) {
    const state = popups.get(id);
    if (!state) return open(id, popup, dotNet, options);
    state.popup = popup;
    state.options = options;
    place(id);
}

export function close(id) {
    const state = popups.get(id);
    if (!state) return;
    state.resize.disconnect();
    window.removeEventListener('scroll', state.place, true);
    window.removeEventListener('resize', state.place);
    document.removeEventListener('pointerdown', state.onPress, true);
    document.removeEventListener('keydown', state.onKey);
    popups.delete(id);
}

export function place(id) {
    const state = popups.get(id);
    if (!state || !state.popup.isConnected) return;
    const { popup, options } = state;
    const anchor = anchorOf(id, options);
    popup.style.width = options.width ?? '';
    const viewportW = document.documentElement.clientWidth;
    const viewportH = window.innerHeight;
    if (!anchor) {
        popup.style.visibility = 'hidden';
        return;
    }
    popup.style.visibility = '';
    const a = anchor.getBoundingClientRect();
    const vertical = options.placement === 'bottom' || options.placement === 'top';
    const gap = options.offset ?? 4;

    // Room on each side decides the side; the popup is then held to the room it got.
    const room = {
        bottom: viewportH - a.bottom - gap - EDGE,
        top: a.top - gap - EDGE,
        right: viewportW - a.right - gap - EDGE,
        left: a.left - gap - EDGE
    };
    const opposite = { bottom: 'top', top: 'bottom', right: 'left', left: 'right' };
    popup.style.maxHeight = '';
    let size = popup.getBoundingClientRect();
    let side = options.placement;
    const need = vertical ? size.height : size.width;
    if (room[side] < need && room[opposite[side]] > room[side]) side = opposite[side];
    if (vertical) {
        const fit = Math.max(80, room[side]);
        popup.style.maxHeight = options.maxHeight ? `min(${options.maxHeight}, ${fit}px)` : `${fit}px`;
    } else if (options.maxHeight) {
        popup.style.maxHeight = options.maxHeight;
    }
    size = popup.getBoundingClientRect();

    let top, left;
    if (vertical) {
        top = side === 'bottom' ? a.bottom + gap : a.top - gap - size.height;
        left = options.align === 'end' ? a.right - size.width
            : options.align === 'center' ? a.left + (a.width - size.width) / 2
            : a.left;
        left = Math.min(Math.max(left, EDGE), Math.max(EDGE, viewportW - size.width - EDGE));
    } else {
        left = side === 'right' ? a.right + gap : a.left - gap - size.width;
        top = options.align === 'end' ? a.bottom - size.height
            : options.align === 'center' ? a.top + (a.height - size.height) / 2
            : a.top;
        top = Math.min(Math.max(top, EDGE), Math.max(EDGE, viewportH - size.height - EDGE));
    }
    popup.style.top = `${Math.round(top)}px`;
    popup.style.left = `${Math.round(left)}px`;
    popup.dataset.placement = side;
}

// ── Hover cards ───────────────────────────────────────────────────────────────

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export function watchHover(id, dotNet, options) {
    unwatchHover(id);
    const state = { anchor: null, showTimer: 0, hideTimer: 0 };
    const popup = () => popups.get(id)?.popup;
    const contextOf = el => el.closest('[data-nx-context]')?.getAttribute('data-nx-context') ?? null;
    const show = (anchor, delay) => {
        clearTimeout(state.hideTimer);
        clearTimeout(state.showTimer);
        if (anchor === state.anchor && popup()) return;
        state.showTimer = setTimeout(async () => {
            if (!anchor.isConnected) return;
            const before = state.anchor;
            state.anchor = anchor;
            // .NET may refuse (OnOpening cancelled): then the popup stays as it was.
            if (!await dotNet.invokeMethodAsync('OnHoverOpen', contextOf(anchor))) state.anchor = before;
        }, delay);
    };
    const hide = () => {
        clearTimeout(state.showTimer);
        clearTimeout(state.hideTimer);
        if (!popup()) return;
        state.hideTimer = setTimeout(() => { if (popup()) dotNet.invokeMethodAsync('OnDismiss'); }, options.hideDelay);
    };
    // Touch has no hover: a tap is the page's to handle.
    state.onOver = e => {
        if (e.pointerType === 'touch') return;
        const anchor = e.target.closest?.(options.anchor);
        if (anchor) show(anchor, options.showDelay);
        else if (popup()?.contains(e.target)) clearTimeout(state.hideTimer);
        else hide();
    };
    state.onLeave = () => hide();
    state.onFocusIn = e => {
        const anchor = e.target.closest?.(options.anchor);
        if (anchor && e.target.matches(':focus-visible')) show(anchor, 0);
        else if (!anchor && !popup()?.contains(e.target)) hide();
    };
    // Down from the anchor goes into the card, where its links are.
    state.onKey = e => {
        if (e.key !== 'ArrowDown' || !popup() || e.target !== state.anchor) return;
        const first = popup().querySelector(FOCUSABLE);
        if (!first) return;
        e.preventDefault();
        first.focus();
    };
    document.addEventListener('pointerover', state.onOver, true);
    document.documentElement.addEventListener('pointerleave', state.onLeave);
    document.addEventListener('focusin', state.onFocusIn);
    document.addEventListener('keydown', state.onKey);
    hovers.set(id, state);
}

export function unwatchHover(id) {
    const state = hovers.get(id);
    if (!state) return;
    clearTimeout(state.showTimer);
    clearTimeout(state.hideTimer);
    document.removeEventListener('pointerover', state.onOver, true);
    document.documentElement.removeEventListener('pointerleave', state.onLeave);
    document.removeEventListener('focusin', state.onFocusIn);
    document.removeEventListener('keydown', state.onKey);
    hovers.delete(id);
}
