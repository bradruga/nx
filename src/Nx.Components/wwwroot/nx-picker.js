// Interop for NxDatePicker, NxDateRangePicker, NxTimePicker and NxDateTimePicker. Placing the popup, locking
// scroll under the phone sheet and watching the viewport come from nx-popup.js; masking typed times comes
// and swapping in the edit text on focus come from nx-input.js. Here: the keys the open popup takes over from the
// input, stepping a date's parts, the browser's time zone, and the phone sheet's time columns.

import { open as openPopup, close as closePopup, unwatchViewport } from './nx-popup.js';
import { mask, preventKeys, editOnFocus, roving, dispose as disposeInput } from './nx-input.js';
export { watchViewport, unwatchViewport, lockScroll } from './nx-popup.js';
export { mask, preventKeys, editOnFocus };

const states = new WeakMap();
const steppers = new WeakMap();

// The popup is placed against the field's bordered box, not the whole field with its label.
const boxOf = input => input.closest('.nx-field-box') ?? input;

export function open(input, popup, body, maxHeight, dotNet, outsideMethod) {
    if (!input) return;
    return openPopup(boxOf(input), popup, body, maxHeight, dotNet, outsideMethod);
}

export function close(input) {
    if (input) closePopup(boxOf(input));
}

// While the popup is open the arrow and page keys move through it and Enter picks, so their
// browser defaults (caret moves, form submit) are cancelled; Alt+Down opens it at any time. Closed,
// the input behaves like any text box.
export function init(input) {
    if (!input || states.has(input)) return;
    const onKeyDown = e => {
        const open = input.getAttribute('aria-expanded') === 'true';
        if (e.altKey && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) e.preventDefault();
        else if (open && ['ArrowDown', 'ArrowUp', 'ArrowLeft', 'ArrowRight', 'PageDown', 'PageUp', 'Enter', 'Escape'].includes(e.key))
            e.preventDefault();
    };
    input.addEventListener('keydown', onKeyDown);
    states.set(input, onKeyDown);
}

export function dispose(input) {
    if (!input) return;
    const onKeyDown = states.get(input);
    if (onKeyDown) input.removeEventListener('keydown', onKeyDown);
    states.delete(input);
    disposeStepper(input);
    close(input);
    unwatchViewport(input);
    disposeInput(input);
}

// Brings the time list's active (else selected) option into view; in the phone sheet also focuses it.
export function scrollActive(list, focus) {
    const show = () => {
        const item = list && (list.querySelector('.nx-timepicker-item-active') ?? list.querySelector('[aria-selected="true"]'));
        item?.scrollIntoView({ block: focus ? 'center' : 'nearest' });
        return item;
    };
    const item = show();
    if (focus) (item ?? list?.querySelector('button'))?.focus();
    if (!list || focus) return;
    // On a slow connection the render that moves the active option can land after this call: follow it for a moment.
    followers.get(list)?.disconnect();
    const observer = new MutationObserver(show);
    observer.observe(list, { subtree: true, attributes: true, attributeFilter: ['class', 'aria-selected'] });
    followers.set(list, observer);
    setTimeout(() => { if (followers.get(list) === observer) { observer.disconnect(); followers.delete(list); } }, 1000);
}

const followers = new WeakMap();   // time list → the observer that keeps its active option in view

// NxDatePicker, while its calendar is closed: Up and Down step the date part under the caret (day, month or year),
// as a spin box would; an empty or unreadable field starts from today. The text is typed freely, so its numbers are
// read in order ("12/5/26" as well as "12/05/2026"); it is rewritten here in the full pattern, the part stays
// selected, and an input and a change event tell .NET, which commits it. pattern is the full text's shape, e.g.
// "99/99/9999"; order is its parts' pattern letters, e.g. "Mdy".
export function stepDate(input, pattern, order) {
    if (!input) return;
    const state = steppers.get(input) ?? {};
    state.parts = [];
    let at = 0;
    for (const m of (pattern ?? '').matchAll(/(9+)([^9]*)/g)) {
        state.parts.push({ start: at, length: m[1].length, kind: order?.[state.parts.length] });
        at += m[1].length + m[2].length;
    }
    state.width = at;
    if (!state.onKeyDown) {
        state.onKeyDown = e => {
            if ((e.key !== 'ArrowUp' && e.key !== 'ArrowDown') || e.altKey || e.ctrlKey || e.metaKey || input.readOnly || state.parts.length === 0) return;
            if (input.getAttribute('aria-expanded') === 'true') return;     // the open calendar has the arrows
            e.preventDefault();
            const parts = state.parts;
            const typed = [...input.value.matchAll(/\d+/g)];
            const read = kind => {
                const i = parts.findIndex(x => x.kind === kind);
                return typed.length === parts.length && i >= 0 ? parseInt(typed[i][0], 10) : NaN;
            };
            let d = read('d'), m = read('M'), y = read('y');
            if (y < 100) y += 2000;
            const complete = [d, m, y].every(n => n > 0) && m <= 12 && !/[a-z]/i.test(input.value);
            const caret = input.selectionStart ?? 0;
            const part = (complete ? parts[typed.findLastIndex(t => t.index <= caret)] : parts.findLast(p => p.start <= caret)) ?? parts[0];
            if (!complete) {
                const today = new Date();
                [d, m, y] = [today.getDate(), today.getMonth() + 1, today.getFullYear()];
            } else {
                const step = e.key === 'ArrowUp' ? 1 : -1;
                const daysIn = (month, year) => new Date(year, month, 0).getDate();
                if (part.kind === 'd') d = ((d - 1 + step + daysIn(m, y)) % daysIn(m, y)) + 1;
                else if (part.kind === 'M') m = ((m - 1 + step + 12) % 12) + 1;
                else y = Math.max(1, y + step);
                d = Math.min(d, daysIn(m, y));
            }
            const values = { d, M: m, y };
            let text = '';
            parts.forEach((p, i) => text += String(values[p.kind]).padStart(p.length, '0') + (state.separators[i] ?? ''));
            input.value = text;
            input.setSelectionRange(part.start, part.start + part.length);
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
        };
        input.addEventListener('keydown', state.onKeyDown);
    }
    state.separators = [...(pattern ?? '').matchAll(/9+([^9]*)/g)].map(m => m[1]);
    steppers.set(input, state);
}

export function disposeStepper(input) {
    const state = input && steppers.get(input);
    if (state?.onKeyDown) input.removeEventListener('keydown', state.onKeyDown);
    steppers.delete(input);
}

// The browser's time zone, for the offset of a new DateTimeOffset: its IANA name (.NET works out the offset on the
// picked date) and, should the server not know that zone, the offset right now in minutes east of UTC.
export function timeZone() {
    return { id: Intl.DateTimeFormat().resolvedOptions().timeZone ?? null, offset: -new Date().getTimezoneOffset() };
}

// NxTimeColumns (the phone sheet's hour, minute and AM/PM columns): each column is one tab stop whose arrow keys
// move between its options, opens with its selected option in the middle, and the first column takes focus.
export function initColumns(container) {
    if (!container) return;
    const columns = [...container.querySelectorAll('[role="listbox"]')];
    for (const column of columns) {
        roving(column, '[role="option"]');
        column.querySelector('[aria-selected="true"]')?.scrollIntoView({ block: 'center' });
    }
    (columns[0]?.querySelector('[aria-selected="true"]') ?? columns[0]?.querySelector('[role="option"]:not(:disabled)'))?.focus({ preventScroll: true });
}

// After a pick moved another column's selection (an hour that pulled the minute to Min), bring it into view.
export function revealColumns(container) {
    for (const selected of container?.querySelectorAll('[role="option"][aria-selected="true"]') ?? [])
        selected.scrollIntoView({ block: 'nearest' });
}
