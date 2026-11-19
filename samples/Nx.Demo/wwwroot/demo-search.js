// Site search: Ctrl+K / Cmd+K, or "/" outside a text field, opens it from anywhere. Also localStorage helpers that
// never throw (private windows, blocked site data), used by SiteSearchState for Recent and Pinned.
(() => {
    window.nxDemoStore = {
        get: key => { try { return localStorage.getItem(key); } catch { return null; } },
        set: (key, value) => { try { localStorage.setItem(key, value); } catch { /* not saved */ } },
    };

    const typing = el => el instanceof HTMLElement &&
        (el.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName));

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
