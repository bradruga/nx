// Interop for NxPdfViewer. pdf.js (vendored in ./pdfjs, Apache-2.0) is loaded on first use: the library, its worker
// and the viewer components (PDFViewer, PDFFindController, PDFLinkService). This module opens a document from bytes or
// an address, and reports the page count, the page in view, the zoom and find matches to .NET. State is keyed by id.

const BASE = new URL('./pdfjs/', import.meta.url);
const viewers = new Map();
let loading = null;

function loadPdfJs() {
    loading ??= (async () => {
        const pdfjsLib = await import(new URL('pdf.min.mjs', BASE).href);
        pdfjsLib.GlobalWorkerOptions.workerSrc = new URL('pdf.worker.min.mjs', BASE).href;
        // pdf_viewer.mjs reads the library from this global.
        globalThis.pdfjsLib = pdfjsLib;
        const components = await import(new URL('pdf_viewer.mjs', BASE).href);
        if (!document.querySelector('link[data-nx-pdfjs]')) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = new URL('pdf_viewer.css', BASE).href;
            link.dataset.nxPdfjs = '';
            document.head.append(link);
        }
        return { pdfjsLib, components };
    })();
    return loading;
}

export async function init(id, container, dotNet) {
    if (!container || viewers.has(id)) return;
    const state = { container, dotNet, document: null, task: null, zoom: 'auto', reported: '' };
    viewers.set(id, state);
    const { components } = await loadPdfJs();
    const eventBus = new components.EventBus();
    const linkService = new components.PDFLinkService({ eventBus });
    const findController = new components.PDFFindController({ eventBus, linkService });
    const viewer = new components.PDFViewer({ container, eventBus, linkService, findController });
    linkService.setViewer(viewer);
    Object.assign(state, { eventBus, linkService, findController, viewer });

    eventBus.on('pagesinit', () => {
        viewer.currentScaleValue = state.zoom;
        report(state);
    });
    eventBus.on('pagechanging', () => report(state));
    eventBus.on('scalechanging', () => report(state));
    const matches = e => {
        const count = e.matchesCount ?? { current: 0, total: 0 };
        dotNet.invokeMethodAsync('OnMatches', count.current ?? 0, count.total ?? 0);
    };
    eventBus.on('updatefindmatchescount', matches);
    eventBus.on('updatefindcontrolstate', matches);
    // The viewer keeps the zoom it had ("auto" refits) when its container resizes.
    state.resize = new ResizeObserver(() => {
        if (state.document && typeof viewer.currentScaleValue === 'string' && isNaN(Number(viewer.currentScaleValue)))
            viewer.currentScaleValue = viewer.currentScaleValue;
    });
    state.resize.observe(container);
}

function report(state) {
    const { viewer } = state;
    const value = String(viewer.currentScaleValue ?? '');
    const preset = isNaN(Number(value)) ? value : null;
    const now = `${viewer.currentPageNumber}|${viewer.currentScale}|${preset}`;
    if (now === state.reported) return;
    state.reported = now;
    state.dotNet.invokeMethodAsync('OnViewChanged', viewer.currentPageNumber, viewer.currentScale, preset);
}

export async function openData(id, stream, zoom) {
    const bytes = new Uint8Array(await stream.arrayBuffer());
    await open(id, { data: bytes }, zoom);
}

// Relative to the app's <base href>, as a link would be (pdf.js alone resolves against the page's path).
export async function openUrl(id, url, zoom) {
    await open(id, { url: new URL(url, document.baseURI).href }, zoom);
}

async function open(id, source, zoom) {
    const state = viewers.get(id);
    if (!state) return;
    while (!state.viewer) await new Promise(r => setTimeout(r, 20));
    const { pdfjsLib } = await loadPdfJs();
    await unload(state);
    state.zoom = zoom || 'auto';
    state.reported = '';
    const task = pdfjsLib.getDocument({
        ...source,
        cMapUrl: new URL('cmaps/', BASE).href,
        cMapPacked: true,
        standardFontDataUrl: new URL('standard_fonts/', BASE).href,
        wasmUrl: new URL('wasm/', BASE).href,
        iccUrl: new URL('iccs/', BASE).href
    });
    state.task = task;
    try {
        const document = await task.promise;
        if (state.task !== task) { document.destroy(); return; }
        state.document = document;
        state.viewer.setDocument(document);
        state.linkService.setDocument(document, null);
        state.dotNet.invokeMethodAsync('OnLoaded', document.numPages);
    } catch (e) {
        if (state.task !== task) return;
        state.dotNet.invokeMethodAsync('OnFailed', e?.message ?? String(e));
    }
}

async function unload(state) {
    const { task, document } = state;
    state.task = null;
    state.document = null;
    state.viewer?.setDocument(null);
    state.linkService?.setDocument(null, null);
    try { await (document ?? task)?.destroy(); } catch { /* already gone */ }
}

export async function close(id) {
    const state = viewers.get(id);
    if (state) await unload(state);
}

export function setPage(id, page) {
    const state = viewers.get(id);
    if (state?.document) state.viewer.currentPageNumber = page;
}

export function setZoom(id, value) {
    const state = viewers.get(id);
    if (state?.document) state.viewer.currentScaleValue = value;
}

export function zoomStep(id, zoomIn) {
    const state = viewers.get(id);
    if (!state?.document) return;
    zoomIn ? state.viewer.increaseScale() : state.viewer.decreaseScale();
}

export function find(id, query, again, previous) {
    const state = viewers.get(id);
    if (!state?.document) return;
    state.eventBus.dispatch('find', {
        source: null, type: again ? 'again' : '', query, caseSensitive: false, entireWord: false,
        highlightAll: true, findPrevious: previous, matchDiacritics: false
    });
}

async function blobUrl(state) {
    const data = await state.document.getData();
    return URL.createObjectURL(new Blob([data], { type: 'application/pdf' }));
}

export async function download(id, fileName) {
    const state = viewers.get(id);
    if (!state?.document) return;
    const url = await blobUrl(state);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.append(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 30000);
}

// The browser prints the file itself: a hidden frame shows it, and its print dialog opens.
export async function print(id) {
    const state = viewers.get(id);
    if (!state?.document) return;
    const url = await blobUrl(state);
    const frame = document.createElement('iframe');
    frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;';
    frame.src = url;
    frame.onload = () => {
        try { frame.contentWindow.focus(); frame.contentWindow.print(); }
        catch { window.open(url, '_blank'); }
        setTimeout(() => { frame.remove(); URL.revokeObjectURL(url); }, 60000);
    };
    document.body.append(frame);
}

export async function dispose(id) {
    const state = viewers.get(id);
    if (!state) return;
    viewers.delete(id);
    state.resize?.disconnect();
    await unload(state);
}
