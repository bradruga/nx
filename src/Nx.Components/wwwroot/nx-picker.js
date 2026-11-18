// Interop for NxDatePicker, NxDateRangePicker, NxTimePicker and NxDateTimePicker. Placing the popup, locking
// scroll under the phone sheet and watching the viewport come from nx-popup.js; masking typed times comes
// from nx-input.js. Here: the keys the open popup takes over from the input, and stepping a date's parts.

import { open as openPopup, close as closePopup, unwatchViewport } from './nx-popup.js';
import { mask, preventKeys, dispose as disposeInput } from './nx-input.js';
export { watchViewport, unwatchViewport, lockScroll } from './nx-popup.js';
export { mask, preventKeys };

const states = new WeakMap();
const steppers = new WeakMap();

// The popup is placed against the field's bordered box, not the whole field with its label.
const boxOf = input => input.closest('.nx-field-box') ?? input;

export function open(input, popup, body, maxHeight, dotNet, outsideMethod) {
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
    const item = list && (list.querySelector('.nx-timepicker-item-active') ?? list.querySelector('[aria-selected="true"]'));
    item?.scrollIntoView({ block: focus ? 'center' : 'nearest' });
    if (focus) (item ?? list?.querySelector('button'))?.focus();
}

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
