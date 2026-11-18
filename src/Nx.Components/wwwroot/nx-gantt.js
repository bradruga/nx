// Interop for NxGantt. The browser's part: the task list and the timeline scroll together (and each header with its
// body), the divider is dragged, a press outside the cell being edited ends the edit, a dependency is drawn by
// dragging from one bar's start or end handle onto another bar, and a bar is dragged (or one of its edges) to new
// days: an outline shows where it would land and .NET is told how many days on release. Everything else is .NET.

const states = new WeakMap();
const MIN_PANE = 120;   // px either side of the divider
const DRAG_START = 4;   // px the pointer moves before a press on a bar becomes a drag (a click still selects)

export function init(root, dotNet, todayX) {
    if (!root || states.has(root)) return;
    const q = selector => root.querySelector(selector);
    const state = {
        dotNet,
        treeHead: q('.nx-gantt-tree-head'), treeBody: q('.nx-gantt-tree-body'),
        timeHead: q('.nx-gantt-time-head'), timeBody: q('.nx-gantt-time-body'),
        divider: q('.nx-gantt-divider'), syncing: false
    };
    states.set(root, state);

    // One pane's scroll moves the other's rows and its own header; the guard stops the echo.
    const sync = (from, to, head) => () => {
        head.scrollLeft = from.scrollLeft;
        if (state.syncing) return;
        state.syncing = true;
        to.scrollTop = from.scrollTop;
        requestAnimationFrame(() => { state.syncing = false; });
    };
    state.onTreeScroll = sync(state.treeBody, state.timeBody, state.treeHead);
    state.onTimeScroll = sync(state.timeBody, state.treeBody, state.timeHead);
    state.treeBody.addEventListener('scroll', state.onTreeScroll, { passive: true });
    state.timeBody.addEventListener('scroll', state.onTimeScroll, { passive: true });

    // Open at today, a little way in from the left.
    state.timeBody.scrollLeft = Math.max(0, todayX - 3 * slotWidth(root));

    state.onDividerDown = e => dragDivider(root, state, e);
    state.onDividerKey = e => {
        if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return;
        e.preventDefault();
        setWidth(root, state, treeWidth(root) + (e.key === 'ArrowRight' ? 24 : -24), true);
    };
    state.divider.addEventListener('pointerdown', state.onDividerDown);
    state.divider.addEventListener('keydown', state.onDividerKey);

    state.onHandleDown = e => {
        const handle = e.target.closest('[data-nx-gantt-handle]');
        if (handle) { dragLink(root, state, handle, e); return; }
        const bar = e.button === 0 ? e.target.closest('[data-nx-gantt-drag]') : null;
        if (bar) dragBar(root, state, bar, e.target.closest('[data-nx-gantt-grip]')?.dataset.nxGanttGrip ?? 'move', e);
    };
    state.timeBody.addEventListener('pointerdown', state.onHandleDown);

    // Alt+Left/Right on a row move its bar (.NET); the browser's Back and Forward must not also run.
    state.onRowKey = e => {
        if (e.altKey && (e.key === 'ArrowLeft' || e.key === 'ArrowRight') && e.target.closest('[role="row"]')) e.preventDefault();
    };
    state.treeBody.addEventListener('keydown', state.onRowKey);
}

function slotWidth(root) {
    return parseFloat(getComputedStyle(root.querySelector('.nx-gantt-canvas')).getPropertyValue('--nx-gantt-slot')) || 30;
}

function treeWidth(root) {
    return root.querySelector('.nx-gantt-tree').getBoundingClientRect().width;
}

function setWidth(root, state, width, report) {
    const total = root.querySelector('.nx-gantt-panes').getBoundingClientRect().width;
    const clamped = Math.round(Math.min(Math.max(width, MIN_PANE), total - MIN_PANE));
    root.style.setProperty('--nx-gantt-tree-width', clamped + 'px');
    if (report) state.dotNet.invokeMethodAsync('OnTreeWidth', clamped);
}

function dragDivider(root, state, e) {
    e.preventDefault();
    const startX = e.clientX, startWidth = treeWidth(root);
    state.divider.setPointerCapture(e.pointerId);
    root.classList.add('nx-gantt-resizing');
    const move = ev => setWidth(root, state, startWidth + ev.clientX - startX, false);
    const up = ev => {
        state.divider.removeEventListener('pointermove', move);
        state.divider.removeEventListener('pointerup', up);
        root.classList.remove('nx-gantt-resizing');
        setWidth(root, state, startWidth + ev.clientX - startX, true);
    };
    state.divider.addEventListener('pointermove', move);
    state.divider.addEventListener('pointerup', up);
}

// From a handle, a line follows the pointer; let go over a bar (its handle picks start or end, else its start).
function dragLink(root, state, handle, e) {
    e.preventDefault();
    e.stopPropagation();
    const canvas = root.querySelector('.nx-gantt-canvas');
    const from = handle.closest('[data-nx-gantt-task]');
    const box = canvas.getBoundingClientRect();
    const origin = handle.getBoundingClientRect();
    const x1 = origin.left + origin.width / 2 - box.left, y1 = origin.top + origin.height / 2 - box.top;
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('class', 'nx-gantt-drag-line');
    svg.setAttribute('width', canvas.scrollWidth);
    svg.setAttribute('height', canvas.scrollHeight);
    const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
    line.setAttribute('x1', x1); line.setAttribute('y1', y1); line.setAttribute('x2', x1); line.setAttribute('y2', y1);
    svg.append(line);
    canvas.append(svg);
    canvas.classList.add('nx-gantt-linking');
    const move = ev => {
        line.setAttribute('x2', ev.clientX - box.left);
        line.setAttribute('y2', ev.clientY - box.top);
    };
    const up = ev => {
        document.removeEventListener('pointermove', move);
        document.removeEventListener('pointerup', up);
        canvas.classList.remove('nx-gantt-linking');
        svg.remove();
        const target = document.elementFromPoint(ev.clientX, ev.clientY);
        const toBar = target?.closest('[data-nx-gantt-task]');
        if (!toBar || toBar === from || !root.contains(toBar)) return;
        const toHandle = target.closest('[data-nx-gantt-handle]');
        state.dotNet.invokeMethodAsync('OnLinkDrop', Number(from.dataset.nxGanttTask), handle.dataset.nxGanttHandle === 'end',
            Number(toBar.dataset.nxGanttTask), toHandle?.dataset.nxGanttHandle === 'end');
    };
    document.addEventListener('pointermove', move);
    document.addEventListener('pointerup', up);
}

// A press on a bar moves it; on a grip at either end, that end. An outline snaps to whole days and the bar itself stays
// put until .NET draws it again, so a page that refuses or adjusts the dates never leaves it in the wrong place.
// Escape, or a cancelled pointer, drops the drag.
function dragBar(root, state, bar, mode, e) {
    const canvas = root.querySelector('.nx-gantt-canvas');
    const slot = slotWidth(root);
    const startX = e.clientX, left = bar.offsetLeft, width = bar.offsetWidth;
    const span = Math.max(1, Math.round(width / slot));     // the bar's days; an end never crosses the other
    let ghost = null, days = 0;
    const move = ev => {
        const dx = ev.clientX - startX;
        if (!ghost) {
            if (Math.abs(dx) < DRAG_START) return;
            ghost = document.createElement('div');
            ghost.className = 'nx-gantt-bar-ghost';
            ghost.style.top = bar.offsetTop + 'px';
            ghost.style.height = bar.offsetHeight + 'px';
            canvas.append(ghost);
            bar.classList.add('nx-gantt-bar-dragging');
            root.classList.add(mode === 'move' ? 'nx-gantt-moving' : 'nx-gantt-sizing');
        }
        days = Math.round(dx / slot);
        if (mode === 'start') days = Math.min(days, span - 1);
        if (mode === 'end') days = Math.max(days, 1 - span);
        const shift = days * slot;
        ghost.style.left = (mode === 'end' ? left : left + shift) + 'px';
        ghost.style.width = (mode === 'move' ? width : mode === 'start' ? width - shift : width + shift) + 'px';
    };
    const finish = commit => {
        document.removeEventListener('pointermove', move);
        document.removeEventListener('pointerup', up);
        document.removeEventListener('pointercancel', cancel);
        document.removeEventListener('keydown', key, true);
        bar.classList.remove('nx-gantt-bar-dragging');
        root.classList.remove('nx-gantt-moving', 'nx-gantt-sizing');
        const dragged = ghost !== null;
        ghost?.remove();
        if (commit && dragged && days !== 0) state.dotNet.invokeMethodAsync('OnBarDrop', Number(bar.dataset.nxGanttTask), mode, days);
    };
    const up = () => finish(true);
    const cancel = () => finish(false);
    const key = ev => {
        if (ev.key !== 'Escape' || !ghost) return;
        ev.preventDefault();
        ev.stopPropagation();
        finish(false);
    };
    document.addEventListener('pointermove', move);
    document.addEventListener('pointerup', up);
    document.addEventListener('pointercancel', cancel);
    document.addEventListener('keydown', key, true);
}

// While a cell is edited, a press anywhere outside it ends the edit (its own popups live inside it).
export function watchEdit(root) {
    const state = states.get(root);
    if (!state) return;
    unwatchEdit(root);
    state.onOutside = e => {
        const cell = root.querySelector('.nx-gantt-td-editing');
        if (cell && !cell.contains(e.target)) state.dotNet.invokeMethodAsync('OnEditOutside');
    };
    document.addEventListener('pointerdown', state.onOutside, true);
}

export function unwatchEdit(root) {
    const state = states.get(root);
    if (state?.onOutside) {
        document.removeEventListener('pointerdown', state.onOutside, true);
        state.onOutside = null;
    }
}

export function focusRow(root, id) {
    const row = root?.querySelector(`#${CSS.escape(id)}`);
    row?.focus({ preventScroll: true });
    row?.scrollIntoView({ block: 'nearest' });
}

export function dispose(root) {
    const state = root && states.get(root);
    if (!state) return;
    unwatchEdit(root);
    state.treeBody.removeEventListener('scroll', state.onTreeScroll);
    state.timeBody.removeEventListener('scroll', state.onTimeScroll);
    state.divider.removeEventListener('pointerdown', state.onDividerDown);
    state.divider.removeEventListener('keydown', state.onDividerKey);
    state.timeBody.removeEventListener('pointerdown', state.onHandleDown);
    state.treeBody.removeEventListener('keydown', state.onRowKey);
    states.delete(root);
}
