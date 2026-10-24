// NxTooltip interop. Blazor renders the tooltip (hidden) once; this module shows and hides it on the
// anchor's hover, focus, click or long press, places it with flipping, and points aria-describedby at
// it — all in the browser, so a tooltip appears without a server round trip. The tooltip never takes
// pointer events. Escape hides it (WCAG 1.4.13). See NxTooltip.razor.cs, "JS interop".

const GAP = 8;              // px between anchor and tooltip (room for the arrow)
const EDGE = 8;             // px kept clear of the viewport edge
const LONG_PRESS = 500;     // ms a finger must rest to show on touch
const TOUCH_LINGER = 1500;  // ms a touch-shown tooltip stays after the finger lifts
const states = new Map();   // tooltip id → state

const OPPOSITE = { top: 'bottom', bottom: 'top', left: 'right', right: 'left' };

function anchorOf(state) {
    if (state.anchor?.isConnected) return state.anchor;
    const { target, wrapper } = state.options;
    state.anchor = target ? document.querySelector(target) : wrapper?.firstElementChild ?? null;
    return state.anchor;
}

/** The element that takes focus and is described: the anchor, or the first focusable thing inside it. */
function describedOf(anchor) {
    if (!anchor) return null;
    if (anchor.matches('a[href], button, input, select, textarea, [tabindex]')) return anchor;
    return anchor.querySelector('a[href], button, input, select, textarea, [tabindex]') ?? anchor;
}

function fits(side, a, w, h, vw, vh) {
    switch (side) {
        case 'top': return a.top - GAP - h >= EDGE;
        case 'bottom': return a.bottom + GAP + h <= vh - EDGE;
        case 'left': return a.left - GAP - w >= EDGE;
        default: return a.right + GAP + w <= vw - EDGE;
    }
}

function place(state) {
    const anchor = anchorOf(state);
    const tip = state.tip;
    if (!anchor) return;
    const a = anchor.getBoundingClientRect();
    const vw = document.documentElement.clientWidth, vh = window.innerHeight;
    // Scrolled out of view: keep it open but unseen, rather than pinned to the viewport edge.
    tip.style.visibility = a.bottom < 0 || a.top > vh || a.right < 0 || a.left > vw ? 'hidden' : '';
    const w = tip.offsetWidth, h = tip.offsetHeight;
    let side = state.options.placement;
    if (!fits(side, a, w, h, vw, vh) && fits(OPPOSITE[side], a, w, h, vw, vh)) side = OPPOSITE[side];

    let left, top;
    if (side === 'top' || side === 'bottom') {
        left = a.left + a.width / 2 - w / 2;
        top = side === 'top' ? a.top - GAP - h : a.bottom + GAP;
    } else {
        top = a.top + a.height / 2 - h / 2;
        left = side === 'left' ? a.left - GAP - w : a.right + GAP;
    }
    const clampedLeft = Math.min(Math.max(left, EDGE), vw - EDGE - w);
    const clampedTop = Math.min(Math.max(top, EDGE), vh - EDGE - h);
    tip.style.left = `${Math.round(clampedLeft)}px`;
    tip.style.top = `${Math.round(clampedTop)}px`;
    tip.dataset.placement = side;
    // Keep the arrow on the anchor's centre even when the tooltip was pushed in from an edge.
    const arrow = side === 'top' || side === 'bottom'
        ? a.left + a.width / 2 - clampedLeft
        : a.top + a.height / 2 - clampedTop;
    tip.style.setProperty('--nx-tooltip-arrow-at', `${Math.round(arrow)}px`);
}

function show(state, now) {
    clearTimeout(state.timer);
    if (state.open) return;
    const run = () => {
        state.open = true;
        state.tip.hidden = false;
        place(state);
        state.tip.dataset.open = 'true';
        window.addEventListener('scroll', state.onReposition, true);
        window.addEventListener('resize', state.onReposition);
    };
    if (now || state.options.showDelay <= 0) run();
    else state.timer = setTimeout(run, state.options.showDelay);
}

function hide(state, now) {
    clearTimeout(state.timer);
    if (!state.open) return;
    const run = () => {
        state.open = false;
        delete state.tip.dataset.open;
        state.tip.hidden = true;
        window.removeEventListener('scroll', state.onReposition, true);
        window.removeEventListener('resize', state.onReposition);
    };
    if (now || state.options.hideDelay <= 0) run();
    else state.timer = setTimeout(run, state.options.hideDelay);
}

function attach(state) {
    const anchor = anchorOf(state);
    if (!anchor || state.attached === anchor) return;
    detach(state);
    state.attached = anchor;
    const t = state.options.triggers;
    const on = (type, fn, opts) => { anchor.addEventListener(type, fn, opts); state.listeners.push([anchor, type, fn, opts]); };
    if (t.includes('hover')) {
        // A mouse only: a tap also fires hover events, and on touch the long press is the trigger.
        on('pointerenter', e => { if (e.pointerType === 'mouse') show(state); });
        on('pointerleave', e => { if (e.pointerType === 'mouse') hide(state); });
    }
    if (t.includes('focus')) {
        // Keyboard focus only: a mouse click also focuses, and the hover trigger already covers the mouse.
        on('focusin', () => { if (document.activeElement?.matches(':focus-visible')) show(state, true); });
        on('focusout', () => hide(state, true));
    }
    if (t.includes('click')) {
        on('click', () => (state.open ? hide(state, true) : show(state, true)));
    }
    // Touch: a long press shows the tooltip (a tap stays a tap), and it lingers briefly after.
    let pressTimer = 0;
    on('touchstart', () => { pressTimer = setTimeout(() => { state.byTouch = true; show(state, true); }, LONG_PRESS); }, { passive: true });
    const end = () => {
        clearTimeout(pressTimer);
        if (state.byTouch) { state.byTouch = false; state.timer = setTimeout(() => hide(state, true), TOUCH_LINGER); }
    };
    on('touchend', end);
    on('touchcancel', end);
    on('contextmenu', e => { if (state.byTouch) e.preventDefault(); });

    const described = describedOf(anchor);
    if (described) {
        const ids = (described.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(Boolean);
        if (!ids.includes(state.id)) described.setAttribute('aria-describedby', [...ids, state.id].join(' '));
        state.described = described;
    }
}

function detach(state) {
    for (const [el, type, fn, opts] of state.listeners) el.removeEventListener(type, fn, opts);
    state.listeners = [];
    if (state.described) {
        const ids = (state.described.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(i => i && i !== state.id);
        if (ids.length) state.described.setAttribute('aria-describedby', ids.join(' '));
        else state.described.removeAttribute('aria-describedby');
        state.described = null;
    }
    state.attached = null;
}

let escapeListening = false;
function listenForEscape() {
    if (escapeListening) return;
    escapeListening = true;
    document.addEventListener('keydown', e => {
        if (e.key !== 'Escape') return;
        for (const state of states.values()) if (state.open) hide(state, true);
    });
}

/**
 * Sets up tooltip `id`. options: { target (CSS selector) | wrapper (element whose first child is the anchor),
 * triggers: ['hover','focus','click'], showDelay, hideDelay, placement: 'top'|'bottom'|'left'|'right', disabled }.
 */
export function init(id, tip, options) {
    let state = states.get(id);
    if (!state) {
        state = { id, tip, listeners: [], open: false, timer: 0 };
        state.onReposition = () => place(state);
        states.set(id, state);
        listenForEscape();
    }
    state.tip = tip;
    state.options = options;
    state.anchor = null;
    if (options.disabled) {
        hide(state, true);
        detach(state);
        return;
    }
    attach(state);
}

/** Re-finds the anchor after a render (a wrapped child or a target may have been re-created). */
export function refresh(id) {
    const state = states.get(id);
    if (!state || state.options.disabled) return;
    state.anchor = null;
    attach(state);
    if (state.open) place(state);
}

export function showNow(id) { const s = states.get(id); if (s && !s.options.disabled) show(s, true); }
export function hideNow(id) { const s = states.get(id); if (s) hide(s, true); }

export function dispose(id) {
    const state = states.get(id);
    if (!state) return;
    hide(state, true);
    detach(state);
    states.delete(id);
}
