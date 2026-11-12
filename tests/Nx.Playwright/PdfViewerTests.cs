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
    public async Task Download_SavesUnderTheFileName()
    {
        await GoToPage("overview");
        await Expect(Page.Locator("#overview-pdf .nx-pdfviewer-pages")).ToHaveTextAsync("of 3");
        var download = await Page.RunAndWaitForDownloadAsync(() => Tool("overview-pdf", "download").ClickAsync());
        Assert.That(download.SuggestedFilename, Is.EqualTo("Pour report P-14.pdf"));
    }
}
