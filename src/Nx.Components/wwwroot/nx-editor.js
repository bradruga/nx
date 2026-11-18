// Interop for NxEditor. The editable area is a contenteditable div whose children belong to this module, not Blazor:
// commands (the browser's execCommand where it does the job, DOM edits for styles and tables), an undo history of
// snapshots (typing grouped by pauses), cleaning pasted HTML, and reporting what the caret is in. .NET hears the HTML
// after an edit (debounced while typing) and the caret's state when it changes.

import { open as openPopup, close as closePopup } from './nx-popup.js';

const editors = new Map();      // content id -> state
const HISTORY = 100;            // undo steps kept
const TYPING_PAUSE = 700;       // ms without typing that ends an undo step
const TABLE_STYLE = 'width:100%;border-collapse:collapse';
const CELL_STYLE = 'border:1px solid #c8c8cc;padding:4px 6px';
const BLOCKS = 'p,h1,h2,h3,h4,h5,h6,blockquote,pre';

// ── Setup ────────────────────────────────────────────────────────────────────

export function init(root, content, dotNet, html) {
    if (!content || editors.has(content.id)) return;
    const state = { root, content, dotNet, undo: [], redo: [], typing: false, typingTimer: 0, changeTimer: 0, range: null,
        lastHtml: null, reported: '', frame: 0, image: null, imageBox: null, dragImage: null, sizing: false };
    editors.set(content.id, state);
    document.execCommand('defaultParagraphSeparator', false, 'p');
    setContent(state, html);

    state.onBeforeInput = e => {
        if (e.inputType === 'historyUndo' || e.inputType === 'historyRedo') {
            e.preventDefault();
            e.inputType === 'historyUndo' ? undo(state) : redo(state);
        } else if (/^(insertText|insertCompositionText|insertParagraph|insertLineBreak|deleteContent|deleteWord|deleteSoftLine|deleteHardLine)/.test(e.inputType)) {
            beginTyping(state);
        } else {
            record(state);
        }
    };
    state.onInput = () => {
        if (!state.styling) normalizeMarks(state);   // a style tool normalizes itself, to keep its selection
        updateEmpty(state);
        saveRange(state);
        scheduleChange(state);
        reportSoon(state);
    };
    state.onKeyDown = e => keyDown(state, e);
    state.onPaste = e => paste(state, e);
    state.onBlur = () => { endTyping(state); flushChange(state); if (!state.sizing) deselectImage(state); };
    state.onMouseDown = e => {
        // Toolbar buttons and picture handles must not take the focus (and the selection) from the text.
        if (e.target.closest('[data-nx-editor-cmd], .nx-editor-image-handle')) e.preventDefault();
    };
    state.onPointerDown = e => {
        if (e.target.tagName === 'IMG' && content.isContentEditable) selectImage(state, e.target);
        else deselectImage(state);
    };
    state.onClick = e => { if (e.target === state.image) selectNode(state, e.target); };
    state.onDragStart = e => { state.dragImage = e.target.tagName === 'IMG' && content.isContentEditable ? e.target : null; };
    state.onDragEnd = () => { state.dragImage = null; };
    state.onDrop = e => dropImage(state, e);
    state.onPlace = () => placeImageBox(state);
    content.addEventListener('beforeinput', state.onBeforeInput);
    content.addEventListener('input', state.onInput);
    content.addEventListener('keydown', state.onKeyDown);
    content.addEventListener('paste', state.onPaste);
    content.addEventListener('blur', state.onBlur);
    content.addEventListener('pointerdown', state.onPointerDown);
    content.addEventListener('click', state.onClick);
    content.addEventListener('dragstart', state.onDragStart);
    content.addEventListener('dragend', state.onDragEnd);
    content.addEventListener('drop', state.onDrop);
    content.addEventListener('scroll', state.onPlace);
    root.addEventListener('mousedown', state.onMouseDown);
    if (editors.size === 1) document.addEventListener('selectionchange', onSelectionChange);
    reportSoon(state);
}

export function dispose(id) {
    const state = editors.get(id);
    if (!state) return;
    clearTimeout(state.typingTimer);
    clearTimeout(state.changeTimer);
    cancelAnimationFrame(state.frame);
    const { content, root } = state;
    deselectImage(state);
    content.removeEventListener('pointerdown', state.onPointerDown);
    content.removeEventListener('click', state.onClick);
    content.removeEventListener('dragstart', state.onDragStart);
    content.removeEventListener('dragend', state.onDragEnd);
    content.removeEventListener('drop', state.onDrop);
    content.removeEventListener('scroll', state.onPlace);
    content.removeEventListener('beforeinput', state.onBeforeInput);
    content.removeEventListener('input', state.onInput);
    content.removeEventListener('keydown', state.onKeyDown);
    content.removeEventListener('paste', state.onPaste);
    content.removeEventListener('blur', state.onBlur);
    root.removeEventListener('mousedown', state.onMouseDown);
    if (state.pickerButton) closePopup(state.pickerButton);
    editors.delete(id);
    if (editors.size === 0) document.removeEventListener('selectionchange', onSelectionChange);
}

// An outside change to Value: new text, a new history.
export function setHtml(id, html) {
    const state = editors.get(id);
    if (!state) return;
    setContent(state, html);
    state.undo = [];
    state.redo = [];
    reportSoon(state);
}

function setContent(state, html) {
    state.content.innerHTML = html || '<p><br></p>';
    state.range = null;
    state.lastHtml = getHtml(state);
    updateEmpty(state);
}

function isEmpty(content) {
    return !content.textContent.trim() && !content.querySelector('img,table,hr,iframe,video');
}

function getHtml(state) {
    return isEmpty(state.content) ? '' : state.content.innerHTML;
}

function updateEmpty(state) {
    state.content.toggleAttribute('data-empty', isEmpty(state.content));
}

// ── Value to .NET ────────────────────────────────────────────────────────────

function scheduleChange(state) {
    clearTimeout(state.changeTimer);
    state.changeTimer = setTimeout(() => flushChange(state), Number(state.content.dataset.debounce) || 0);
}

function flushChange(state) {
    clearTimeout(state.changeTimer);
    const html = getHtml(state);
    if (html === state.lastHtml) return;
    state.lastHtml = html;
    state.dotNet.invokeMethodAsync('OnContentChanged', html);
}

// After a command: one change, at once.
function changed(state) {
    updateEmpty(state);
    saveRange(state);
    flushChange(state);
    reportSoon(state);
}

// ── Selection ────────────────────────────────────────────────────────────────

function onSelectionChange() {
    const selection = document.getSelection();
    if (!selection.rangeCount) return;
    for (const state of editors.values()) {
        if (state.content.contains(selection.anchorNode)) {
            saveRange(state);
            reportSoon(state);
        }
    }
}

function saveRange(state) {
    const selection = document.getSelection();
    if (selection.rangeCount && state.content.contains(selection.anchorNode))
        state.range = selection.getRangeAt(0).cloneRange();
}

// Focus the text and put back the selection it had (the end when it never had one).
function restore(state) {
    const { content } = state;
    if (document.activeElement !== content) content.focus({ preventScroll: true });
    let range = state.range;
    if (!range || !content.contains(range.startContainer) || !content.contains(range.endContainer)) {
        range = document.createRange();
        range.selectNodeContents(content.lastElementChild ?? content);
        range.collapse(false);
    }
    const selection = document.getSelection();
    selection.removeAllRanges();
    selection.addRange(range);
}

export function restoreSelection(id) {
    const state = editors.get(id);
    if (state) restore(state);
}

function caretIn(state, node) {
    const range = document.createRange();
    range.selectNodeContents(node);
    range.collapse(true);
    const selection = document.getSelection();
    selection.removeAllRanges();
    selection.addRange(range);
    saveRange(state);
}

function anchorElement(state) {
    const node = state.range?.startContainer;
    if (!node || !state.content.contains(node)) return null;
    return node.nodeType === 1 ? node : node.parentElement;
}

// closest(), but only inside the text.
function within(state, element, selector) {
    const found = element?.closest(selector);
    return found && found !== state.content && state.content.contains(found) ? found : null;
}

// ── History ──────────────────────────────────────────────────────────────────

function pathTo(content, node) {
    const path = [];
    while (node && node !== content) {
        path.unshift([...node.parentNode.childNodes].indexOf(node));
        node = node.parentNode;
    }
    return node === content ? path : null;
}

function nodeAt(content, path) {
    let node = content;
    for (const i of path ?? []) node = node?.childNodes[i];
    return node;
}

function snapshot(state) {
    const selection = document.getSelection();
    const range = selection.rangeCount && state.content.contains(selection.anchorNode) ? selection.getRangeAt(0) : state.range;
    return {
        html: state.content.innerHTML,
        start: range ? pathTo(state.content, range.startContainer) : null, startOffset: range?.startOffset ?? 0,
        end: range ? pathTo(state.content, range.endContainer) : null, endOffset: range?.endOffset ?? 0
    };
}

function apply(state, shot) {
    state.content.innerHTML = shot.html;
    const start = shot.start && nodeAt(state.content, shot.start);
    const end = shot.end && nodeAt(state.content, shot.end);
    state.range = null;
    if (start && end) {
        try {
            const range = document.createRange();
            range.setStart(start, shot.startOffset);
            range.setEnd(end, shot.endOffset);
            state.range = range;
        } catch { /* the offsets no longer fit: the end of the text */ }
    }
    restore(state);
}

function push(stack, shot) {
    stack.push(shot);
    if (stack.length > HISTORY) stack.shift();
}

// Before a command: one undo step.
function record(state) {
    endTyping(state);
    push(state.undo, snapshot(state));
    state.redo = [];
}

// Typing: one undo step until a pause.
function beginTyping(state) {
    if (!state.typing) {
        push(state.undo, snapshot(state));
        state.redo = [];
        state.typing = true;
    }
    clearTimeout(state.typingTimer);
    state.typingTimer = setTimeout(() => endTyping(state), TYPING_PAUSE);
}

function endTyping(state) {
    clearTimeout(state.typingTimer);
    state.typing = false;
}

function undo(state) {
    endTyping(state);
    if (!state.undo.length) return;
    push(state.redo, snapshot(state));
    apply(state, state.undo.pop());
    changed(state);
}

function redo(state) {
    endTyping(state);
    if (!state.redo.length) return;
    push(state.undo, snapshot(state));
    apply(state, state.redo.pop());
    changed(state);
}

// ── Commands ─────────────────────────────────────────────────────────────────

const NATIVE = {
    bold: 'bold', italic: 'italic', underline: 'underline', strikethrough: 'strikeThrough',
    alignLeft: 'justifyLeft', alignCenter: 'justifyCenter', alignRight: 'justifyRight', alignJustify: 'justifyFull',
    unorderedList: 'insertUnorderedList', orderedList: 'insertOrderedList', indent: 'indent', outdent: 'outdent'
};

const STYLES = new Set(['fontSize', 'fontFamily', 'foreColor', 'backColor']);

const TABLE_EDITS = {
    addRowBefore: s => addRow(s, false), addRowAfter: s => addRow(s, true),
    addColumnBefore: s => addColumn(s, false), addColumnAfter: s => addColumn(s, true),
    deleteRow, deleteColumn, deleteTable, mergeCells, splitCell
};

export function exec(id, command, value) {
    const state = editors.get(id);
    if (!state || !state.content.isContentEditable) return;
    if (command === 'undo') return undo(state);
    if (command === 'redo') return redo(state);
    restore(state);
    record(state);
    document.execCommand('styleWithCSS', false, false);
    if (NATIVE[command]) document.execCommand(NATIVE[command]);
    else if (command === 'format') document.execCommand('formatBlock', false, `<${value || 'p'}>`);
    else if (STYLES.has(command)) applyStyle(state, command, value);
    else if (command === 'unlink') unlink(state);
    else if (TABLE_EDITS[command]) {
        TABLE_EDITS[command](state);
        // A span of 1 is no span.
        for (const cell of state.content.querySelectorAll('[colspan="1"], [rowspan="1"]')) {
            if (cell.getAttribute('colspan') === '1') cell.removeAttribute('colspan');
            if (cell.getAttribute('rowspan') === '1') cell.removeAttribute('rowspan');
        }
    }
    changed(state);
}

export function insertHtml(id, html) {
    const state = editors.get(id);
    if (!state) return;
    restore(state);
    record(state);
    document.execCommand('insertHTML', false, html);
    changed(state);
}

// Text color, font and size go through the browser's <font> commands (it splits elements and drops the same style
// inside the selection, or keeps it pending for the next typed text at a caret); normalizeMarks then turns every <font>
// into a styled span. An empty value removes the style: a sentinel color or face that becomes no span at all.
const NO_COLOR = '#010203';
const NO_FONT = 'nx-none';

function applyStyle(state, command, value) {
    // Read before the edit: turning <font> into spans below collapses the selection.
    const hadRange = !document.getSelection().isCollapsed;
    state.styling = true;   // execCommand raises input at once; its handler must leave the <font> to us
    try {
        if (command === 'foreColor') {
            document.execCommand('foreColor', false, value || NO_COLOR);
        } else if (command === 'fontFamily') {
            document.execCommand('fontName', false, value || NO_FONT);
        } else if (command === 'fontSize') {
            state.pendingSize = value || '';
            document.execCommand('fontSize', false, '7');
        } else {
            // Highlights are spans already; "none" is a transparent one, removed below.
            document.execCommand('styleWithCSS', false, true);
            document.execCommand('hiliteColor', false, value || 'transparent');
            document.execCommand('styleWithCSS', false, false);
        }
    } finally {
        state.styling = false;
    }
    const made = normalizeMarks(state);
    if (made.length && hadRange) {
        const range = document.createRange();
        range.setStartBefore(made[0]);
        range.setEndAfter(made[made.length - 1]);
        const selection = document.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
        // A list or color tool takes focus back after this: the next tool restores this range, not the removed <font>.
        saveRange(state);
    }
}

const FONT_SIZES = ['', 'x-small', 'small', 'medium', 'large', 'x-large', 'xx-large', 'xxx-large'];

function normalizeMarks(state) {
    const made = [];
    for (const font of [...state.content.querySelectorAll('font')]) {
        const span = document.createElement('span');
        const face = font.getAttribute('face');
        const color = font.getAttribute('color');
        const size = font.getAttribute('size');
        if (face && face !== NO_FONT) span.style.fontFamily = face;
        if (color && color.toLowerCase() !== NO_COLOR) span.style.color = color;
        if (size === '7') { if (state.pendingSize) span.style.fontSize = state.pendingSize; }
        else if (FONT_SIZES[size]) span.style.fontSize = FONT_SIZES[size];
        span.append(...font.childNodes);
        font.replaceWith(span);
        if (span.getAttribute('style')) made.push(span);
        else made.push(...unwrap(span));
    }
    for (const lit of [...state.content.querySelectorAll('[style*="background"]')]) {
        if (lit.style.backgroundColor === 'transparent') {
            lit.style.removeProperty('background-color');
            tidy(lit);
        }
    }
    return made;
}

function unwrap(element) {
    const children = [...element.childNodes];
    element.replaceWith(...children);
    return children;
}

// A span left with no style is no span.
function tidy(element) {
    if (element.getAttribute('style') === '') element.removeAttribute('style');
    if (element.tagName === 'SPAN' && !element.attributes.length) unwrap(element);
}

function unlink(state) {
    const link = within(state, anchorElement(state), 'a');
    if (link && document.getSelection().isCollapsed) {
        const range = document.createRange();
        range.selectNodeContents(link);
        const selection = document.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
    }
    document.execCommand('unlink');
}

// ── Links, pictures, tables ──────────────────────────────────────────────────

const escape = text => String(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

export function linkInfo(id) {
    const state = editors.get(id);
    const link = state && within(state, anchorElement(state), 'a');
    return {
        href: link?.getAttribute('href') ?? '',
        text: link ? link.textContent : (state?.range?.toString() ?? ''),
        newWindow: link?.target === '_blank',
        editing: !!link
    };
}

export function createLink(id, href, text, newWindow) {
    const state = editors.get(id);
    if (!state) return;
    restore(state);
    record(state);
    const selection = document.getSelection();
    const existing = within(state, anchorElement(state), 'a');
    const target = newWindow ? ' target="_blank" rel="noopener noreferrer"' : '';
    if (existing) {
        existing.setAttribute('href', href);
        if (text && text !== existing.textContent) existing.textContent = text;
        setTarget(existing, newWindow);
    } else if (selection.isCollapsed || (text && text !== selection.toString())) {
        document.execCommand('insertHTML', false, `<a href="${escape(href)}"${target}>${escape(text || href)}</a>`);
    } else {
        document.execCommand('createLink', false, href);
        const range = selection.rangeCount ? selection.getRangeAt(0) : null;
        for (const a of state.content.querySelectorAll('a')) {
            if (a.getAttribute('href') === href && range?.intersectsNode(a)) setTarget(a, newWindow);
        }
    }
    changed(state);
}

function setTarget(link, newWindow) {
    if (newWindow) {
        link.target = '_blank';
        link.rel = 'noopener noreferrer';
    } else {
        link.removeAttribute('target');
        link.removeAttribute('rel');
    }
}

export function insertImage(id, src, alt, width, height) {
    const state = editors.get(id);
    if (!state) return;
    restore(state);
    record(state);
    const size = (width ? ` width="${width}"` : '') + (height ? ` height="${height}"` : '');
    document.execCommand('insertHTML', false, `<img src="${escape(src)}" alt="${escape(alt)}"${size}>`);
    changed(state);
}

export function insertTable(id, rows, columns) {
    const state = editors.get(id);
    if (!state) return;
    restore(state);
    record(state);
    const row = `<tr>${`<td style="${CELL_STYLE}"><br></td>`.repeat(columns)}</tr>`;
    document.execCommand('insertHTML', false, `<table style="${TABLE_STYLE}"><tbody>${row.repeat(rows)}</tbody></table><p><br></p>`);
    saveRange(state);
    const after = within(state, anchorElement(state), BLOCKS);
    const table = after?.previousElementSibling?.tagName === 'TABLE' ? after.previousElementSibling : null;
    if (table) caretIn(state, table.rows[0].cells[0]);
    else saveRange(state);
    changed(state);
}

function currentCell(state) {
    return within(state, anchorElement(state), 'td,th');
}

// Where every cell sits, rowspan and colspan counted: grid[row][column] is the cell covering it.
function tableGrid(table) {
    const grid = [];
    const at = new Map();
    [...table.rows].forEach((row, r) => {
        grid[r] ??= [];
        let c = 0;
        for (const cell of row.cells) {
            while (grid[r][c]) c++;
            at.set(cell, { r, c });
            for (let i = 0; i < cell.rowSpan; i++) {
                for (let j = 0; j < cell.colSpan; j++) (grid[r + i] ??= [])[c + j] = cell;
            }
            c += cell.colSpan;
        }
    });
    const columns = Math.max(0, ...grid.map(row => row.length));
    return { grid, at, columns };
}

function newCell(like) {
    const cell = document.createElement(like?.tagName === 'TH' ? 'th' : 'td');
    const style = like?.getAttribute('style');
    if (style) cell.setAttribute('style', style);
    cell.innerHTML = '<br>';
    return cell;
}

function addRow(state, after) {
    const cell = currentCell(state);
    if (!cell) return;
    const table = cell.closest('table');
    const { grid, at, columns } = tableGrid(table);
    const index = after ? at.get(cell).r + cell.rowSpan : at.get(cell).r;
    const row = document.createElement('tr');
    const grown = new Set();
    for (let c = 0; c < columns; c++) {
        const above = grid[index - 1]?.[c];
        if (above && above === grid[index]?.[c]) {
            if (!grown.has(above)) { above.rowSpan++; grown.add(above); }
            continue;
        }
        row.append(newCell(above ?? cell));
    }
    const rows = table.rows;
    if (index < rows.length) rows[index].before(row);
    else rows[rows.length - 1].after(row);
    caretIn(state, row.cells[0] ?? cell);
}

function addColumn(state, after) {
    const cell = currentCell(state);
    if (!cell) return;
    const table = cell.closest('table');
    const { grid, at } = tableGrid(table);
    const index = after ? at.get(cell).c + cell.colSpan : at.get(cell).c;
    const grown = new Set();
    [...table.rows].forEach((row, r) => {
        const left = grid[r]?.[index - 1];
        const right = grid[r]?.[index];
        if (left && left === right) {
            if (!grown.has(left)) { left.colSpan++; grown.add(left); }
            return;
        }
        const before = [...row.cells].find(x => at.get(x).c >= index) ?? null;
        row.insertBefore(newCell(right ?? left ?? cell), before);
    });
    caretIn(state, cell);
}

function deleteRow(state) {
    const cell = currentCell(state);
    if (!cell) return;
    const table = cell.closest('table');
    const { grid, at, columns } = tableGrid(table);
    const r = at.get(cell).r;
    const row = table.rows[r];
    const next = table.rows[r + 1];
    const seen = new Set();
    for (let c = 0; c < columns; c++) {
        const x = grid[r][c];
        if (!x || seen.has(x)) continue;
        seen.add(x);
        if (x.rowSpan < 2) continue;
        x.rowSpan--;
        // A cell starting in this row and spanning down moves into the next one.
        if (at.get(x).r === r && next) next.insertBefore(x, [...next.cells].find(y => at.get(y).c > c) ?? null);
    }
    row.remove();
    afterTableEdit(state, table, next ?? table.rows[r - 1]);
}

function deleteColumn(state) {
    const cell = currentCell(state);
    if (!cell) return;
    const table = cell.closest('table');
    const { grid, at } = tableGrid(table);
    const c = at.get(cell).c;
    const row = cell.parentElement;
    const seen = new Set();
    for (const line of grid) {
        const x = line?.[c];
        if (!x || seen.has(x)) continue;
        seen.add(x);
        if (x.colSpan > 1) x.colSpan--;
        else x.remove();
    }
    for (const empty of [...table.rows].filter(r => !r.cells.length)) empty.remove();
    afterTableEdit(state, table, row.isConnected ? row : table.rows[0]);
}

function deleteTable(state) {
    const table = currentCell(state)?.closest('table');
    if (table) afterTableEdit(state, table, null, true);
}

// Puts the caret back in the table, or where the table was once it is gone.
function afterTableEdit(state, table, row, remove = false) {
    if (remove || !table.rows.length) {
        const next = table.nextElementSibling ?? table.previousElementSibling;
        table.remove();
        if (!state.content.firstChild) state.content.innerHTML = '<p><br></p>';
        caretIn(state, next?.isConnected ? next : state.content.firstElementChild);
        return;
    }
    const cell = row?.isConnected ? row.cells[0] : table.rows[0].cells[0];
    if (cell) caretIn(state, cell);
}

// The cells the selection touches become one, if together they make a rectangle.
function mergeCells(state) {
    const cell = currentCell(state);
    const selection = document.getSelection();
    if (!cell || !selection.rangeCount) return;
    const range = selection.getRangeAt(0);
    const table = cell.closest('table');
    const { grid, at } = tableGrid(table);
    const chosen = [...table.querySelectorAll('td,th')].filter(x => x.closest('table') === table && range.intersectsNode(x));
    if (chosen.length < 2) return;
    let top = Infinity, left = Infinity, bottom = -1, right = -1;
    for (const x of chosen) {
        const p = at.get(x);
        top = Math.min(top, p.r);
        left = Math.min(left, p.c);
        bottom = Math.max(bottom, p.r + x.rowSpan - 1);
        right = Math.max(right, p.c + x.colSpan - 1);
    }
    const inside = new Set();
    for (let r = top; r <= bottom; r++) {
        for (let c = left; c <= right; c++) {
            const x = grid[r]?.[c];
            if (!x) return;
            const p = at.get(x);
            if (p.r < top || p.c < left || p.r + x.rowSpan - 1 > bottom || p.c + x.colSpan - 1 > right) return;
            inside.add(x);
        }
    }
    const target = grid[top][left];
    for (const x of inside) {
        if (x === target) continue;
        if (x.textContent.trim() || x.querySelector('img,table')) {
            if (target.textContent.trim() || target.querySelector('img,table')) target.append(document.createElement('br'));
            target.append(...x.childNodes);
        }
        x.remove();
    }
    target.rowSpan = bottom - top + 1;
    target.colSpan = right - left + 1;
    // A row merged away entirely: drop it, and the spans that crossed it.
    for (let r = table.rows.length - 1; r >= 0; r--) {
        if (table.rows[r].cells.length) continue;
        const crossing = new Set(tableGrid(table).grid[r] ?? []);
        for (const x of crossing) if (x && x.rowSpan > 1) x.rowSpan--;
        table.rows[r].remove();
    }
    caretIn(state, target);
}

function splitCell(state) {
    const cell = currentCell(state);
    if (!cell || (cell.rowSpan < 2 && cell.colSpan < 2)) return;
    const table = cell.closest('table');
    const { at } = tableGrid(table);
    const { r, c } = at.get(cell);
    const rows = cell.rowSpan, columns = cell.colSpan;
    cell.rowSpan = 1;
    cell.colSpan = 1;
    let last = cell;
    for (let j = 1; j < columns; j++) {
        const added = newCell(cell);
        last.after(added);
        last = added;
    }
    for (let i = 1; i < rows; i++) {
        const row = table.rows[r + i];
        if (!row) break;
        const before = [...row.cells].find(y => at.get(y)?.c > c) ?? null;
        for (let j = 0; j < columns; j++) row.insertBefore(newCell(cell), before);
    }
    caretIn(state, cell);
}

// ── Picture selection: an outline and four corner handles over the picture, kept outside the text ──

const IMAGE_MIN = 24;           // px, the narrowest a picture is dragged to
const IMAGE_ROOM = 30;          // px the handles may reach past the picture before they are clipped

function selectImage(state, image) {
    if (state.image === image) return;
    deselectImage(state);
    const box = document.createElement('div');
    box.className = 'nx-editor-image-box';
    box.setAttribute('aria-hidden', 'true');
    for (const corner of ['nw', 'ne', 'sw', 'se']) {
        const handle = document.createElement('span');
        handle.className = `nx-editor-image-handle nx-editor-image-handle-${corner}`;
        handle.addEventListener('pointerdown', e => beginSizing(state, e, corner));
        box.append(handle);
    }
    state.root.append(box);
    state.image = image;
    state.imageBox = box;
    // The picture moving, resizing or leaving the text, and the editor turning read-only.
    state.imageWatch = new MutationObserver(state.onPlace);
    state.imageWatch.observe(state.content, { attributes: true, childList: true, characterData: true, subtree: true });
    state.imageResize = new ResizeObserver(state.onPlace);
    state.imageResize.observe(state.content);
    state.imageResize.observe(image);
    window.addEventListener('resize', state.onPlace);
    placeImageBox(state);
}

function deselectImage(state) {
    if (!state.image) return;
    state.imageWatch.disconnect();
    state.imageResize.disconnect();
    window.removeEventListener('resize', state.onPlace);
    state.imageBox.remove();
    state.image = state.imageBox = null;
    state.sizing = false;
}

function placeImageBox(state) {
    const { image, imageBox: box, content, root } = state;
    if (!image) return;
    if (!image.isConnected || !content.isContentEditable) return deselectImage(state);
    const r = image.getBoundingClientRect();
    const o = root.getBoundingClientRect();
    const c = content.getBoundingClientRect();
    const left = c.left + content.clientLeft, top = c.top + content.clientTop;
    box.style.left = `${r.left - o.left - root.clientLeft}px`;
    box.style.top = `${r.top - o.top - root.clientTop}px`;
    box.style.width = `${r.width}px`;
    box.style.height = `${r.height}px`;
    // Scrolled partly out of the text: cut there, so nothing draws over the toolbar.
    const cut = hidden => hidden > 0 ? `${hidden}px` : `-${IMAGE_ROOM}px`;
    box.style.clipPath = `inset(${cut(top - r.top)} ${cut(r.right - left - content.clientWidth)} ${cut(r.bottom - top - content.clientHeight)} ${cut(left - r.left)})`;
}

// The selection is the picture itself, so alignment and the like apply to it.
function selectNode(state, node) {
    const range = document.createRange();
    range.selectNode(node);
    const selection = document.getSelection();
    selection.removeAllRanges();
    selection.addRange(range);
    saveRange(state);
}

function caretBeside(state, node) {
    const range = document.createRange();
    range.setStartAfter(node);
    range.collapse(true);
    const selection = document.getSelection();
    selection.removeAllRanges();
    selection.addRange(range);
    saveRange(state);
}

// A block left with nothing in it keeps its line.
function keepLine(block) {
    if (block?.isConnected && !block.childNodes.length) block.append(document.createElement('br'));
}

function removeImage(state, image) {
    record(state);
    const parent = image.parentNode;
    caretBeside(state, image);
    image.remove();
    if (parent !== state.content) keepLine(parent);
    if (!state.content.firstChild) state.content.innerHTML = '<p><br></p>';
    changed(state);
}

// A corner drag keeps the picture's proportions and writes width and height when it ends.
function beginSizing(state, e, corner) {
    const image = state.image;
    if (!image || e.button > 0) return;
    const handle = e.currentTarget;
    const start = image.getBoundingClientRect();
    if (!start.width || !start.height) return;
    const before = { width: image.getAttribute('width'), height: image.getAttribute('height') };
    const ratio = start.width / start.height;
    const signX = corner.includes('e') ? 1 : -1, signY = corner.includes('s') ? 1 : -1;
    const style = getComputedStyle(state.content);
    const most = Math.max(IMAGE_MIN, state.content.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight));
    let recorded = false;
    state.sizing = true;
    handle.setPointerCapture(e.pointerId);

    const move = m => {
        const wide = Math.max(start.width + signX * (m.clientX - e.clientX), (start.height + signY * (m.clientY - e.clientY)) * ratio);
        let width = Math.round(Math.min(most, Math.max(IMAGE_MIN, wide)));
        if (!recorded && width === Math.round(start.width)) return;
        if (!recorded) { record(state); recorded = true; }
        image.setAttribute('width', width);
        image.setAttribute('height', Math.round(width / ratio));
        // Its container may hold it narrower than asked (a table cell): keep what shows.
        const shown = Math.round(image.getBoundingClientRect().width);
        if (shown < width) {
            image.setAttribute('width', shown);
            image.setAttribute('height', Math.round(shown / ratio));
        }
        placeImageBox(state);
    };
    const end = u => {
        handle.removeEventListener('pointermove', move);
        handle.removeEventListener('pointerup', end);
        handle.removeEventListener('pointercancel', end);
        state.sizing = false;
        if (!recorded) return;
        if (u.type === 'pointercancel') {
            for (const name of ['width', 'height']) {
                if (before[name] === null) image.removeAttribute(name);
                else image.setAttribute(name, before[name]);
            }
            state.undo.pop();
        }
        changed(state);
        placeImageBox(state);
    };
    handle.addEventListener('pointermove', move);
    handle.addEventListener('pointerup', end);
    handle.addEventListener('pointercancel', end);
}

function rangeAtPoint(x, y) {
    if (document.caretPositionFromPoint) {
        const position = document.caretPositionFromPoint(x, y);
        if (!position) return null;
        const range = document.createRange();
        range.setStart(position.offsetNode, position.offset);
        return range;
    }
    return document.caretRangeFromPoint?.(x, y) ?? null;
}

// A picture dragged inside the text is moved as the same element: one undo step, nothing for the browser to re-serialize.
function dropImage(state, e) {
    const image = state.dragImage;
    state.dragImage = null;
    if (!image || !image.isConnected || !state.content.isContentEditable) return;
    e.preventDefault();
    const range = rangeAtPoint(e.clientX, e.clientY);
    const at = range?.startContainer;
    if (!at || at === image || !state.content.contains(at)) return;
    record(state);
    const parent = image.parentNode;
    if (at === state.content) (state.content.children[range.startOffset] ?? state.content.lastElementChild ?? state.content).append(image);
    else range.insertNode(image);
    if (parent !== state.content) keepLine(parent);
    state.content.focus({ preventScroll: true });
    selectNode(state, image);
    selectImage(state, image);
    changed(state);
    placeImageBox(state);
}

// ── Table size picker ────────────────────────────────────────────────────────

export function openTablePicker(id, popup, dotNet) {
    const state = editors.get(id);
    const button = state?.root.querySelector('[data-nx-editor-cmd="insertTable"]');
    if (!state || !popup || !button) return;
    state.pickerButton = button;
    openPopup(button, popup, popup, 400, dotNet, 'OnTablePickerDismissed');
    const cells = [...popup.querySelectorAll('[data-r]')];
    const label = popup.querySelector('.nx-editor-tablepicker-label');
    const size = Math.max(...cells.map(x => Number(x.dataset.r)));
    let pick = { r: 1, c: 1 };
    const mark = (r, c) => {
        pick = { r: Math.min(Math.max(r, 1), size), c: Math.min(Math.max(c, 1), size) };
        for (const x of cells) x.toggleAttribute('data-on', Number(x.dataset.r) <= pick.r && Number(x.dataset.c) <= pick.c);
        label.textContent = `${pick.r} × ${pick.c} table`;
    };
    popup.onmouseover = e => {
        const cell = e.target.closest('[data-r]');
        if (cell) mark(Number(cell.dataset.r), Number(cell.dataset.c));
    };
    popup.onclick = e => {
        const cell = e.target.closest('[data-r]');
        if (cell) dotNet.invokeMethodAsync('OnTablePicked', Number(cell.dataset.r), Number(cell.dataset.c));
    };
    popup.onkeydown = e => {
        const moves = { ArrowRight: [0, 1], ArrowLeft: [0, -1], ArrowDown: [1, 0], ArrowUp: [-1, 0] };
        if (moves[e.key]) {
            e.preventDefault();
            mark(pick.r + moves[e.key][0], pick.c + moves[e.key][1]);
        } else if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            dotNet.invokeMethodAsync('OnTablePicked', pick.r, pick.c);
        } else if (e.key === 'Escape' || e.key === 'Tab') {
            e.preventDefault();
            dotNet.invokeMethodAsync('OnTablePickerDismissed');
        }
    };
    mark(1, 1);
    popup.focus({ preventScroll: true });
}

export function closeTablePicker(id) {
    const state = editors.get(id);
    if (state?.pickerButton) closePopup(state.pickerButton);
    if (state) state.pickerButton = null;
}

// ── Keys ─────────────────────────────────────────────────────────────────────

function keyDown(state, e) {
    const ctrl = e.ctrlKey || e.metaKey;
    const key = e.key.toLowerCase();
    if (state.image && !['Control', 'Shift', 'Alt', 'Meta'].includes(e.key)) {
        // Any key lets go of the picture; Delete and Backspace take it out.
        const image = state.image;
        deselectImage(state);
        if (e.key === 'Escape' || e.key === 'Delete' || e.key === 'Backspace') {
            e.preventDefault();
            if (e.key === 'Escape') caretBeside(state, image);
            else removeImage(state, image);
            return;
        }
    }
    if (ctrl && !e.altKey && (key === 'z' || key === 'y')) {
        e.preventDefault();
        if (!state.content.isContentEditable) return;
        key === 'y' || e.shiftKey ? redo(state) : undo(state);
    } else if (ctrl && !e.altKey && !e.shiftKey && key === 'k') {
        e.preventDefault();
        saveRange(state);
        state.dotNet.invokeMethodAsync('OnShortcut', 'createLink');
    } else if (e.key === 'Tab' && !ctrl && !e.altKey) {
        // In a table, Tab moves between cells (a new row after the last); elsewhere it leaves the editor.
        saveRange(state);
        const cell = currentCell(state);
        if (!cell || !state.content.isContentEditable) return;
        const cells = [...cell.closest('table').querySelectorAll('td,th')].filter(x => x.closest('table') === cell.closest('table'));
        const index = cells.indexOf(cell) + (e.shiftKey ? -1 : 1);
        if (index < 0) return;
        e.preventDefault();
        if (index < cells.length) {
            caretIn(state, cells[index]);
        } else {
            caretIn(state, cells[cells.length - 1]);
            record(state);
            addRow(state, true);
            changed(state);
        }
    }
}

// ── Paste ────────────────────────────────────────────────────────────────────

function paste(state, e) {
    // A page's own paste handler (pictures, say) may have taken it already.
    if (e.defaultPrevented || !state.content.isContentEditable) return;
    const html = e.clipboardData?.getData('text/html');
    if (!html) return;   // plain text: the browser's own paste
    e.preventDefault();
    record(state);
    document.execCommand('insertHTML', false, cleanHtml(html));
    normalizeMarks(state);
    changed(state);
}

const DROP = new Set(['SCRIPT', 'STYLE', 'META', 'LINK', 'TITLE', 'HEAD', 'OBJECT', 'EMBED', 'IFRAME', 'FRAME', 'FRAMESET', 'SVG',
    'MATH', 'TEMPLATE', 'NOSCRIPT', 'XML', 'BUTTON', 'INPUT', 'SELECT', 'TEXTAREA', 'FORM', 'CANVAS', 'AUDIO', 'VIDEO', 'BASE']);

const KEEP = {
    P: [], BR: [], B: [], STRONG: [], I: [], EM: [], U: [], S: [], STRIKE: [], DEL: [], INS: [], SUB: [], SUP: [], SPAN: [], DIV: [],
    A: ['href', 'title', 'target'], UL: [], OL: ['start', 'type'], LI: [], H1: [], H2: [], H3: [], H4: [], H5: [], H6: [],
    BLOCKQUOTE: [], PRE: [], CODE: [], HR: [], IMG: ['src', 'alt', 'title', 'width', 'height'],
    TABLE: ['border', 'cellpadding', 'cellspacing', 'width'], THEAD: [], TBODY: [], TFOOT: [], TR: [], CAPTION: [],
    TD: ['colspan', 'rowspan', 'width'], TH: ['colspan', 'rowspan', 'width'], COLGROUP: [], COL: ['span', 'width']
};

const KEEP_STYLES = new Set(['color', 'background-color', 'font-weight', 'font-style', 'text-decoration', 'text-decoration-line',
    'text-align', 'font-size', 'font-family', 'vertical-align', 'margin-left', 'padding-left', 'border', 'border-collapse',
    'padding', 'width', 'height']);

// Pasted HTML keeps its structure and look, not its baggage: no scripts, comments, classes or Office styles; links and
// pictures only over http(s) (pictures also as data:image).
export function cleanHtml(html) {
    const doc = new DOMParser().parseFromString(html, 'text/html');
    convertWordLists(doc);
    cleanChildren(doc.body);
    return doc.body.innerHTML;
}

function cleanChildren(parent) {
    for (const node of [...parent.childNodes]) {
        if (node.nodeType === Node.COMMENT_NODE) { node.remove(); continue; }
        if (node.nodeType !== Node.ELEMENT_NODE) continue;
        const tag = node.tagName.toUpperCase();
        if (DROP.has(tag) || (tag.includes(':') && !node.textContent.trim())) { node.remove(); continue; }
        cleanChildren(node);
        if (tag === 'FONT') { fontToSpan(node); continue; }
        if (!(tag in KEEP)) { node.replaceWith(...node.childNodes); continue; }
        for (const attribute of [...node.attributes]) {
            const name = attribute.name.toLowerCase();
            if (name === 'style') keepStyles(node);
            else if (!KEEP[tag].includes(name)) node.removeAttribute(attribute.name);
        }
        if (tag === 'A' && !safeUrl(node.getAttribute('href'), false)) node.removeAttribute('href');
        if (tag === 'A' && node.getAttribute('target') === '_blank') node.rel = 'noopener noreferrer';
        if (tag === 'IMG' && !safeUrl(node.getAttribute('src'), true)) { node.remove(); continue; }
        if (tag === 'SPAN' && !node.attributes.length) node.replaceWith(...node.childNodes);
    }
}

function fontToSpan(font) {
    const span = font.ownerDocument.createElement('span');
    if (font.getAttribute('face')) span.style.fontFamily = font.getAttribute('face');
    if (font.getAttribute('color')) span.style.color = font.getAttribute('color');
    if (FONT_SIZES[font.getAttribute('size')]) span.style.fontSize = FONT_SIZES[font.getAttribute('size')];
    span.append(...font.childNodes);
    if (span.getAttribute('style')) font.replaceWith(span);
    else font.replaceWith(...span.childNodes);
}

// The browser's CSS parser already dropped what it does not know (mso-*); keep the listed properties without urls.
function keepStyles(element) {
    const kept = [];
    for (const property of [...element.style]) {
        const value = element.style.getPropertyValue(property);
        if (KEEP_STYLES.has(property) && !/url\(|expression/i.test(value)) kept.push(`${property}: ${value}`);
    }
    if (kept.length) element.setAttribute('style', kept.join('; '));
    else element.removeAttribute('style');
}

function safeUrl(url, image) {
    if (!url) return false;
    const value = url.trim().toLowerCase();
    if (image && /^data:image\/(png|jpe?g|gif|webp);/.test(value)) return true;
    if (/^(https?:|\/|\.|#)/.test(value)) return true;
    if (!image && /^(mailto:|tel:)/.test(value)) return true;
    return !/^[a-z][a-z0-9+.-]*:/.test(value);   // relative
}

// Word pastes a list as paragraphs styled "mso-list: l0 level1", the bullet in a span marked mso-list:Ignore.
function convertWordLists(doc) {
    const isItem = p => /mso-list:\s*l\d+\s+level\d+/i.test(p.getAttribute('style') ?? '');
    let stack = [];
    for (const item of [...doc.body.querySelectorAll('p')].filter(isItem)) {
        if (!stack.length || item.previousElementSibling !== stack[0].list) stack = [];
        const level = Number(/level(\d+)/i.exec(item.getAttribute('style'))?.[1] ?? 1);
        const marker = item.querySelector('span[style*="mso-list:Ignore" i], span[style*="mso-list: Ignore" i]');
        const text = marker?.textContent.replace(/\s+/g, '') ?? '';
        const ordered = /^[(\[]?[0-9a-z]{1,4}[.)\]]$/i.test(text);
        marker?.remove();
        while (stack.length && stack[stack.length - 1].level > level) stack.pop();
        if (!stack.length || stack[stack.length - 1].level < level) {
            const list = doc.createElement(ordered ? 'ol' : 'ul');
            if (stack.length) {
                const parent = stack[stack.length - 1].list;
                (parent.lastElementChild ?? parent).append(list);
            } else {
                item.before(list);
            }
            stack.push({ list, level });
        }
        const li = doc.createElement('li');
        li.append(...item.childNodes);
        stack[stack.length - 1].list.append(li);
        item.remove();
    }
}

// ── What the caret is in ─────────────────────────────────────────────────────

function reportSoon(state) {
    cancelAnimationFrame(state.frame);
    state.frame = requestAnimationFrame(() => report(state));
}

function report(state) {
    if (!editors.has(state.content.id)) return;
    const now = JSON.stringify(caretState(state));
    if (now === state.reported) return;
    state.reported = now;
    state.dotNet.invokeMethodAsync('OnStateChanged', JSON.parse(now));
}

function queryState(command) {
    try { return document.queryCommandState(command); } catch { return false; }
}

function toHex(color) {
    const m = /rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?/.exec(color ?? '');
    if (!m || m[4] === '0') return null;
    return '#' + [m[1], m[2], m[3]].map(n => Number(n).toString(16).padStart(2, '0')).join('');
}

// The nearest element inside the text that sets this style itself.
function styledAncestor(state, element, property) {
    for (let e = element; e && e !== state.content && state.content.contains(e); e = e.parentElement) {
        if (e.style?.getPropertyValue(property)) return e;
    }
    return null;
}

function caretState(state) {
    const selection = document.getSelection();
    const focused = selection.rangeCount && state.content.contains(selection.anchorNode);
    const element = anchorElement(state);
    const style = element ? getComputedStyle(element) : null;
    const block = within(state, element, BLOCKS + ',li,td,th,div');
    const align = block ? getComputedStyle(block).textAlign : 'left';
    const colored = styledAncestor(state, element, 'color');
    const lit = styledAncestor(state, element, 'background-color');
    const list = within(state, element, 'ul,ol');
    return {
        bold: !!focused && queryState('bold'),
        italic: !!focused && queryState('italic'),
        underline: !!focused && queryState('underline'),
        strikethrough: !!focused && queryState('strikeThrough'),
        unorderedList: list?.tagName === 'UL',
        orderedList: list?.tagName === 'OL',
        align,
        format: within(state, element, BLOCKS)?.tagName.toLowerCase() ?? 'p',
        fontSize: style ? Math.round(parseFloat(style.fontSize) * 0.75 * 2) / 2 : 0,
        fontFamily: style ? style.fontFamily.split(',')[0].replace(/["']/g, '').trim() : '',
        foreColor: colored ? toHex(colored.style.color) : null,
        backColor: lit ? toHex(lit.style.backgroundColor) : null,
        inLink: !!within(state, element, 'a'),
        inTable: !!within(state, element, 'td,th'),
        canUndo: state.undo.length > 0,
        canRedo: state.redo.length > 0
    };
}
