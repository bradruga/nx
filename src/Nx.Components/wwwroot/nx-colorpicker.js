// Interop for NxColorPicker. Placing the popup and the outside press come from nx-popup.js. Here: arrow keys
// moving focus through the palette grid (by a row for Up and Down), done in the browser so fast key presses
// never outrun a server round trip; focusing the selected swatch when the popup opens; and closing it when
// focus leaves it (Tab), which Blazor's focus events cannot tell.

import { open as openPopup, close as closePopup } from './nx-popup.js';

const grids = new WeakMap();

export function open(field, popup, body, palette, dotNet) {
    openPopup(field, popup, body, 360, dotNet, 'OnOutsidePress');
    const columns = () => parseInt(getComputedStyle(palette).getPropertyValue('--nx-colorpicker-columns')) || 10;
    const onKeyDown = e => {
        if (e.altKey || e.ctrlKey || e.metaKey) return;
        const cells = [...palette.querySelectorAll('.nx-colorpicker-cell')];
        const current = cells.indexOf(document.activeElement);
        if (current < 0) return;
        const step = { ArrowRight: 1, ArrowLeft: -1, ArrowDown: columns(), ArrowUp: -columns() }[e.key];
        let next;
        if (step !== undefined) next = current + step;
        else if (e.key === 'Home') next = 0;
        else if (e.key === 'End') next = cells.length - 1;
        else return;
        e.preventDefault();
        if (next < 0 || next >= cells.length) return;
        cells[current].tabIndex = -1;
        cells[next].tabIndex = 0;
        cells[next].focus();
    };
    const onFocusOut = e => {
        if (e.relatedTarget && !popup.contains(e.relatedTarget) && e.relatedTarget !== field)
            dotNet.invokeMethodAsync('OnOutsidePress');
    };
    palette.addEventListener('keydown', onKeyDown);
    popup.addEventListener('focusout', onFocusOut);
    grids.set(field, { palette, popup, onKeyDown, onFocusOut });
    (palette.querySelector('.nx-colorpicker-cell[aria-selected="true"]') ?? palette.querySelector('.nx-colorpicker-cell'))?.focus();
}

export function close(field) {
    const state = field && grids.get(field);
    if (state) {
        state.palette.removeEventListener('keydown', state.onKeyDown);
        state.popup.removeEventListener('focusout', state.onFocusOut);
        grids.delete(field);
    }
    closePopup(field);
}
