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
