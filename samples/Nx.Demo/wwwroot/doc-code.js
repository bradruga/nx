// Copies a code sample. navigator.clipboard needs a secure context (localhost or https); the demo is also opened
// over plain http on the LAN, so fall back to a hidden textarea and execCommand there.
export async function copy(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        const area = document.createElement('textarea');
        area.value = text;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.select();
        const ok = document.execCommand('copy');
        area.remove();
        return ok;
    }
}
