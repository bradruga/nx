// Interop for NxDatePicker, NxDateRangePicker and NxTimePicker. Placing the popup, locking scroll
// under the phone sheet and watching the viewport come from nx-popup.js; masking typed text comes
// from nx-input.js. Here: the keys the open popup takes over from the input.

import { open as openPopup, close as closePopup, unwatchViewport } from './nx-popup.js';
import { mask, preventKeys, dispose as disposeInput } from './nx-input.js';
export { watchViewport, unwatchViewport, lockScroll } from './nx-popup.js';
export { mask, preventKeys };

const states = new WeakMap();

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
