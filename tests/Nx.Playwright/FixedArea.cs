using Microsoft.Playwright;

namespace Nx.Playwright;

/// <summary>
/// The area fixed elements (windows, popups, sheets, toasts) are placed in. Measure against it, not the viewport:
/// headless Chromium hides scrollbars, so clientWidth reads the full width, yet the demo's
/// <c>scrollbar-gutter: stable</c> still keeps 15 px from fixed layout.
/// </summary>
internal static class FixedArea
{
    /// <summary>The width and height of the area, from a probe pinned to its bottom-right corner.</summary>
    public static async Task<(double Width, double Height)> SizeAsync(IPage page)
    {
        var edges = await page.EvaluateAsync<double[]>(
            "() => { const p = document.createElement('div'); p.style.cssText = 'position:fixed;right:0;bottom:0'; document.body.append(p); const r = p.getBoundingClientRect(); p.remove(); return [r.right, r.bottom]; }");
        return (edges[0], edges[1]);
    }
}
