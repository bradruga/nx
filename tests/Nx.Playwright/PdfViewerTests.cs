using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxPdfViewer: pdf.js loads, renders and finds only in a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class PdfViewerTests : PageTest
{
    private readonly string _baseUrl;

    public PdfViewerTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.GotoAsync($"{_baseUrl}/pdfviewer/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Tool(string viewer, string tool) => Page.Locator($"#{viewer} [data-nx-pdf-tool={tool}]");

    [Test]
    public async Task Data_RendersPagesWithText()
    {
        await GoToPage("overview");
        await Expect(Page.Locator("#overview-pdf .nx-pdfviewer-pages")).ToHaveTextAsync("of 3");
        await Expect(Page.Locator("#overview-pdf .page")).ToHaveCountAsync(3);
        await Expect(Page.Locator("#overview-pdf .page[data-page-number='1'] canvas")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#overview-pdf .page[data-page-number='1'] .textLayer")).ToContainTextAsync("Pour Report P-14");
    }

    [Test]
    public async Task Find_CountsAndSteps()
    {
        await GoToPage("overview");
        await Expect(Page.Locator("#overview-pdf .nx-pdfviewer-pages")).ToHaveTextAsync("of 3");
        var find = Page.Locator("#overview-pdf-find");
        await find.FillAsync("slump");
        await Expect(Page.Locator("#overview-pdf .nx-pdfviewer-matches")).ToHaveTextAsync("1 of 3");
        await Expect(Page.Locator("#overview-pdf .textLayer .highlight").First).ToBeVisibleAsync();

        await find.PressAsync("Enter");
        await Expect(Page.Locator("#overview-pdf .nx-pdfviewer-matches")).ToHaveTextAsync("2 of 3");
        await Expect(Page.Locator("#overview-pdf input[aria-label='Page']")).ToHaveValueAsync("2");
    }

    [Test]
    public async Task Zoom_And_Pager()
    {
        await GoToPage("overview");
        await Expect(Page.Locator("#overview-pdf .nx-pdfviewer-pages")).ToHaveTextAsync("of 3");
        await Tool("overview-pdf", "zoomin").ClickAsync();
        await Expect(Page.Locator("#overview-pdf .nx-pdfviewer-zoom input")).Not.ToHaveValueAsync("Automatic");

        await Tool("overview-pdf", "next").ClickAsync();
        await Expect(Page.Locator("#overview-pdf input[aria-label='Page']")).ToHaveValueAsync("2");
        await Expect(Tool("overview-pdf", "previous")).ToBeEnabledAsync();
    }

    [Test]
    public async Task Url_ResolvesAgainstTheBase_PageFit()
    {
        await GoToPage("examples");
        await Expect(Page.Locator("#url-pdf .nx-pdfviewer-pages")).ToHaveTextAsync("of 3");
        await Expect(Page.Locator("#url-pdf .nx-pdfviewer-zoom input")).ToHaveValueAsync("Page fit");
    }

    [Test]
    public async Task BindPage_JumpsAndFollows()
    {
        await GoToPage("examples");
        await Expect(Page.Locator("#page-jump-3")).ToBeVisibleAsync();
        await Page.Locator("#page-jump-3").ClickAsync();
        await Expect(Page.Locator("#page-value")).ToHaveTextAsync("Page 3 of 3");

        await Page.Locator("#page-pdf .nx-pdfviewer-scroll").EvaluateAsync("e => e.scrollTop = 0");
        await Expect(Page.Locator("#page-value")).ToHaveTextAsync("Page 1 of 3");
    }

    [Test]
    public async Task Switch_BrokenEmptyAndBack()
    {
        await GoToPage("examples");
        await Page.Locator("#switch-broken").ClickAsync();
        await Expect(Page.Locator("#switch-pdf .nx-pdfviewer-error")).ToContainTextAsync("could not be opened");
        await Expect(Page.Locator("#switch-error")).Not.ToBeEmptyAsync();

        await Page.Locator("#switch-none").ClickAsync();
        await Expect(Page.Locator("#switch-pdf .nx-pdfviewer-message")).ToHaveTextAsync("Pick a document above");

        await Page.Locator("#switch-report").ClickAsync();
        await Expect(Page.Locator("#switch-pdf .page")).ToHaveCountAsync(3);
    }

    [Test]
    public async Task Print_PrintsTheFileInAHiddenFrame_NoErrors()
    {
        var errors = new List<string>();
        Page.Console += (_, m) => { if (m.Type == "error") errors.Add(m.Text); };
        Page.PageError += (_, e) => errors.Add(e);
        // Runs in every frame: print is stubbed (headless would block on it) and counted on the page; a failed print opens a window.
        await Page.AddInitScriptAsync("""
            window.print = () => { top.__printed = (top.__printed || 0) + 1; };
            window.open = (...args) => { top.__opened = args; return null; };
            """);
        await GoToPage("examples");
        await Expect(Page.Locator("#url-pdf .nx-pdfviewer-pages")).ToHaveTextAsync("of 3");

        await Tool("url-pdf", "print").ClickAsync();
        var frame = Page.Locator("iframe[src^='blob:']");
        await Expect(frame).ToHaveCountAsync(1);
        Assert.That(await frame.EvaluateAsync<double>("f => f.getBoundingClientRect().width"), Is.EqualTo(0), "hidden");
        Assert.That(await frame.EvaluateAsync<string>("async f => (await (await fetch(f.src)).text()).slice(0, 5)"), Is.EqualTo("%PDF-"), "the frame shows the file");
        // Headless Chromium has no PDF viewer: the frame never loads the file, so its load is raised here to run the print.
        await frame.EvaluateAsync("f => f.dispatchEvent(new Event('load'))");
        await Page.WaitForFunctionAsync("() => window.__printed >= 1 || window.__opened !== undefined");
        Assert.That(await Page.EvaluateAsync<bool>("() => window.__opened === undefined"), Is.True, "the frame printed; no window fallback");
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task Download_SavesUnderTheFileName()
    {
        await GoToPage("overview");
        await Expect(Page.Locator("#overview-pdf .nx-pdfviewer-pages")).ToHaveTextAsync("of 3");
        var download = await Page.RunAndWaitForDownloadAsync(() => Tool("overview-pdf", "download").ClickAsync());
        Assert.That(download.SuggestedFilename, Is.EqualTo("Pour report P-14.pdf"));
    }
}
