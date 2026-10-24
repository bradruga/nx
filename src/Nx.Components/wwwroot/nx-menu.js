// NxMenu interop (also behind NxContextMenu, NxDropDownButton and NxSplitButton). Blazor renders the whole
// menu tree, submenus hidden; this module places the menu at a point or under an anchor with flipping,
// opens and places submenus, moves focus with the keyboard, and reports when the user dismisses it. Done
// here so arrows and submenus answer without a server round trip; picking an item is a Blazor click.
// On phones the menu is a bottom sheet and submenus drill in. See NxMenu.razor.cs, "JS interop".

import { lockScroll } from './nx-popup.js';

const EDGE = 8;              // px kept clear of the viewport edge
const SUB_DELAY = 150;       // ms the pointer rests on a parent item before its submenu opens
const PHONE = '(max-width: 600px)';
const states = new Map();    // menu id → state
const contextTargets = new Map();

const itemsOf = list => [...list.querySelectorAll(':scope > li > .nx-menu-item')]
    .filter(i => i.getAttribute('aria-disabled') !== 'true');
const listOf = item => item.closest('ul.nx-menu-list');
const subOf = item => item.parentElement.querySelector(':scope > ul.nx-menu-list');

function clampInto(el, left, top) {
    const vw = document.documentElement.clientWidth, vh = window.innerHeight;
    const w = el.offsetWidth, h = el.offsetHeight;
    el.style.left = `${Math.round(Math.min(Math.max(left, EDGE), vw - EDGE - w))}px`;
    el.style.top = `${Math.round(Math.min(Math.max(top, EDGE), vh - EDGE - h))}px`;
}

/** Root placement: at a point (opening right and down, flipping left or up), or below an anchor. */
function placeRoot(state) {
    const { how } = state;
    const root = state.rootList;
    if (state.sheet) return;
    const vw = document.documentElement.clientWidth, vh = window.innerHeight;
    const w = root.offsetWidth, h = root.offsetHeight;
    if (how.anchor) {
        const a = how.anchor.getBoundingClientRect();
        const below = a.bottom + 4 + h <= vh - EDGE || a.top - 4 - h < EDGE;
        let left = how.alignEnd ? a.right - w : a.left;
        if (left + w > vw - EDGE) left = a.right - w;
        clampInto(root, left, below ? a.bottom + 4 : a.top - 4 - h);
    } else {
        const left = how.x + w <= vw - EDGE ? how.x : how.x - w;
        const top = how.y + h <= vh - EDGE ? how.y : how.y - h;
        clampInto(root, left, top);
    }
}

/** A submenu sits beside its parent item: to the right, else to the left. On a phone it fills the sheet. */
function placeSub(state, item, sub) {
    if (state.sheet) return;
    const r = item.getBoundingClientRect();
    const vw = document.documentElement.clientWidth;
    const w = sub.offsetWidth;
    const right = r.right - 2 + w <= vw - EDGE;
    clampInto(sub, right ? r.right - 2 : r.left - w + 2, r.top - 5);
}

function openSub(state, item, focusFirst) {
    const sub = subOf(item);
    if (!sub) return;
    // One open submenu per level: close siblings (and their children) first.
    for (const other of listOf(item).querySelectorAll(':scope > li > ul.nx-menu-list:not([hidden])'))
        if (other !== sub) closeSub(other);
    if (sub.hidden) {
        sub.hidden = false;
        item.setAttribute('aria-expanded', 'true');
        placeSub(state, item, sub);
    }
    if (focusFirst) itemsOf(sub)[0]?.focus();
}

function closeSub(sub) {
    for (const inner of sub.querySelectorAll('ul.nx-menu-list:not([hidden])')) inner.hidden = true;
    sub.hidden = true;
    sub.parentElement.querySelector(':scope > .nx-menu-item')?.setAttribute('aria-expanded', 'false');
}

function onKeyDown(state, e) {
    const item = e.target.closest('.nx-menu-item');
    if (!item || !state.root.contains(item)) return;
    const list = listOf(item);
    const items = itemsOf(list);
    const index = items.indexOf(item);
    const isSub = list !== state.rootList;
    const focus = i => { items[(i + items.length) % items.length]?.focus(); };
    switch (e.key) {
        case 'ArrowDown': e.preventDefault(); focus(index + 1); return;
        case 'ArrowUp': e.preventDefault(); focus(index - 1); return;
        case 'Home': e.preventDefault(); focus(0); return;
        case 'End': e.preventDefault(); focus(items.length - 1); return;
        case 'ArrowRight':
            if (subOf(item)) { e.preventDefault(); openSub(state, item, true); }
            return;
        case 'Enter':
        case ' ':
            e.preventDefault();
            if (subOf(item)) openSub(state, item, true);
            else item.click();
            return;
        case 'ArrowLeft':
        case 'Escape':
            e.preventDefault();
            e.stopPropagation();
            if (isSub) {
                const parent = list.parentElement.querySelector(':scope > .nx-menu-item');
                closeSub(list);
                parent?.focus();
            } else if (e.key === 'Escape') {
                dismiss(state, true);
            }
            return;
        case 'Tab':
            e.preventDefault();
            dismiss(state, true);
            return;
    }
    if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
        // First-letter navigation: the next item whose text starts with the key.
        const key = e.key.toLowerCase();
        for (let i = 1; i <= items.length; i++) {
            const next = items[(index + i) % items.length];
            if ((next.querySelector('.nx-menu-text')?.textContent ?? '').trim().toLowerCase().startsWith(key)) {
                next.focus();
                break;
            }
        }
    }
}

function onPointerOver(state, e) {
    if (state.sheet) return;
    const item = e.target.closest('.nx-menu-item');
    if (!item || !state.root.contains(item)) return;
    if (item !== document.activeElement && item.getAttribute('aria-disabled') !== 'true') item.focus({ preventScroll: true });
    clearTimeout(state.subTimer);
    state.subTimer = setTimeout(() => {
        const list = listOf(item);
        if (subOf(item)) openSub(state, item, false);
        else for (const other of list.querySelectorAll(':scope > li > ul.nx-menu-list:not([hidden])')) closeSub(other);
    }, SUB_DELAY);
}

function onClick(state, e) {
    const item = e.target.closest('.nx-menu-item');
    if (item && subOf(item)) {
        e.preventDefault();
        openSub(state, item, state.sheet || e.detail === 0);
    }
    if (e.target.closest('.nx-menu-back')) {
        const list = e.target.closest('ul.nx-menu-list');
        const parent = list.parentElement.querySelector(':scope > .nx-menu-item');
        closeSub(list);
        parent?.focus();
    }
}

function dismiss(state, restoreFocus) {
    state.restoreFocus = restoreFocus;
    state.dotNet.invokeMethodAsync('OnDismiss');
}

/**
 * Opens menu `id` (already rendered): how is { x, y } for a point or { anchor, alignEnd } for an element.
 * focusFirst moves focus to the first item (keyboard and context menus); otherwise the menu itself gets it.
 */
export function open(id, root, dotNet, how, focusFirst) {
    close(id, false);
    const state = { id, root, dotNet, how, subTimer: 0, restoreFocus: true, returnTo: document.activeElement };
    state.rootList = root.querySelector(':scope > ul.nx-menu-list');
    state.sheet = matchMedia(PHONE).matches;
    root.classList.toggle('nx-menu-sheet', state.sheet);
    states.set(id, state);

    state.onKeyDown = e => onKeyDown(state, e);
    state.onPointerOver = e => onPointerOver(state, e);
    state.onClick = e => onClick(state, e);
    state.onOutside = e => { if (!root.contains(e.target) && !(how.anchor?.contains(e.target))) dismiss(state, false); };
    state.onViewport = () => { if (!state.sheet) dismiss(state, false); };
    root.addEventListener('keydown', state.onKeyDown);
    root.addEventListener('pointerover', state.onPointerOver);
    root.addEventListener('click', state.onClick);
    document.addEventListener('pointerdown', state.onOutside, true);
    window.addEventListener('resize', state.onViewport);
    window.addEventListener('blur', state.onViewport);
    // A page scroll: a menu under a button follows it; a menu at a point closes (it has nothing to follow).
    // Scrolling inside the menu itself is neither.
    state.onScroll = e => {
        if (root.contains(e.target)) return;
        if (how.anchor) placeRoot(state);
        else state.onViewport();
    };
    window.addEventListener('scroll', state.onScroll, true);
    if (state.sheet) lockScroll(true);

    placeRoot(state);
    state.rootList.style.visibility = 'visible';
    if (focusFirst) itemsOf(state.rootList)[0]?.focus({ preventScroll: true });
    else state.rootList.focus({ preventScroll: true });
}

/** Closes menu `id`; the component calls it after removing or hiding the menu. */
export function close(id, restore) {
    const state = states.get(id);
    if (!state) return;
    states.delete(id);
    clearTimeout(state.subTimer);
    const { root } = state;
    root.removeEventListener('keydown', state.onKeyDown);
    root.removeEventListener('pointerover', state.onPointerOver);
    root.removeEventListener('click', state.onClick);
    document.removeEventListener('pointerdown', state.onOutside, true);
    window.removeEventListener('resize', state.onViewport);
    window.removeEventListener('blur', state.onViewport);
    window.removeEventListener('scroll', state.onScroll, true);
    if (state.sheet) lockScroll(false);
    if ((restore ?? state.restoreFocus) && state.returnTo?.isConnected) state.returnTo.focus({ preventScroll: true });
}

// ── Context targets: right click and long press ────────────────────────────

const LONG_PRESS = 550;

/** Listens on the target (a selector, or the wrapper's first element) for a right click or a long press. */
export function watchContext(key, dotNet, wrapper, selector) {
    unwatchContext(key);
    const target = selector ? document.querySelector(selector) : wrapper?.firstElementChild ?? wrapper;
    if (!target) return;
    const contextOf = el => el.closest?.('[data-nx-context]')?.getAttribute('data-nx-context') ?? null;
    const request = (x, y, el) => dotNet.invokeMethodAsync('OnContextRequested', x, y, contextOf(el));
    let timer = 0, pressed = false, startX = 0, startY = 0;
    const handlers = {
        contextmenu: e => {
            e.preventDefault();
            if (pressed) { pressed = false; return; }          // the long press already opened it
            request(e.clientX, e.clientY, e.target);
        },
        touchstart: e => {
            const t = e.touches[0];
            startX = t.clientX; startY = t.clientY; pressed = false;
            timer = setTimeout(() => { pressed = true; request(startX, startY, e.target); }, LONG_PRESS);
        },
        touchmove: e => {
            const t = e.touches[0];
            if (Math.abs(t.clientX - startX) > 10 || Math.abs(t.clientY - startY) > 10) clearTimeout(timer);
        },
        touchend: e => { clearTimeout(timer); if (pressed) e.preventDefault(); },
        touchcancel: () => clearTimeout(timer)
    };
    for (const [type, fn] of Object.entries(handlers)) target.addEventListener(type, fn, { passive: type !== 'touchend' && type !== 'contextmenu' });
    contextTargets.set(key, { target, handlers });
}

export function unwatchContext(key) {
    const entry = contextTargets.get(key);
    if (!entry) return;
    for (const [type, fn] of Object.entries(entry.handlers)) entry.target.removeEventListener(type, fn);
    contextTargets.delete(key);
}
