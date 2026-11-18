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
            ({ x, y }) => {
                const el = document.elementFromPoint(x, y);
                el.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'touch', clientX: x, clientY: y }));
                el.dispatchEvent(new MouseEvent('click', { bubbles: true, clientX: x, clientY: y }));
            }
            """, new { x = (double)box.X + 20, y = (double)box.Y + 10 });   // a float arrives in JS as a non-number
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

    private ILocator Names(string manager) => Page.Locator($"#{manager} [role=option] .nx-filemanager-name-text");

    private ILocator ViewButton(string manager, string which) => Page.Locator($"#{manager} [data-nx-filemanager={which}]");

    [Test]
    public async Task ColumnHeaders_Sort_FoldersFirst()
    {
        await GoToPage("overview");
        await Expect(Names("overview-files")).ToHaveTextAsync(["Archive", "Drawings", "Photos", "Quality", "Contract.pdf", "Read me.txt"]);

        await Page.Locator("#overview-files .nx-filemanager-th", new() { HasText = "Name" }).ClickAsync();
        await Expect(Page.Locator("#overview-files .nx-filemanager-th", new() { HasText = "Name" })).ToHaveTextAsync("Name ↓");
        await Expect(Names("overview-files")).ToHaveTextAsync(["Quality", "Photos", "Drawings", "Archive", "Read me.txt", "Contract.pdf"]);

        // Size: the files by size, the folders still ahead of them.
        await Page.Locator("#overview-files .nx-filemanager-th", new() { HasText = "Size" }).ClickAsync();
        await Expect(Names("overview-files").Nth(4)).ToHaveTextAsync("Read me.txt");
        await Expect(Names("overview-files").Nth(5)).ToHaveTextAsync("Contract.pdf");
        var folders = await Page.Locator("#overview-files [role=option]").EvaluateAllAsync<bool[]>(
            "os => os.map(o => !!o.querySelector('.nx-filemanager-icon-folder'))");
        Assert.That(folders, Is.EqualTo(new[] { true, true, true, true, false, false }));
    }

    [Test]
    public async Task Tiles_SwitchesTheLayout_ArrowsStillSelect()
    {
        await GoToPage("overview");
        await ViewButton("overview-files", "tiles").ClickAsync();
        await Expect(ViewButton("overview-files", "tiles")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator("#overview-files .nx-filemanager-list")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-filemanager-tiles"));
        await Expect(Page.Locator("#overview-files .nx-filemanager-head")).ToHaveCountAsync(0);
        // Tiles sit side by side.
        var first = (await Entry("overview-files", "Archive").BoundingBoxAsync())!;
        var second = (await Entry("overview-files", "Drawings").BoundingBoxAsync())!;
        Assert.That(second.Y, Is.EqualTo(first.Y).Within(1));
        Assert.That(second.X, Is.GreaterThan(first.X + first.Width - 1));

        await Page.Locator("#overview-files [role=listbox]").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Page.Locator("#overview-selected")).ToHaveTextAsync("Selected: Drawings");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(Page.Locator("#overview-selected")).ToHaveTextAsync("Selected: Archive");
        await Expect(Entry("overview-files", "Archive")).ToHaveAttributeAsync("aria-selected", "true");
    }

    [Test]
    public async Task DetailsToggle_HidesThePane_ListWidens()
    {
        await GoToPage("overview");
        var main = Page.Locator("#overview-files .nx-filemanager-main");
        await Expect(Page.Locator("#overview-files .nx-filemanager-details")).ToBeVisibleAsync();
        var before = (await main.BoundingBoxAsync())!.Width;
        var pane = (await Page.Locator("#overview-files .nx-filemanager-details").BoundingBoxAsync())!.Width;

        await ViewButton("overview-files", "details").ClickAsync();
        await Expect(ViewButton("overview-files", "details")).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(Page.Locator("#overview-files .nx-filemanager-details")).ToHaveCountAsync(0);
        Assert.That((await main.BoundingBoxAsync())!.Width - before, Is.EqualTo(pane).Within(2));
    }

    [Test]
    public async Task Breadcrumb_GoesUp_TreeFollows()
    {
        await GoToPage("overview");
        await Entry("overview-files", "Drawings").DblClickAsync();
        await Entry("overview-files", "Shop").DblClickAsync();
        await Expect(Page.Locator("#overview-files .nx-treeview [aria-selected=true] > .nx-treeview-row")).ToHaveTextAsync("Shop");

        await Page.Locator("#overview-files .nx-filemanager-crumb").Nth(1).ClickAsync();
        await Expect(Page.Locator("#overview-folder")).ToHaveTextAsync("Folder: Drawings");
        await Expect(Page.Locator("#overview-files .nx-filemanager-crumb")).ToHaveTextAsync(["Route 9 Bridge", "Drawings"]);
        await Expect(Page.Locator("#overview-files .nx-treeview [aria-selected=true] > .nx-treeview-row")).ToHaveTextAsync("Drawings");
        await Expect(Page.Locator("#overview-files .nx-treeview [aria-selected=true]")).ToHaveCountAsync(1);
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
