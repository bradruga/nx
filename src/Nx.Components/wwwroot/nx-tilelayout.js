// Interop for NxTileLayout: dragging a tile by its header onto another, and resizing a tile from its corner
// handle. Both follow the pointer here (the dragged tile moves with it, the resized tile takes its new span at
// once) and tell .NET only on release, which reorders or stores the spans and re-renders. Focusing a tile's
// header after a keyboard move also lives here: the move re-inserts the element, which drops focus.

const states = new WeakMap();
const DragThreshold = 4;

const tileOf = (grid, element) => {
    const tile = element?.closest?.('[data-nx-tile]');
    return tile && tile.parentElement === grid ? tile : null;
};

function trackSize(grid) {
    const style = getComputedStyle(grid);
    const columns = style.gridTemplateColumns.split(' ').map(parseFloat).filter(n => !isNaN(n));
    const rows = style.gridAutoRows.split(' ').map(parseFloat).filter(n => !isNaN(n));
    return {
        column: columns[0] ?? grid.clientWidth,
        columnCount: columns.length || 1,
        row: rows[0],                                   // NaN for auto rows: height is not resizable
        gap: parseFloat(style.columnGap) || 0,
        rowGap: parseFloat(style.rowGap) || 0
    };
}

export function init(grid, dotNet) {
    if (!grid || states.has(grid)) return;
    const state = { drag: null, resize: null };

    const onDown = e => {
        if (e.button !== 0) return;
        const handle = e.target.closest('.nx-tile-resize');
        const tile = tileOf(grid, e.target);
        if (!tile) return;
        if (handle && grid.dataset.resizable === 'true') {
            e.preventDefault();
            handle.setPointerCapture(e.pointerId);
            const t = trackSize(grid);
            state.resize = { tile, handle, pointerId: e.pointerId, track: t, rect: tile.getBoundingClientRect(),
                             cols: +tile.style.getPropertyValue('--nx-tile-cols') || 1, rows: +tile.style.getPropertyValue('--nx-tile-rows') || 1 };
            tile.classList.add('nx-tile-resizing');
            return;
        }
        const header = e.target.closest('.nx-tile-header');
        if (!header || grid.dataset.reorderable !== 'true' || e.target.closest('button, a, input, select, textarea, [contenteditable]')) return;
        state.drag = { tile, header, pointerId: e.pointerId, x: e.clientX, y: e.clientY, started: false, target: null };
    };

    const onMove = e => {
        const r = state.resize;
        if (r && e.pointerId === r.pointerId) {
            const t = r.track;
            const cols = Math.min(t.columnCount, Math.max(1, Math.round((e.clientX - r.rect.left + t.gap) / (t.column + t.gap))));
            const rows = isNaN(t.row) ? r.rows : Math.max(1, Math.round((e.clientY - r.rect.top + t.rowGap) / (t.row + t.rowGap)));
            r.tile.style.setProperty('--nx-tile-cols', cols);
            r.tile.style.setProperty('--nx-tile-rows', rows);
            r.cols = cols;
            r.rows = rows;
            return;
        }
        const d = state.drag;
        if (!d || e.pointerId !== d.pointerId) return;
        if (!d.started) {
            if (Math.hypot(e.clientX - d.x, e.clientY - d.y) < DragThreshold) return;
            d.started = true;
            d.header.setPointerCapture(e.pointerId);
            d.tile.classList.add('nx-tile-dragging');
            grid.classList.add('nx-tilelayout-dragging');
        }
        d.tile.style.transform = `translate(${e.clientX - d.x}px, ${e.clientY - d.y}px)`;
        const under = document.elementsFromPoint(e.clientX, e.clientY).map(el => tileOf(grid, el)).find(t => t && t !== d.tile) ?? null;
        if (under !== d.target) {
            d.target?.classList.remove('nx-tile-drop-target');
            under?.classList.add('nx-tile-drop-target');
            d.target = under;
        }
    };

    const onUp = e => {
        const r = state.resize;
        if (r && e.pointerId === r.pointerId) {
            state.resize = null;
            r.tile.classList.remove('nx-tile-resizing');
            dotNet.invokeMethodAsync('OnTileResize', r.tile.dataset.nxTile, r.cols, r.rows);
            return;
        }
        const d = state.drag;
        if (!d || e.pointerId !== d.pointerId) return;
        state.drag = null;
        if (!d.started) return;
        d.tile.style.transform = '';
        d.tile.classList.remove('nx-tile-dragging');
        grid.classList.remove('nx-tilelayout-dragging');
        d.target?.classList.remove('nx-tile-drop-target');
        if (e.type === 'pointerup' && d.target)
            dotNet.invokeMethodAsync('OnTileDrop', d.tile.dataset.nxTile, d.target.dataset.nxTile);
    };

    // Ctrl and Shift with the arrow keys move and resize a tile from its header; they must not scroll the page.
    const onKeyDown = e => {
        if ((e.ctrlKey || e.shiftKey) && e.key.startsWith('Arrow') && e.target.classList.contains('nx-tile-header')) e.preventDefault();
    };

    grid.addEventListener('keydown', onKeyDown);
    grid.addEventListener('pointerdown', onDown);
    grid.addEventListener('pointermove', onMove);
    grid.addEventListener('pointerup', onUp);
    grid.addEventListener('pointercancel', onUp);
    states.set(grid, { onKeyDown, onDown, onMove, onUp });
}

export function focusTile(grid, key) {
    grid?.querySelector(`:scope > [data-nx-tile="${CSS.escape(key)}"] > .nx-tile-header`)?.focus();
}

export function dispose(grid) {
    const state = grid && states.get(grid);
    if (!state) return;
    grid.removeEventListener('keydown', state.onKeyDown);
    grid.removeEventListener('pointerdown', state.onDown);
    grid.removeEventListener('pointermove', state.onMove);
    grid.removeEventListener('pointerup', state.onUp);
    grid.removeEventListener('pointercancel', state.onUp);
    states.delete(grid);
}
