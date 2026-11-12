using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxFileManager: double-click, touch taps, container-sized layout and scrolling need a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class FileManagerTests : PageTest
{
    private readonly string _baseUrl;

    public FileManagerTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page, int width = 1280)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync($"{_baseUrl}/filemanager/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Entry(string manager, string text) => Page.Locator($"#{manager} [role=option]").Filter(new() { HasText = text });

    [Test]
    public async Task DoubleClick_IntoFolders_TreeAndCrumbsFollow()
    {
        await GoToPage("overview");
        await Entry("overview-files", "Drawings").DblClickAsync();
        await Entry("overview-files", "Shop").DblClickAsync();

        await Expect(Page.Locator("#overview-folder")).ToHaveTextAsync("Folder: Shop");
        await Expect(Page.Locator("#overview-files .nx-filemanager-crumb")).ToHaveTextAsync(["Route 9 Bridge", "Drawings", "Shop"]);
        await Expect(Page.Locator("#overview-files .nx-treeview [aria-selected=true] > .nx-treeview-row")).ToHaveTextAsync("Shop");

        await Page.Locator("#overview-files .nx-filemanager-crumb").Nth(0).ClickAsync();
        await Expect(Page.Locator("#overview-folder")).ToHaveTextAsync("Folder: Route 9 Bridge");
    }

    [Test]
    public async Task DoubleClick_File_Opens_DownloadFromDetails()
    {
        await GoToPage("overview");
        await Entry("overview-files", "Contract").DblClickAsync();
        await Expect(Page.Locator("#overview-opened")).ToHaveTextAsync("Opened Contract.pdf");

        await Page.Locator("#overview-files .nx-filemanager-details button").Filter(new() { HasText = "Download" }).ClickAsync();
        await Expect(Page.Locator("#overview-opened")).ToHaveTextAsync("Downloaded Contract.pdf");
    }

    [Test]
    public async Task Touch_TapOpens()
    {
        await GoToPage("overview");
        var box = (await Entry("overview-files", "Drawings").BoundingBoxAsync())!;
        await Page.EvaluateAsync("""
            ([x, y]) => {
                const el = document.elementFromPoint(x, y);
                el.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'touch', clientX: x, clientY: y }));
                el.dispatchEvent(new MouseEvent('click', { bubbles: true, clientX: x, clientY: y }));
            }
            """, new[] { box.X + 20, box.Y + 10 });
        await Expect(Page.Locator("#overview-folder")).ToHaveTextAsync("Folder: Drawings");
    }

    [Test]
    public async Task Keyboard_ArrowsEnterBackspace()
    {
        await GoToPage("overview");
        var list = Page.Locator("#overview-files [role=listbox]");
        await list.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Page.Locator("#overview-selected")).ToHaveTextAsync("Selected: Drawings");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#overview-folder")).ToHaveTextAsync("Folder: Drawings");
        await Page.Keyboard.PressAsync("Backspace");
        await Expect(Page.Locator("#overview-folder")).ToHaveTextAsync("Folder: Route 9 Bridge");
    }

    [Test]
    public async Task Search_FindsInSubfolders()
    {
        await GoToPage("examples");
        await Page.Locator("#search-files-search").FillAsync("wp-1");
        await Expect(Page.Locator("#search-files [role=option]")).ToHaveCountAsync(5);
        await Expect(Page.Locator("#search-files .nx-filemanager-crumb-search")).ToHaveTextAsync("Search results");
        await Expect(Entry("search-files", "WP-12 shop drawing").Locator(".nx-filemanager-location")).ToHaveTextAsync("in Drawings › Shop");
    }

    [Test]
    public async Task FolderFromCode_OpensTreeToIt()
    {
        await GoToPage("examples");
        await Page.Locator("#go-cylinder-breaks").ClickAsync();
        await Expect(Page.Locator("#go-files .nx-filemanager-crumb")).ToHaveTextAsync(["Project", "Quality", "Cylinder breaks"]);
        await Expect(Page.Locator("#go-files .nx-treeview [role=treeitem][aria-expanded=true]")).ToHaveCountAsync(1);
    }

    [TestCase(390)]
    [TestCase(1280)]
    public async Task List_NeverScrollsSideways(int width)
    {
        await GoToPage("overview", width);
        Assert.That(await Page.Locator("#overview-files .nx-filemanager-list").EvaluateAsync<int>("l => l.scrollWidth - l.clientWidth"), Is.LessThanOrEqualTo(0));
        Assert.That(await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth"), Is.LessThanOrEqualTo(0));
    }
}
