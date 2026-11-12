// Interop for NxChat. Scrolling needs the browser: the list follows new messages while the user is at the bottom, and
// stays put while they scroll up to read. Enter sends (Shift+Enter is a new line), which needs a per-key preventDefault.

const states = new WeakMap();      // root -> state
const byList = new WeakMap();      // list -> state
const NEAR = 48;    // px from the bottom that still counts as "at the bottom"

export function init(root, list) {
    if (!root || !list || states.has(root)) return;
    const state = { list, stick: true };
    const bottom = () => { list.scrollTop = list.scrollHeight; };
    state.onScroll = () => { state.stick = list.scrollHeight - list.scrollTop - list.clientHeight <= NEAR; };
    // New or longer messages, and pictures that load later, change the height: follow them when at the bottom.
    state.follow = () => { if (state.stick) bottom(); };
    state.mutations = new MutationObserver(state.follow);
    state.mutations.observe(list, { childList: true, subtree: true, characterData: true });
    state.resize = new ResizeObserver(state.follow);
    state.resize.observe(list);
    state.onLoad = e => { if (e.target.tagName === 'IMG') state.follow(); };
    state.onKeyDown = e => {
        if (e.key !== 'Enter' || e.shiftKey || e.isComposing || e.target.tagName !== 'TEXTAREA') return;
        e.preventDefault();
        state.stick = true;
        e.target.closest('form')?.requestSubmit();
    };
    list.addEventListener('scroll', state.onScroll, { passive: true });
    list.addEventListener('load', state.onLoad, true);
    root.addEventListener('keydown', state.onKeyDown);
    states.set(root, state);
    byList.set(list, state);
    bottom();
}

// After the user sent a message: back to the newest, and following again.
export function scrollToBottom(list) {
    if (!list) return;
    const state = byList.get(list);
    if (state) state.stick = true;
    list.scrollTop = list.scrollHeight;
}

export function dispose(root) {
    const state = root && states.get(root);
    if (!state) return;
    state.mutations.disconnect();
    state.resize.disconnect();
    state.list.removeEventListener('scroll', state.onScroll);
    state.list.removeEventListener('load', state.onLoad, true);
    root.removeEventListener('keydown', state.onKeyDown);
    byList.delete(state.list);
    states.delete(root);
}
