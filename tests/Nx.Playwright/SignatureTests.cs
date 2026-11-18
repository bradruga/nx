using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxSignature: drawing with pointer events on a canvas needs a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class SignatureTests : PageTest
{
    private readonly string _baseUrl;

    public SignatureTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.GotoAsync($"{_baseUrl}/signature/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private async Task Draw(string pad)
    {
        var canvas = Page.Locator($"#{pad} canvas");
        await canvas.ScrollIntoViewIfNeededAsync();
        var box = (await canvas.BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(box.X + 30, box.Y + box.Height * 0.6f);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= 20; i++)
            await Page.Mouse.MoveAsync(box.X + 30 + i * 10, box.Y + box.Height * 0.6f - (float)Math.Sin(i / 2.0) * 30);
        await Page.Mouse.UpAsync();
    }

    private Task<int> InkPixels(string pad) =>
        Page.Locator($"#{pad} canvas").EvaluateAsync<int>("""
            c => { const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data; let n = 0;
                   for (let i = 0; i < d.length; i += 4) if (d[i] < 80) n++; return n; }
            """);

    /// <summary>[ink, paper] pixel counts of a PNG data URL: pixels within 40 of each RGB color.</summary>
    private static Task<int[]> CountColors(IPage page, string dataUrl, int[] ink, int[] paper) =>
        page.EvaluateAsync<int[]>("""
            async ({ url, ink, paper }) => {
                const img = new Image(); img.src = url; await img.decode();
                const c = document.createElement('canvas'); c.width = img.naturalWidth; c.height = img.naturalHeight;
                const ctx = c.getContext('2d'); ctx.drawImage(img, 0, 0);
                const d = ctx.getImageData(0, 0, c.width, c.height).data;
                const near = (i, rgb) => Math.abs(d[i] - rgb[0]) < 40 && Math.abs(d[i + 1] - rgb[1]) < 40 && Math.abs(d[i + 2] - rgb[2]) < 40;
                let n = 0, p = 0;
                for (let i = 0; i < d.length; i += 4) { if (near(i, ink)) n++; else if (near(i, paper)) p++; }
                return [n, p];
            }
            """, new { url = dataUrl, ink, paper });

    [Test]
    public async Task Touch_Draw_SavesAPngWithTheInk()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_baseUrl}/signature/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        var canvas = page.Locator("#overview-signature canvas");
        await canvas.ScrollIntoViewIfNeededAsync();
        var box = (await canvas.BoundingBoxAsync())!;
        var scroll = await page.EvaluateAsync<double>("() => window.scrollY");
        // Real touch input through the DevTools protocol: the browser makes pointer events of type touch from it.
        var cdp = await context.NewCDPSessionAsync(page);
        double x0 = box.X + 20, y = box.Y + box.Height / 2;
        await cdp.SendAsync("Input.dispatchTouchEvent", new() { ["type"] = "touchStart", ["touchPoints"] = new[] { new { x = x0, y } } });
        for (var i = 1; i <= 15; i++)
            await cdp.SendAsync("Input.dispatchTouchEvent", new() { ["type"] = "touchMove", ["touchPoints"] = new[] { new { x = x0 + i * 15, y = y + Math.Sin(i / 2.0) * 20 } } });
        await cdp.SendAsync("Input.dispatchTouchEvent", new() { ["type"] = "touchEnd", ["touchPoints"] = Array.Empty<object>() });

        await Expect(page.Locator("#overview-length")).ToContainTextAsync("Signed");
        var png = (await page.Locator("#overview-image").GetAttributeAsync("src"))!;
        Assert.That(png, Does.StartWith("data:image/png;base64,"));
        var counts = await CountColors(page, png, [0, 0, 0], [255, 255, 255]);
        Assert.That(counts[0], Is.GreaterThan(100), "black ink in the saved image");
        Assert.That(counts[1], Is.GreaterThan(counts[0]), "on white paper");
        Assert.That(await page.EvaluateAsync<double>("() => window.scrollY"), Is.EqualTo(scroll), "the page does not scroll under the finger");
    }

    [Test]
    public async Task Pen_Draw_ExportsTheStrokeColor()
    {
        // The example's value is not shown on the page: record the PNG the pad exports for it.
        await Page.AddInitScriptAsync("""
            (() => { const toDataURL = HTMLCanvasElement.prototype.toDataURL;
                     HTMLCanvasElement.prototype.toDataURL = function (...args) { const url = toDataURL.apply(this, args); window.__nxPngs = [...(window.__nxPngs ?? []), url]; return url; }; })()
            """);
        await GoToPage("examples");

        var canvas = Page.Locator("#blue-signature canvas");
        await canvas.ScrollIntoViewIfNeededAsync();
        await Expect(canvas).ToHaveAttributeAsync("aria-label", "Not signed");
        var box = (await canvas.BoundingBoxAsync())!;
        var cdp = await Page.Context.NewCDPSessionAsync(Page);
        double x0 = box.X + 30, y = box.Y + box.Height / 2;
        Dictionary<string, object> Pen(string type, double x, double py) => new()
        {
            ["type"] = type, ["x"] = x, ["y"] = py, ["button"] = "left", ["buttons"] = type == "mouseReleased" ? 0 : 1,
            ["clickCount"] = 1, ["pointerType"] = "pen", ["force"] = 0.5
        };
        await cdp.SendAsync("Input.dispatchMouseEvent", Pen("mousePressed", x0, y));
        for (var i = 1; i <= 20; i++)
            await cdp.SendAsync("Input.dispatchMouseEvent", Pen("mouseMoved", x0 + i * 12, y + Math.Sin(i / 2.0) * 25));
        await cdp.SendAsync("Input.dispatchMouseEvent", Pen("mouseReleased", x0 + 240, y));

        await Expect(canvas).ToHaveAttributeAsync("aria-label", "Signed");
        await Page.WaitForFunctionAsync("() => (window.__nxPngs ?? []).some(u => u.startsWith('data:image/png'))");
        var png = await Page.EvaluateAsync<string>("() => window.__nxPngs.filter(u => u.startsWith('data:image/png')).at(-1)");
        // Color="#1d4ed8" on BackgroundColor="#fdfbe8".
        var counts = await CountColors(Page, png, [0x1d, 0x4e, 0xd8], [0xfd, 0xfb, 0xe8]);
        Assert.That(counts[0], Is.GreaterThan(100), "blue ink in the exported image");
        Assert.That(counts[1], Is.GreaterThan(counts[0]), "on the cream paper");
        var black = await CountColors(Page, png, [0, 0, 0], [255, 255, 255]);
        Assert.That(black[0], Is.Zero, "no default black ink");
    }

    [Test]
    public async Task Draw_SavesAPng_ShowsIt()
    {
        await GoToPage("overview");
        await Draw("overview-signature");

        await Expect(Page.Locator("#overview-length")).ToContainTextAsync("Signed");
        await Expect(Page.Locator("#overview-signature canvas")).ToHaveAttributeAsync("aria-label", "Signed");
        var size = await Page.Locator("#overview-image").EvaluateAsync<int[]>("i => [i.naturalWidth, i.naturalHeight]");
        // ExportScale 2: twice the pad's drawing area.
        Assert.That(size[0], Is.GreaterThan(500));
        Assert.That(await InkPixels("overview-signature"), Is.GreaterThan(100));
    }

    [Test]
    public async Task Required_ThenSigned_Submits_ClearEmpties()
    {
        await GoToPage("examples");
        await Page.Locator("#signature-submit").ClickAsync();
        await Expect(Page.Locator(".validation-message")).ToHaveTextAsync("Sign the form.");
        await Expect(Page.Locator("#form-signature")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-signature-invalid"));

        await Draw("form-signature");
        await Expect(Page.Locator("#form-signature .nx-signature-clear")).ToBeVisibleAsync();
        await Page.Locator("#signature-submit").ClickAsync();
        await Expect(Page.Locator("#signature-result")).ToHaveTextAsync("Submitted");

        await Page.Locator("#form-signature .nx-signature-clear").ClickAsync();
        await Expect(Page.Locator("#form-signature canvas")).ToHaveAttributeAsync("aria-label", "Not signed");
        Assert.That(await InkPixels("form-signature"), Is.Zero);
    }

    [Test]
    public async Task ReadOnly_ShowsTheSaved_IgnoresDrawing()
    {
        await GoToPage("examples");
        await Page.Locator("#load-signature").ClickAsync();
        await Page.WaitForFunctionAsync("""
            () => { const c = document.querySelector('#readonly-signature canvas'); const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
                    for (let i = 0; i < d.length; i += 4) if (d[i] < 80) return true; return false; }
            """);
        var before = await InkPixels("readonly-signature");
        await Draw("readonly-signature");
        Assert.That(await InkPixels("readonly-signature"), Is.EqualTo(before));
        await Expect(Page.Locator("#readonly-signature .nx-signature-clear")).ToHaveCountAsync(0);
    }
}
