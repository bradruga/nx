// Interop for NxSignature. Drawing needs the browser: pointer events on a canvas, strokes kept as points so the pad
// can be redrawn at any size (a resize, the saved image at ExportScale). A signature set from .NET is drawn as an image
// under any new strokes. When a stroke ends the PNG goes to .NET.

const states = new WeakMap();

export function init(root, canvas, dotNet, options, value) {
    if (!root || !canvas || states.has(root)) return;
    const state = { root, canvas, dotNet, options, strokes: [], current: null, image: null };
    states.set(root, state);
    state.resize = new ResizeObserver(() => fit(state));
    state.resize.observe(root);
    state.onDown = e => down(state, e);
    state.onMove = e => move(state, e);
    state.onUp = e => up(state, e);
    canvas.addEventListener('pointerdown', state.onDown);
    canvas.addEventListener('pointermove', state.onMove);
    canvas.addEventListener('pointerup', state.onUp);
    canvas.addEventListener('pointercancel', state.onUp);
    fit(state);
    setValue(root, value);
}

export function setOptions(root, options) {
    const state = states.get(root);
    if (!state) return;
    state.options = options;
    render(state);
}

// A signature from .NET (or none): it replaces whatever is on the pad.
export function setValue(root, value) {
    const state = states.get(root);
    if (!state) return;
    state.strokes = [];
    state.image = null;
    if (!value) { render(state); return; }
    const image = new Image();
    image.onload = () => { state.image = image; render(state); };
    image.src = value;
}

export function clear(root) {
    setValue(root, null);
}

export function dispose(root) {
    const state = root && states.get(root);
    if (!state) return;
    state.resize.disconnect();
    const { canvas } = state;
    canvas.removeEventListener('pointerdown', state.onDown);
    canvas.removeEventListener('pointermove', state.onMove);
    canvas.removeEventListener('pointerup', state.onUp);
    canvas.removeEventListener('pointercancel', state.onUp);
    states.delete(root);
}

// The canvas has the pad's pixel size times the device's ratio, so strokes stay sharp.
function fit(state) {
    const { canvas } = state;
    const width = canvas.clientWidth, height = canvas.clientHeight;
    const ratio = window.devicePixelRatio || 1;
    if (!width || !height) return;
    canvas.width = Math.round(width * ratio);
    canvas.height = Math.round(height * ratio);
    render(state);
}

const locked = state => state.root.dataset.locked === 'true';

function point(state, e) {
    const rect = state.canvas.getBoundingClientRect();
    return { x: (e.clientX - rect.left) / rect.width, y: (e.clientY - rect.top) / rect.height };
}

function down(state, e) {
    if (locked(state) || (e.pointerType === 'mouse' && e.button !== 0)) return;
    e.preventDefault();
    state.canvas.setPointerCapture(e.pointerId);
    state.current = [point(state, e)];
    state.strokes.push(state.current);
    render(state);
}

function move(state, e) {
    if (!state.current) return;
    const events = e.getCoalescedEvents?.() ?? [e];
    for (const each of events) state.current.push(point(state, each));
    render(state);
}

function up(state, e) {
    if (!state.current) return;
    state.current = null;
    if (state.canvas.hasPointerCapture(e.pointerId)) state.canvas.releasePointerCapture(e.pointerId);
    state.dotNet.invokeMethodAsync('OnSigned', exportPng(state));
}

// Points are stored as fractions of the pad, so the same strokes draw at any size.
function draw(state, context, width, height, scale) {
    const { options } = state;
    context.fillStyle = options.background;
    context.fillRect(0, 0, width, height);
    if (state.image) context.drawImage(state.image, 0, 0, width, height);
    context.strokeStyle = options.color;
    context.fillStyle = options.color;
    context.lineWidth = options.strokeWidth * scale;
    context.lineCap = 'round';
    context.lineJoin = 'round';
    for (const stroke of state.strokes) {
        const points = stroke.map(p => ({ x: p.x * width, y: p.y * height }));
        if (points.length === 1) {
            context.beginPath();
            context.arc(points[0].x, points[0].y, context.lineWidth / 2, 0, Math.PI * 2);
            context.fill();
            continue;
        }
        context.beginPath();
        context.moveTo(points[0].x, points[0].y);
        // Through the midpoints, with each point as the control: a smooth line without corners.
        for (let i = 1; i < points.length - 1; i++) {
            const mid = { x: (points[i].x + points[i + 1].x) / 2, y: (points[i].y + points[i + 1].y) / 2 };
            context.quadraticCurveTo(points[i].x, points[i].y, mid.x, mid.y);
        }
        const last = points[points.length - 1];
        context.lineTo(last.x, last.y);
        context.stroke();
    }
}

function render(state) {
    const { canvas } = state;
    if (!canvas.width || !canvas.height) return;
    draw(state, canvas.getContext('2d'), canvas.width, canvas.height, canvas.width / canvas.clientWidth);
}

function exportPng(state) {
    const scale = state.options.exportScale || 1;
    const out = document.createElement('canvas');
    out.width = Math.round(state.canvas.clientWidth * scale);
    out.height = Math.round(state.canvas.clientHeight * scale);
    draw(state, out.getContext('2d'), out.width, out.height, scale);
    return out.toDataURL('image/png');
}
