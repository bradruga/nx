// Interop for NxScheduler. Scrolling needs the browser: the hours open scrolled to the start of the work day.

export function scrollToRow(body, row) {
    const first = body?.querySelector('.nx-scheduler-slot');
    if (!first) return;
    body.scrollTop = Math.round(row * first.getBoundingClientRect().height);
}
