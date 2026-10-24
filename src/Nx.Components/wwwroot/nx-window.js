// NxWindow interop. Blazor owns the markup; this module places the window, drags it by the title bar,
// resizes it from its edges, stacks windows (the last one opened or pressed is on top), traps Tab in
// a modal window, closes on Escape, locks page scroll, and returns focus on close. Blazor never
// renders a style attribute on the window or its backdrop: their position, size and z-index are set here.
// See NxWindow.razor.cs, "JS interop".

import { lockScroll } from './nx-popup.js';

const PHONE = '(max-width: 600px)';
const KEEP_VISIBLE = 80;        // px of title bar kept on screen while dragging
const windows = new Map();      // id → state; keyed by id because Blazor's element refs die with the element
let topZ = 0;

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]):not([type=hidden]), select:not([disabled]), ' +
                  'textarea:not([disabled]), [tabindex]:not([tabindex="-1"]), [contenteditable="true"]';

function focusables(root) {
    return [...root.querySelectorAll(FOCUSABLE)].filter(e => e.offsetParent !== null || e === document.activeElement);
}

function baseZ(win) {
    const value = parseInt(getComputedStyle(win).getPropertyValue('--nx-z-window'), 10);
    return Number.isFinite(value) ? value : 1050;
}

function bringToFront(state) {
    if (state.z !== 0 && state.z === topZ) return;
    state.z = topZ = Math.max(topZ, baseZ(state.win)) + 2;
    state.win.style.zIndex = state.z;
    if (state.backdrop) state.backdrop.style.zIndex = state.z - 1;
}

function clamp(state) {
    const { win } = state;
    const vw = document.documentElement.clientWidth, vh = window.innerHeight;
    const left = Math.min(Math.max(win.offsetLeft, KEEP_VISIBLE - win.offsetWidth), vw - KEEP_VISIBLE);
    const top = Math.min(Math.max(win.offsetTop, 0), vh - 40);
    win.style.left = left + 'px';
    win.style.top = top + 'px';
}

export function center(id) {
    const state = windows.get(id);
    if (!state) return;
    const { win } = state;
    const vw = document.documentElement.clientWidth, vh = window.innerHeight;
    win.style.left = Math.max(0, Math.round((vw - win.offsetWidth) / 2)) + 'px';
    win.style.top = Math.max(0, Math.round((vh - win.offsetHeight) / 2)) + 'px';
}

function startDrag(state, e) {
    const { win } = state;
    if (e.button !== 0 || e.target.closest('button, a, input, select, textarea') || !state.options.draggable) return;
    if (win.classList.contains('nx-window-maximized') || matchMedia(PHONE).matches) return;
    e.preventDefault();
    const startX = e.clientX, startY = e.clientY, left = win.offsetLeft, top = win.offsetTop;
    win.classList.add('nx-window-dragging');
    const move = m => {
        win.style.left = (left + m.clientX - startX) + 'px';
        win.style.top = (top + m.clientY - startY) + 'px';
        clamp(state);
    };
    const up = () => {
        win.classList.remove('nx-window-dragging');
        document.removeEventListener('pointermove', move);
        document.removeEventListener('pointerup', up);
        document.removeEventListener('pointercancel', up);
    };
    document.addEventListener('pointermove', move);
    document.addEventListener('pointerup', up);
    document.addEventListener('pointercancel', up);
}

function startResize(state, handle, e) {
    const { win, options } = state;
    if (e.button !== 0 || win.classList.contains('nx-window-maximized') || matchMedia(PHONE).matches) return;
    e.preventDefault();
    e.stopPropagation();
    const edge = handle.dataset.edge;            // n, s, e, w, ne, nw, se, sw
    const startX = e.clientX, startY = e.clientY;
    const start = { left: win.offsetLeft, top: win.offsetTop, width: win.offsetWidth, height: win.offsetHeight };
    const minW = options.minWidth, minH = options.minHeight;
    win.classList.add('nx-window-resizing');
    const move = m => {
        const dx = m.clientX - startX, dy = m.clientY - startY;
        let { left, top, width, height } = start;
        if (edge.includes('e')) width = Math.max(minW, start.width + dx);
        if (edge.includes('s')) height = Math.max(minH, start.height + dy);
        if (edge.includes('w')) { width = Math.max(minW, start.width - dx); left = start.left + start.width - width; }
        if (edge.includes('n')) { height = Math.max(minH, start.height - dy); top = Math.max(0, start.top + start.height - height); height = start.top + start.height - top; }
        Object.assign(win.style, { left: left + 'px', top: top + 'px', width: width + 'px', height: height + 'px' });
    };
    const up = () => {
        win.classList.remove('nx-window-resizing');
        document.removeEventListener('pointermove', move);
        document.removeEventListener('pointerup', up);
        document.removeEventListener('pointercancel', up);
    };
    document.addEventListener('pointermove', move);
    document.addEventListener('pointerup', up);
    document.addEventListener('pointercancel', up);
}

// Tab and Shift+Tab wrap inside a modal window; Escape (unless a control inside already used it, as an
// open combo box or date picker does) asks the component to close. The event keeps bubbling, since
// Blazor's handlers inside listen at the document; a mark on it keeps the outer window of a nested pair
// from acting on the inner one's keys.
function onKeyDown(state, e) {
    if (e.nxWindowHandled) return;
    e.nxWindowHandled = true;
    if (e.key === 'Escape') {
        if (!e.defaultPrevented && state.options.closeOnEscape) state.dotNet.invokeMethodAsync('OnEscape');
        return;
    }
    if (e.key !== 'Tab' || !state.options.trapFocus) return;
    const items = focusables(state.win);
    if (items.length === 0) { e.preventDefault(); state.win.focus(); return; }
    const first = items[0], last = items[items.length - 1];
    if (e.shiftKey && (document.activeElement === first || document.activeElement === state.win)) {
        e.preventDefault();
        last.focus();
    } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
    }
}

/**
 * Opens window `id`. options: { modal, trapFocus, draggable, closeOnEscape, width, height, left, top,
 * minWidth, minHeight }. Width/height/left/top are CSS lengths or null (size from the CSS preset,
 * centered).
 */
export function open(id, win, backdrop, dotNet, options) {
    if (!win || windows.has(id)) return;
    const state = { id, win, backdrop, dotNet, options, z: 0, returnTo: document.activeElement, locked: false };
    windows.set(id, state);

    // Lock first: dropping the page's scrollbar changes the width the window is centered in.
    state.locked = options.modal || matchMedia(PHONE).matches;
    if (state.locked) lockScroll(true);

    if (options.width) win.style.width = options.width;
    if (options.height) win.style.height = options.height;
    if (options.left != null || options.top != null) {
        win.style.left = options.left ?? '0px';
        win.style.top = options.top ?? '0px';
    } else {
        center(id);
    }
    bringToFront(state);
    win.style.visibility = 'visible';

    state.onPointerDown = () => bringToFront(state);
    state.onKeyDown = e => onKeyDown(state, e);
    state.onTitleDown = e => startDrag(state, e);
    state.onHandleDown = e => {
        const handle = e.target.closest('.nx-window-resize');
        if (handle && win.contains(handle)) startResize(state, handle, e);
    };
    win.addEventListener('pointerdown', state.onPointerDown, true);
    win.addEventListener('keydown', state.onKeyDown);
    win.querySelector('.nx-window-titlebar')?.addEventListener('pointerdown', state.onTitleDown);
    win.addEventListener('pointerdown', state.onHandleDown);

    // An [autofocus] in the body, else one in the footer (a dialog's OK), else the body's first field.
    const target = win.querySelector('.nx-window-body [autofocus]') ?? win.querySelector('.nx-window-footer [autofocus]')
        ?? focusables(win.querySelector('.nx-window-body') ?? win)[0] ?? win;
    target.focus({ preventScroll: true });
}

/** Updates the options of an open window (e.g. Modal or Draggable changed). */
export function update(id, options) {
    const state = windows.get(id);
    if (!state) return;
    const lock = options.modal || matchMedia(PHONE).matches;
    if (lock !== state.locked) {
        lockScroll(lock);
        state.locked = lock;
    }
    state.options = options;
}

/** Re-binds the elements after Blazor re-created them (e.g. minimize swapped the backdrop out). */
export function rebind(id, win, backdrop) {
    const state = windows.get(id);
    if (!state) return;
    state.backdrop = backdrop;
    if (backdrop) backdrop.style.zIndex = state.z - 1;
    if (win !== state.win && win) {
        state.win = win;
        win.style.zIndex = state.z;
    }
}

export function front(id) {
    const state = windows.get(id);
    if (state) bringToFront(state);
}

/** Closes window `id`: releases the scroll lock and puts focus back where it was when the window opened. */
export function close(id) {
    const state = windows.get(id);
    if (!state) return;
    windows.delete(id);
    const { win } = state;
    win.removeEventListener('pointerdown', state.onPointerDown, true);
    win.removeEventListener('keydown', state.onKeyDown);
    win.querySelector('.nx-window-titlebar')?.removeEventListener('pointerdown', state.onTitleDown);
    win.removeEventListener('pointerdown', state.onHandleDown);
    if (state.locked) lockScroll(false);
    if (windows.size === 0) topZ = 0;
    const back = state.returnTo;
    if (back && back.isConnected && typeof back.focus === 'function') back.focus({ preventScroll: true });
}
