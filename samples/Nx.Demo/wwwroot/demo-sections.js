// Demo site sections: heading links, the "On this page" list / jump control, and the Phone / Tablet / Desktop
// frames of Examples pages. Loaded as a classic script in <head> so frame mode is set before the first paint.
// Blazor owns the page markup and replaces it when the circuit starts, so everything here is re-applied from a
// MutationObserver; nothing it adds carries text inside a heading, so h2.textContent stays the heading's text.
(() => {
    'use strict';

    const html = document.documentElement;
    const params = new URLSearchParams(location.search);
    const frameId = params.get('frame');
    const inFrame = frameId !== null;

    // ── Frame mode: the bare page an example's iframe loads (/{slug}/examples?frame={sectionId}) ──
    if (inFrame) {
        html.classList.add('ds-frame');
        try {
            const parentHtml = window.parent !== window ? window.parent.document.documentElement : null;
            if (parentHtml) {
                const follow = () => html.classList.toggle('nx-theme-dark', parentHtml.classList.contains('nx-theme-dark'));
                follow();
                new MutationObserver(follow).observe(parentHtml, { attributes: true, attributeFilter: ['class'] });
            }
        } catch { /* opened on its own, or cross-origin: keep the saved theme */ }
    }

    // The id rule shared with the site search: keep in step with it.
    const slugify = text => text.toLowerCase().replace(/&/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');

    const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const examplesPath = () => /^\/[^/]+\/examples\/?$/.test(location.pathname);

    const svg = (paths, size = 16) =>
        `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" ` +
        `stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">${paths}</svg>`;
    const icons = {
        link: svg('<path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/>', 14),
        phone: svg('<rect x="6" y="2" width="12" height="20" rx="2"/><path d="M11 18h2"/>'),
        tablet: svg('<rect x="4" y="2" width="16" height="20" rx="2"/><path d="M11 18h2"/>'),
        desktop: svg('<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/>'),
        external: svg('<path d="M15 3h6v6M10 14 21 3M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/>'),
        close: svg('<path d="M18 6 6 18M6 6l12 12"/>'),
    };

    const devices = [
        { key: 'phone', label: 'Phone', width: 390, height: 844 },
        { key: 'tablet', label: 'Tablet', width: 768, height: 1024 },
        { key: 'desktop', label: 'Desktop', width: 1280, height: 800 },
    ];

    // ── Sections ──────────────────────────────────────────────────────────────

    let sections = [];   // { section, heading, id, title }

    function readSections() {
        const used = new Map();
        return [...document.querySelectorAll('.main-content .doc-section > h2')].map(heading => {
            const title = heading.textContent.trim();
            let id = slugify(title) || 'section';
            const n = (used.get(id) || 0) + 1;
            used.set(id, n);
            if (n > 1) id += '-' + n;
            // Not an id attribute: examples give components the same ids (#search-box, #hover-card, which renders
            // only while open), and tests and labels find them by id. Hashes reach the heading through sectionById.
            if (heading.dataset.sectionId !== id) heading.dataset.sectionId = id;
            return { section: heading.parentElement, heading, id, title };
        });
    }

    const sectionById = id => sections.find(s => s.id === id)?.heading ?? null;

    function sectionUrl(id) {
        return location.origin + location.pathname + location.search + '#' + id;
    }

    function frameUrl(id) {
        return location.pathname.replace(/\/$/, '') + '?frame=' + encodeURIComponent(id);
    }

    // The tools float right just before the heading, outside it, so they stay out of the heading's accessible name.
    // They are out of the tab order (the examples' keyboard demos tab from one section into the next); the list and
    // jump control are the keyboard way to a section.
    function decorateHeading(s, framed) {
        const h = s.heading;
        h.classList.add('ds-h2');
        let tools = h.previousElementSibling?.classList.contains('ds-h2-tools') ? h.previousElementSibling : null;
        if (tools && tools.dataset.id === s.id && tools.dataset.framed === String(framed) && tools.dataset.path === location.pathname)
            return;
        tools?.remove();
        tools = document.createElement('div');
        tools.className = 'ds-h2-tools';
        tools.dataset.id = s.id;
        tools.dataset.framed = String(framed);
        tools.dataset.path = location.pathname;

        const anchor = document.createElement('a');
        anchor.className = 'ds-anchor';
        anchor.href = location.pathname + '#' + s.id;
        anchor.setAttribute('aria-label', `Copy link to ${s.title}`);
        anchor.title = 'Copy link to this section';
        anchor.innerHTML = icons.link;
        anchor.tabIndex = -1;
        tools.append(anchor);

        if (framed) {
            const group = document.createElement('span');
            group.className = 'ds-frame-links';
            group.setAttribute('role', 'group');
            group.setAttribute('aria-label', `Preview ${s.title}`);
            for (const d of devices) {
                const a = document.createElement('a');
                a.className = 'ds-frame-link';
                a.href = frameUrl(s.id);
                a.dataset.device = d.key;
                a.tabIndex = -1;
                a.setAttribute('aria-label', `${s.title} at ${d.label.toLowerCase()} width (${d.width} px)`);
                a.title = `${d.label} · ${d.width} px`;
                a.innerHTML = icons[d.key];
                group.append(a);
            }
            tools.append(group);
        }
        h.before(tools);
    }

    // ── On this page: the list beside the content, the jump control under the header ──

    const wide = window.matchMedia('(min-width: 600px)');
    let toc = null;      // <aside> appended to <body>, outside Blazor's markup
    let jump = null;     // <div> placed after .doc-tabs
    let activeId = null;

    function ensureToc() {
        if (!toc) {
            toc = document.createElement('aside');
            toc.className = 'ds-toc';
            toc.setAttribute('aria-label', 'On this page');
            toc.innerHTML = '<p class="ds-toc-title">On this page</p><ul class="ds-toc-list"></ul>';
            document.body.append(toc);
        }
        if (!toc.isConnected)
            document.body.append(toc);
        return toc;
    }

    function ensureJump() {
        if (!jump) {
            jump = document.createElement('div');
            jump.className = 'ds-jump';
            jump.innerHTML = '<label class="ds-jump-label" for="ds-jump-select">On this page</label>' +
                '<span class="ds-jump-box"><select id="ds-jump-select" class="ds-jump-select" aria-label="Jump to section"></select>' +
                '<svg class="ds-jump-chevron" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" ' +
                'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg></span>';
            jump.querySelector('select').addEventListener('change', e => goTo(e.target.value, true));
        }
        return jump;
    }

    function renderLists() {
        const key = location.pathname + sections.map(s => s.id + '\u0001' + s.title).join('\u0002');
        const show = sections.length >= 3;
        html.classList.toggle('ds-has-toc', show);

        if (!show) {
            toc?.remove();
            jump?.remove();
            return;
        }

        const aside = ensureToc();
        if (aside.dataset.key !== key) {
            aside.dataset.key = key;
            const list = aside.querySelector('ul');
            list.replaceChildren(...sections.map(s => {
                const li = document.createElement('li');
                const a = document.createElement('a');
                a.className = 'ds-toc-link';
                a.href = location.pathname + '#' + s.id;
                a.dataset.id = s.id;
                a.textContent = s.title;
                li.append(a);
                return li;
            }));
        }

        // In the tab row where it fits (no layout shift), on its own row under the tabs on a phone.
        const box = ensureJump();
        const tabs = document.querySelector('.main-content .doc-tabs');
        const inRow = tabs && wide.matches;
        box.classList.toggle('ds-jump-inline', !!inRow);
        if (inRow) {
            if (box.parentElement !== tabs || box.nextSibling) tabs.append(box);
        } else if (tabs) {
            if (box.previousElementSibling !== tabs) tabs.after(box);
        } else if (box.nextElementSibling !== sections[0].section) {
            sections[0].section.before(box);
        }
        const select = box.querySelector('select');
        if (select.dataset.key !== key) {
            select.dataset.key = key;
            select.replaceChildren(...sections.map(s => new Option(s.title, s.id)));
        }
        activeId = null;
        updateActive();
    }

    function updateActive() {
        if (sections.length < 3)
            return;
        const offset = 96;
        let current = sections[0];
        for (const s of sections) {
            if (s.section.getBoundingClientRect().top - offset <= 0) current = s;
            else break;
        }
        // At the bottom of the page the last sections cannot reach the top; mark the last one.
        if (window.innerHeight + window.scrollY >= html.scrollHeight - 2)
            current = sections[sections.length - 1];
        if (current.id === activeId)
            return;
        activeId = current.id;
        if (toc) {
            for (const a of toc.querySelectorAll('.ds-toc-link')) {
                const on = a.dataset.id === activeId;
                a.classList.toggle('ds-toc-active', on);
                if (on) a.setAttribute('aria-current', 'location'); else a.removeAttribute('aria-current');
            }
            const link = toc.querySelector('.ds-toc-active');
            if (link && toc.scrollHeight > toc.clientHeight) {
                const top = link.offsetTop, bottom = top + link.offsetHeight;
                if (top < toc.scrollTop + 40) toc.scrollTop = top - 40;
                else if (bottom > toc.scrollTop + toc.clientHeight - 40) toc.scrollTop = bottom - toc.clientHeight + 40;
            }
        }
        const select = jump?.querySelector('select');
        if (select) select.value = activeId;
    }

    let spyQueued = false;
    function queueSpy() {
        if (spyQueued) return;
        spyQueued = true;
        requestAnimationFrame(() => { spyQueued = false; updateActive(); });
    }

    // ── Scrolling to a section ────────────────────────────────────────────────

    let handledUrl = null;

    function goTo(id, smooth) {
        const target = sectionById(id);
        if (!target) return false;
        target.scrollIntoView({ behavior: smooth && !reducedMotion() ? 'smooth' : 'auto', block: 'start' });
        if (location.hash !== '#' + id) {
            history.replaceState(history.state, '', location.pathname + location.search + '#' + id);
        }
        handledUrl = location.href;
        return true;
    }

    // Scrolls to the hash once the page is interactive (the prerendered markup is replaced when the circuit starts),
    // after Blazor's FocusOnNavigate has focused the h1.
    function scrollToHash() {
        if (handledUrl === location.href) return;
        if (!location.hash) { handledUrl = location.href; return; }
        if (!document.querySelector('.layout-container[data-interactive=true]')) return;
        const id = decodeURIComponent(location.hash.slice(1));
        const find = () => sectionById(id) ?? document.getElementById(id);
        if (!find()) return;
        handledUrl = location.href;
        const run = () => find()?.scrollIntoView({ block: 'start' });
        run();
        setTimeout(run, 120);
    }

    // ── Copying a section link ────────────────────────────────────────────────

    let toast = null, toastTimer = 0;

    async function copyText(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            const area = document.createElement('textarea');
            area.value = text;
            area.setAttribute('readonly', '');
            area.style.cssText = 'position:fixed;top:0;left:0;opacity:0';
            document.body.append(area);
            area.select();
            let ok = false;
            try { ok = document.execCommand('copy'); } catch { }
            area.remove();
            return ok;
        }
    }

    function showToast(anchor, text) {
        if (!toast) {
            toast = document.createElement('div');
            toast.className = 'ds-toast';
            toast.setAttribute('role', 'status');
            toast.setAttribute('aria-live', 'polite');
            document.body.append(toast);
        }
        if (!toast.isConnected) document.body.append(toast);
        const r = anchor.getBoundingClientRect();
        toast.textContent = text;
        toast.style.top = Math.max(8, r.top - 34) + 'px';
        toast.style.left = Math.min(window.innerWidth - 120, Math.max(8, r.left + r.width / 2 - 50)) + 'px';
        toast.classList.add('ds-toast-on');
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => toast.classList.remove('ds-toast-on'), 1600);
    }

    // ── Frames: one example at phone, tablet or desktop width ────────────────

    let dialog = null, opener = null, current = null;   // current = { id, title, device }

    function ensureDialog() {
        if (dialog) return dialog;
        dialog = document.createElement('dialog');
        dialog.className = 'ds-frame-dialog';
        dialog.setAttribute('aria-labelledby', 'ds-frame-title');
        dialog.innerHTML =
            '<div class="ds-frame-head">' +
            '  <div class="ds-frame-heading"><h2 id="ds-frame-title" class="ds-frame-title"></h2><span class="ds-frame-size"></span></div>' +
            '  <div class="ds-frame-devices" role="group" aria-label="Width">' +
            devices.map(d => `<button type="button" class="ds-frame-device" data-device="${d.key}" aria-pressed="false">${icons[d.key]}<span>${d.label}</span></button>`).join('') +
            '  </div>' +
            `  <a class="ds-frame-icon" target="_blank" rel="noopener" aria-label="Open in a new tab" title="Open in a new tab">${icons.external}</a>` +
            `  <button type="button" class="ds-frame-icon ds-frame-close" aria-label="Close" title="Close">${icons.close}</button>` +
            '</div>' +
            '<div class="ds-frame-stage"><div class="ds-frame-box"><iframe class="ds-frame-iframe"></iframe></div></div>';
        document.body.append(dialog);

        dialog.querySelector('.ds-frame-close').addEventListener('click', () => dialog.close());
        dialog.querySelector('.ds-frame-devices').addEventListener('click', e => {
            const b = e.target.closest('[data-device]');
            if (b) setDevice(b.dataset.device);
        });
        // A click on the backdrop lands on the dialog itself.
        dialog.addEventListener('click', e => { if (e.target === dialog) dialog.close(); });
        dialog.addEventListener('close', () => {
            html.classList.remove('ds-dialog-open');
            const frame = dialog.querySelector('iframe');
            frame.removeAttribute('src');
            frame.src = 'about:blank';
            current = null;
            if (opener?.isConnected) opener.focus({ preventScroll: true });
            opener = null;
        });
        window.addEventListener('resize', () => { if (current) layoutFrame(); });
        return dialog;
    }

    function openFrame(link) {
        const s = sections.find(x => x.id === link.closest('.ds-h2-tools')?.dataset.id);
        if (!s) return;
        const d = ensureDialog();
        opener = link;
        current = { id: s.id, title: s.title, device: link.dataset.device };
        d.querySelector('.ds-frame-title').textContent = s.title;
        const url = frameUrl(s.id);
        d.querySelector('a.ds-frame-icon').href = url;
        const frame = d.querySelector('iframe');
        frame.title = `${s.title} example`;
        html.classList.add('ds-dialog-open');
        if (!d.open) d.showModal();
        setDevice(current.device);   // measures the stage, so after the dialog shows
        frame.src = url;
        d.querySelector(`.ds-frame-device[data-device="${current.device}"]`).focus();
    }

    function setDevice(key) {
        if (!current) return;
        current.device = key;
        for (const b of dialog.querySelectorAll('.ds-frame-device'))
            b.setAttribute('aria-pressed', String(b.dataset.device === key));
        layoutFrame();
    }

    function layoutFrame() {
        const dev = devices.find(d => d.key === current.device);
        const stage = dialog.querySelector('.ds-frame-stage');
        const cs = getComputedStyle(stage);
        const availW = stage.clientWidth - parseFloat(cs.paddingLeft) - parseFloat(cs.paddingRight);
        const availH = stage.clientHeight - parseFloat(cs.paddingTop) - parseFloat(cs.paddingBottom);
        const scale = Math.min(1, availW / dev.width, availH / dev.height);
        const box = dialog.querySelector('.ds-frame-box');
        const frame = dialog.querySelector('iframe');
        box.style.width = Math.floor(dev.width * scale) + 'px';
        box.style.height = Math.floor(dev.height * scale) + 'px';
        frame.style.width = dev.width + 'px';
        frame.style.height = dev.height + 'px';
        frame.style.transform = scale < 1 ? `scale(${scale})` : '';
        dialog.dataset.device = dev.key;
        dialog.querySelector('.ds-frame-size').textContent =
            `${dev.label} · ${dev.width} × ${dev.height}` + (scale < 1 ? ` · shown at ${Math.round(scale * 100)}%` : '');
    }

    // ── Wiring ────────────────────────────────────────────────────────────────

    function decorate() {
        if (inFrame) {
            for (const s of readSections())
                s.section.classList.toggle('ds-frame-target', s.id === frameId);
            return;
        }
        sections = readSections();
        const framed = examplesPath();
        for (const s of sections) decorateHeading(s, framed);
        renderLists();
        scrollToHash();
    }

    let queued = false;
    function queueDecorate() {
        if (queued) return;
        queued = true;
        queueMicrotask(() => { queued = false; decorate(); });
    }

    function onClick(e) {
        if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        const a = e.target.closest?.('a.ds-anchor, a.ds-toc-link, a.ds-frame-link');
        if (!a) {
            // Any other link to a section of this page (the headings carry no id for the browser to find).
            const link = e.target.closest?.('a[href*="#"]');
            if (!link || link.target) return;
            const url = new URL(link.href);
            const id = decodeURIComponent(url.hash.slice(1));
            if (url.pathname !== location.pathname || url.search !== location.search || !sectionById(id)) return;
            e.preventDefault();
            goTo(id, true);
            return;
        }
        // Handled here, before Blazor's document listener sees the click (it skips prevented clicks).
        e.preventDefault();
        if (a.classList.contains('ds-frame-link')) {
            openFrame(a);
        } else if (a.classList.contains('ds-toc-link')) {
            goTo(a.dataset.id, true);
        } else {
            const id = a.closest('.ds-h2-tools').dataset.id;
            history.replaceState(history.state, '', location.pathname + location.search + '#' + id);
            handledUrl = location.href;
            copyText(sectionUrl(id)).then(ok => showToast(a, ok ? 'Link copied' : 'Copy failed'));
        }
    }

    function start() {
        decorate();
        wide.addEventListener('change', queueDecorate);
        new MutationObserver(queueDecorate).observe(document.body, { childList: true, subtree: true });
        if (inFrame) return;
        window.addEventListener('click', onClick, true);
        window.addEventListener('scroll', queueSpy, { passive: true });
        window.addEventListener('resize', queueSpy);
        window.addEventListener('hashchange', () => { handledUrl = null; scrollToHash(); });
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', start);
    else
        start();
})();
