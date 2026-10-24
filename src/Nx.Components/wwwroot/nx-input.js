// Shared input interop for NxNumericTextBox, NxToggleGroup, NxCalendar and the pickers. Blazor
// cannot cancel a key's browser default per key, mask typed text, select an input's text, or move
// focus without a server round trip, so those things are done here.

const states = new WeakMap();

// Cancels the browser default for the given keys on an element: arrows would move a caret or
// scroll the page while the component uses them to step a value or move focus.
export function preventKeys(element, keys) {
    if (!element) return;
    release(element);
    const onKeyDown = e => {
        if (keys.includes(e.key) && !e.altKey && !e.ctrlKey && !e.metaKey) e.preventDefault();
    };
    element.addEventListener('keydown', onKeyDown);
    states.set(element, { ...(states.get(element) ?? {}), onKeyDown });
}

// Roving tabindex: arrow keys move focus between a group's enabled items (wrapping), Home and End
// jump to the ends. Done in the browser so fast key presses never outrun a server round trip. In a
// radiogroup, the item focused is also clicked, so selection follows focus like native radios.
export function roving(container, itemSelector) {
    if (!container) return;
    release(container);
    const onKeyDown = e => {
        if (e.altKey || e.ctrlKey || e.metaKey) return;
        const keys = ['ArrowRight', 'ArrowDown', 'ArrowLeft', 'ArrowUp', 'Home', 'End'];
        if (!keys.includes(e.key)) return;
        const items = [...container.querySelectorAll(itemSelector)].filter(i => !i.disabled);
        const current = items.indexOf(document.activeElement);
        if (items.length === 0 || current < 0) return;
        e.preventDefault();
        const next =
            e.key === 'Home' ? 0 :
            e.key === 'End' ? items.length - 1 :
            e.key === 'ArrowRight' || e.key === 'ArrowDown' ? (current + 1) % items.length :
            (current - 1 + items.length) % items.length;
        items[next].focus();
        if (container.getAttribute('role') === 'radiogroup' && items[next].getAttribute('aria-checked') !== 'true')
            items[next].click();
    };
    container.addEventListener('keydown', onKeyDown);
    states.set(container, { ...(states.get(container) ?? {}), onKeyDown });
}

// Masked entry for the date and time pickers. The pattern is runs of 9 (a digit) between literal
// separators, e.g. "99/99/9999". While typing, a full run gets its separator added ("10" becomes
// "10/"), and typing a separator early pads the run with a zero ("1/" becomes "01/"), so both
// "10232026" and "1/5/2026" end up in the pattern. Done here, before Blazor reads the value:
// masking on the server would rewrite the input mid-typing. Pastes, deletions and text holding
// characters the pattern does not use (a month name, a dash) are left for the component to parse.
export function mask(input, pattern) {
    if (!input) return;
    const state = states.get(input) ?? {};
    state.segments = pattern ? [...pattern.matchAll(/(9+)([^9]*)/g)].map(m => ({ length: m[1].length, separator: m[2] })) : null;
    if (!state.onMask) {
        state.onMask = e => {
            const segments = state.segments;
            if (!segments || !e.inputType?.startsWith('insertText')) return;
            const literals = new Set(segments.flatMap(s => [...s.separator]));
            const value = input.value;
            if ([...value].some(c => !/[0-9]/.test(c) && !literals.has(c))) return;
            let out = '', run = '', i = 0;
            for (const c of value) {
                if (i >= segments.length) break;
                const segment = segments[i];
                if (/[0-9]/.test(c)) {
                    if (run.length < segment.length) run += c;
                    if (run.length === segment.length && segment.separator) {
                        out += run + segment.separator;
                        run = '';
                        i++;
                    }
                } else if (run.length > 0 && segment.separator) {
                    out += run.padStart(segment.length, '0') + segment.separator;
                    run = '';
                    i++;
                }
            }
            out += run;
            if (out !== value) input.value = out;
        };
        input.addEventListener('input', state.onMask);
    }
    states.set(input, state);
}

// Auto-resize for NxTextArea: the textarea grows with its text between minRows and maxRows lines, then
// scrolls. Measured on every input in the browser, so the height follows each keystroke.
export function autosize(textarea, minRows, maxRows) {
    if (!textarea) return;
    const state = states.get(textarea) ?? {};
    state.rows = { minRows, maxRows };
    if (!state.onResize) {
        state.onResize = () => {
            const { minRows: min, maxRows: max } = state.rows;
            const style = getComputedStyle(textarea);
            const line = parseFloat(style.lineHeight) || 20;
            const pad = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
            textarea.style.height = 'auto';
            const wanted = textarea.scrollHeight;
            const lo = min * line + pad, hi = max ? max * line + pad : Infinity;
            textarea.style.height = `${Math.min(Math.max(wanted, lo), hi)}px`;
            textarea.style.overflowY = wanted > hi ? 'auto' : 'hidden';
        };
        textarea.addEventListener('input', state.onResize);
    }
    states.set(textarea, state);
    state.onResize();
}

// A formatted field ($1,234.50) is edited as plain text (1234.5), which the component puts in data-nx-edit.
// The swap happens here, the moment the input gains focus: done by the server a round trip later, it would
// land on whatever was typed meanwhile. The input event hands the edit text to .NET through @bind.
// Setting the value drops any selection, so a swap not made by a click selects the new text, as a Tab into a
// text field does, and typing replaces the number. With selectText every focus selects; a mouse focus ends
// with a mouseup that would drop the caret at the click point and undo that, so its default is cancelled.
export function editOnFocus(input, selectText) {
    if (!input) return;
    const state = states.get(input) ?? {};
    state.selectText = selectText;
    if (!state.onFocus) {
        let pointerDown = false;
        let justFocused = false;
        state.onPointerDown = () => { pointerDown = true; };
        state.onFocus = () => {
            const byPointer = pointerDown;
            pointerDown = false;
            const edit = input.dataset.nxEdit;
            const swapped = edit !== undefined && input.value !== edit;
            if (swapped) input.value = edit;
            if (state.selectText || (swapped && !byPointer)) input.select();
            justFocused = state.selectText;
            if (swapped) input.dispatchEvent(new Event('input', { bubbles: true }));
        };
        state.onBlur = () => { pointerDown = false; };
        state.onMouseUp = e => {
            if (justFocused) e.preventDefault();
            justFocused = false;
        };
        input.addEventListener('pointerdown', state.onPointerDown);
        input.addEventListener('focus', state.onFocus);
        input.addEventListener('blur', state.onBlur);
        input.addEventListener('mouseup', state.onMouseUp);
    }
    states.set(input, state);
}

export function release(element) {
    const state = element && states.get(element);
    if (state?.onKeyDown) {
        element.removeEventListener('keydown', state.onKeyDown);
        delete state.onKeyDown;
    }
}

export function dispose(element) {
    const state = element && states.get(element);
    if (!state) return;
    if (state.onKeyDown) element.removeEventListener('keydown', state.onKeyDown);
    if (state.onFocus) {
        element.removeEventListener('pointerdown', state.onPointerDown);
        element.removeEventListener('focus', state.onFocus);
        element.removeEventListener('blur', state.onBlur);
        element.removeEventListener('mouseup', state.onMouseUp);
    }
    if (state.onMask) element.removeEventListener('input', state.onMask);
    if (state.onResize) element.removeEventListener('input', state.onResize);
    states.delete(element);
}
