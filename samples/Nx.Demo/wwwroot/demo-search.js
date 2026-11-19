// Site search: Ctrl+K / Cmd+K, or "/" outside a text field, opens it from anywhere. Also localStorage helpers that
// never throw (private windows, blocked site data), used by SiteSearchState for Recent and Pinned.
(() => {
    window.nxDemoStore = {
        get: key => { try { return localStorage.getItem(key); } catch { return null; } },
        set: (key, value) => { try { localStorage.setItem(key, value); } catch { /* not saved */ } },
    };

    const typing = el => el instanceof HTMLElement &&
        (el.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName));

    // Opening takes a server round trip; letters typed meanwhile are held and put into the field once it has focus.
    // Only keys landing where focus was when it opened (or on the page) are held, never keys meant for another field.
    let held = null, heldFrom = null, heldTimer = 0;
    const startHolding = from => {
        held = '';
        heldFrom = from;
        clearTimeout(heldTimer);
        heldTimer = setTimeout(() => held = null, 3000);
    };
    document.addEventListener('click', e => {
        const button = e.target instanceof Element && e.target.closest('#site-search-button');
        if (button) startHolding(document.activeElement === document.body ? button : document.activeElement);
    }, true);
    document.addEventListener('keydown', e => {
        if (held === null || e.key.length !== 1 || e.ctrlKey || e.metaKey || e.altKey) return;
        if (e.target !== document.body && e.target !== heldFrom) return;
        e.preventDefault();
        e.stopImmediatePropagation();
        held += e.key;
    }, true);
    document.addEventListener('focusin', e => {
        if (held === null || !(e.target instanceof HTMLInputElement) || e.target.id !== 'site-search-input') return;
        const text = held;
        held = null;
        clearTimeout(heldTimer);
        if (!text) return;
        e.target.value += text;
        e.target.dispatchEvent(new Event('input', { bubbles: true }));
    }, true);

    document.addEventListener('keydown', e => {
        // A control that used the key (NxEditor's Ctrl+K inserts a link) keeps it.
        if (e.defaultPrevented || e.repeat || e.altKey) return;
        const key = e.key.toLowerCase();
        const shortcut = ((e.ctrlKey || e.metaKey) && !e.shiftKey && key === 'k') ||
            (key === '/' && !e.ctrlKey && !e.metaKey && !typing(e.target));
        const button = shortcut && document.getElementById('site-search-button');
        if (!button) return;
        e.preventDefault();
        button.click();
    });
})();
