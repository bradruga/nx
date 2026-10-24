// Interop for NxUpload. The browser holds the File objects and posts them: .NET only ever sees their names and
// sizes. Here: picking (the file input lies over the drop zone, so clicks and drops both land in it) and Ctrl+V
// pastes, the upload queue with its concurrency limit, an XHR per file with throttled progress, cancel, and
// reading files from the clipboard for a Paste button.

const states = new WeakMap();
let nextId = 0;

export function init(input, zone, dotNet) {
    if (!input || states.has(input)) return;
    const state = { input, zone, dotNet, files: new Map(), xhrs: new Map(), queue: [], active: 0 };
    state.add = list => {
        const infos = [];
        for (const file of list) {
            const id = `f${++nextId}`;
            state.files.set(id, file);
            infos.push({ id, name: file.name, size: file.size });
        }
        if (infos.length) dotNet.invokeMethodAsync('OnFilesAdded', infos);
    };
    state.onChange = () => { state.add([...input.files]); input.value = ''; };
    state.onPaste = e => {
        const files = [...(e.clipboardData?.files ?? [])];
        if (files.length === 0) return;
        e.preventDefault();
        state.add(files);
    };
    input.addEventListener('change', state.onChange);
    zone?.addEventListener('paste', state.onPaste);
    states.set(input, state);
}

export function upload(input, ids, url, field, withCredentials, concurrency) {
    const state = states.get(input);
    if (!state) return;
    Object.assign(state, { url, field, withCredentials, concurrency });
    for (const id of ids) if (!state.queue.includes(id) && !state.xhrs.has(id)) state.queue.push(id);
    pump(state);
}

function pump(state) {
    while (state.queue.length && (state.concurrency <= 0 || state.active < state.concurrency)) start(state, state.queue.shift());
}

function start(state, id) {
    const file = state.files.get(id);
    if (!file) return;
    const xhr = new XMLHttpRequest();
    state.xhrs.set(id, xhr);
    state.active++;
    let last = 0;
    xhr.upload.onprogress = e => {
        if (!e.lengthComputable) return;
        const percent = Math.floor(e.loaded * 100 / e.total);
        const now = performance.now();
        if (percent < 100 && now - last < 150) return;
        last = now;
        state.dotNet.invokeMethodAsync('OnUploadProgress', id, percent);
    };
    const finish = (status, text) => {
        if (!state.xhrs.delete(id)) return;
        state.active--;
        state.dotNet.invokeMethodAsync('OnUploadDone', id, status, text ?? '');
        pump(state);
    };
    xhr.onload = () => finish(xhr.status, xhr.responseText);
    xhr.onerror = () => finish(0, '');
    xhr.open('POST', state.url);
    xhr.withCredentials = !!state.withCredentials;
    const form = new FormData();
    form.append(state.field, file, file.name);
    xhr.send(form);
}

export function cancel(input, id) {
    const state = states.get(input);
    if (!state) return;
    state.queue = state.queue.filter(q => q !== id);
    const xhr = state.xhrs.get(id);
    if (xhr) {
        state.xhrs.delete(id);
        state.active--;
        xhr.abort();
        pump(state);
    }
}

export function forget(input, id) {
    cancel(input, id);
    states.get(input)?.files.delete(id);
}

// Files on the clipboard (a screenshot, a copied file) for a Paste button; false when there are none or the
// browser will not tell.
export async function paste(input) {
    const state = states.get(input);
    if (!state || !navigator.clipboard?.read) return false;
    try {
        const files = [];
        for (const item of await navigator.clipboard.read()) {
            const type = item.types.find(t => !t.startsWith('text/'));
            if (!type) continue;
            const blob = await item.getType(type);
            const extension = type.split('/')[1]?.split('+')[0] ?? 'bin';
            files.push(new File([blob], `pasted-${files.length + 1}.${extension}`, { type }));
        }
        state.add(files);
        return files.length > 0;
    } catch {
        return false;
    }
}

export function dispose(input) {
    const state = states.get(input);
    if (!state) return;
    for (const xhr of state.xhrs.values()) xhr.abort();
    input.removeEventListener('change', state.onChange);
    state.zone?.removeEventListener('paste', state.onPaste);
    states.delete(input);
}
