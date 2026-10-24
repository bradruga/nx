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

// Scrolls the element matching `selector` inside `container` into view, e.g. a tree's keyboard item.
export function reveal(container, selector) {
    container?.querySelector(selector)?.scrollIntoView({ block: 'nearest' });
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
    if (state.onEditMask) {
        element.removeEventListener('beforeinput', state.onEditMask);
        element.removeEventListener('focus', state.onEditFocus);
    }
    states.delete(element);
}

// NxMaskedTextBox: typing into a fixed mask such as "00-0000". Rule characters are slots (0 digit, 9 digit or
// space, # digit space + -, L letter, ? letter or space, A letter or digit, a letter digit or space, & any but
// space, C any); others are literals, and \ makes the next one literal. MaskPattern.cs reads the same rules.
// Every edit is made here — a typed character goes in the next slot that takes it, Backspace and Delete empty a
// slot — and reported to .NET as an input event, so the text never waits on a round trip.
const accepts = (rule, c) => ({
    '0': /\d/, '9': /[\d ]/, '#': /[\d +-]/, 'L': /\p{L}/u, '?': /[\p{L} ]/u,
    'A': /[\p{L}\d]/u, 'a': /[\p{L}\d ]/u, '&': /\S/, 'C': /[\s\S]/
})[rule]?.test(c) ?? false;

function parseMask(mask) {
    const parts = [];
    for (let i = 0; i < mask.length; i++) {
        const c = mask[i];
        if (c === '\\' && i + 1 < mask.length) parts.push({ literal: mask[++i] });
        else if ('09#L?Aa&C'.includes(c)) parts.push({ rule: c });
        else parts.push({ literal: c });
    }
    return parts;
}

export function editMask(input, mask, prompt) {
    if (!input) return;
    const state = states.get(input) ?? {};
    state.maskParts = parseMask(mask ?? '');
    state.maskPrompt = prompt || '_';
    if (!state.onEditMask) {
        const template = () => state.maskParts.map(p => p.rule ? state.maskPrompt : p.literal).join('');
        const slot = i => state.maskParts[i]?.rule;
        const nextSlot = from => { let i = from; while (i < state.maskParts.length && !slot(i)) i++; return i; };
        const prevSlot = from => { let i = from - 1; while (i >= 0 && !slot(i)) i--; return i; };
        const commit = (chars, caret) => {
            input.value = chars.join('');
            input.setSelectionRange(caret, caret);
            input.dispatchEvent(new Event('input', { bubbles: true }));
        };
        state.onEditMask = e => {
            if (input.readOnly) return;
            e.preventDefault();
            const chars = (input.value.length === state.maskParts.length ? input.value : template()).split('');
            let start = input.selectionStart ?? 0;
            const end = input.selectionEnd ?? start;
            const clear = (from, to) => { for (let i = from; i < to; i++) if (slot(i)) chars[i] = state.maskPrompt; };
            if (e.inputType.startsWith('insert')) {
                clear(start, end);
                let at = start;
                for (const c of [...(e.data ?? e.dataTransfer?.getData('text/plain') ?? '')]) {
                    // A typed literal steps over the same literal ahead; any other character goes in the next slot that takes it.
                    let i = at;
                    while (i < state.maskParts.length && !slot(i) && state.maskParts[i].literal !== c) i++;
                    if (i < state.maskParts.length && !slot(i)) { at = i + 1; continue; }
                    i = nextSlot(at);
                    if (i < state.maskParts.length && accepts(slot(i), c)) { chars[i] = c; at = i + 1; }
                }
                commit(chars, Math.min(nextSlot(at), state.maskParts.length));
            } else if (e.inputType === 'deleteContentBackward' && start === end) {
                const i = prevSlot(start);
                if (i >= 0) { chars[i] = state.maskPrompt; start = i; }
                commit(chars, start);
            } else if (e.inputType === 'deleteContentForward' && start === end) {
                const i = nextSlot(start);
                if (i < chars.length) chars[i] = state.maskPrompt;
                commit(chars, start);
            } else if (e.inputType.startsWith('delete')) {
                clear(start, end);
                commit(chars, start);
            }
        };
        // An empty field shows the mask once focused, with the caret on the first slot.
        state.onEditFocus = () => {
            if (input.readOnly || input.value.length === state.maskParts.length) return;
            const chars = template().split('');
            commit(chars, nextSlot(0));
        };
        input.addEventListener('beforeinput', state.onEditMask);
        input.addEventListener('focus', state.onEditFocus);
    }
    states.set(input, state);
}
