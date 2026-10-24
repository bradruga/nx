// NxComboBox interop. Blazor owns the markup; this module only does what C# cannot: cancel the
// browser default for navigation keys and scroll the active option into view. Placing the popup,
// locking scroll and watching the viewport are shared with the date pickers in nx-popup.js.
// See NxComboBox.razor.cs, "JS interop".

import { unwatchViewport } from './nx-popup.js';
export { open, close, watchViewport, lockScroll } from './nx-popup.js';

const states = new WeakMap();

// Arrow and page keys would move the caret; Enter would submit an enclosing form while the
// list is open. With Multiple, Space toggles the highlighted item once the user has arrowed to it
// (otherwise it types a space). Blazor cannot decide preventDefault per key, and an attribute
// rendered by the server would arrive a round trip late for a fast Arrow-then-Space, so the
// "arrowed since typing" state is tracked here, mirroring NxComboBox.SpaceToggles.
export function init(input) {
    if (!input || states.has(input)) return;
    let navigated = false;
    const onKeyDown = e => {
        const open = input.getAttribute('aria-expanded') === 'true';
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp' || e.key === 'PageDown' || e.key === 'PageUp') {
            e.preventDefault();
            navigated = open && !e.altKey;
        } else if ((e.key === 'Enter' || e.key === 'Escape') && open) {
            e.preventDefault();
            if (e.key === 'Escape') navigated = false;
        } else if (e.key === ' ' && open && navigated && input.dataset.nxMultiple === 'true') {
            e.preventDefault();
        }
    };
    const onInput = () => { navigated = false; };
    input.addEventListener('keydown', onKeyDown);
    input.addEventListener('input', onInput);
    states.set(input, { onKeyDown, onInput });
}

export function dispose(input, field) {
    const state = input && states.get(input);
    if (state) {
        input.removeEventListener('keydown', state.onKeyDown);
        input.removeEventListener('input', state.onInput);
        states.delete(input);
    }
    unwatchViewport(field);
}

// Brings option `index` into view. A rendered option scrolls itself; under virtualization the
// option may not exist yet, so the list is scrolled by row arithmetic and Virtualize renders it.
export function scrollToIndex(list, index, itemHeight) {
    if (!list || index < 0) return;
    const option = list.querySelector(`[data-row="${index}"]`);
    if (option) {
        option.scrollIntoView({ block: 'nearest' });
        return;
    }
    // A multi-column list has a sticky header above the rows, covering their top.
    const header = list.querySelector('.nx-combobox-columns-header');
    const offset = header ? header.offsetHeight : 0;
    const top = offset + index * itemHeight;
    if (top - offset < list.scrollTop) list.scrollTop = top - offset;
    else if (top + itemHeight > list.scrollTop + list.clientHeight) list.scrollTop = top + itemHeight - list.clientHeight;
}
