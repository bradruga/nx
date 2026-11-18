using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxTreeView.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class TreeViewTests : PageTest
{
    private readonly string _baseUrl;

    public TreeViewTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.GotoAsync($"{_baseUrl}/treeview/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Row(string treeId, string text) =>
        Page.Locator($"#{treeId} .nx-treeview-row").Filter(new() { Has = Page.Locator(".nx-treeview-content", new() { HasTextRegex = new($"^\\s*{text}\\s*$") }) });

    [Test]
    public async Task Click_ExpandsAndSelects_DetailFollows()
    {
        await GoToPage("overview");

        await Expect(Row("overview-tree", "Pay bills")).ToBeVisibleAsync();
        await Row("overview-tree", "Commercial").Locator(".nx-treeview-toggle").ClickAsync();
        await Expect(Row("overview-tree", "Projects")).ToBeVisibleAsync();

        await Row("overview-tree", "Edit bills").ClickAsync();
        await Expect(Page.Locator("#overview-detail")).ToContainTextAsync("Acc.Bills.Edit");
        await Expect(Row("overview-tree", "Edit bills")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-treeview-row-selected"));
        await Expect(Page.Locator("#overview-tree [aria-selected=true]")).ToHaveCountAsync(1);
    }

    /// <summary>The overview tree; its items are numbered in data order (Accounting 0, Bills 1, View bills 2, Employee 15, Documents 16).</summary>
    private ILocator Tree => Page.Locator("#overview-tree");

    [Test]
    public async Task Keyboard_AsTheAriaTree_PageDoesNotScroll()
    {
        await Page.SetViewportSizeAsync(1280, 700);
        await GoToPage("overview");

        await Page.Locator("#overview-tree").FocusAsync();
        await Expect(Tree).ToHaveAttributeAsync("aria-activedescendant", "overview-tree-0");
        var scroll = await Page.EvaluateAsync<double>("() => window.scrollY");

        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Tree).ToHaveAttributeAsync("aria-activedescendant", "overview-tree-2");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(Tree).ToHaveAttributeAsync("aria-activedescendant", "overview-tree-1");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(Row("overview-tree", "View bills")).ToHaveCountAsync(0);
        await Page.Keyboard.PressAsync("End");
        await Expect(Tree).ToHaveAttributeAsync("aria-activedescendant", "overview-tree-15");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Tree).ToHaveAttributeAsync("aria-activedescendant", "overview-tree-16");
        await Page.Keyboard.PressAsync("Space");

        await Expect(Page.Locator("#overview-detail")).ToContainTextAsync("A group of permissions");
        Assert.That(await Page.EvaluateAsync<double>("() => window.scrollY"), Is.EqualTo(scroll), "arrows and space do not scroll the page");
        await Expect(Row("overview-tree", "Documents")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-treeview-row-active"));
    }

    [Test]
    public async Task Multiple_ClicksAddAndRemove()
    {
        await GoToPage("examples");

        await Row("multi-tree", "Pay bills").ClickAsync();
        await Row("multi-tree", "Edit bills").ClickAsync();
        await Expect(Page.Locator("#multi-detail strong")).ToHaveTextAsync("2 selected");
        await Expect(Page.Locator("#multi-detail > span")).ToHaveTextAsync(["Edit bills", "Pay bills"]);

        await Row("multi-tree", "Pay bills").ClickAsync();
        await Expect(Page.Locator("#multi-detail strong")).ToHaveTextAsync("1 selected");
    }

    [Test]
    public async Task Nested_ExpandAll_CollapseAll()
    {
        await GoToPage("examples");

        await Page.Locator("#nested-expand").ClickAsync();
        await Expect(Row("nested-tree", "E-101 Podium")).ToBeVisibleAsync();
        await Page.Locator("#nested-collapse").ClickAsync();
        await Expect(Page.Locator("#nested-tree .nx-treeview-row")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Drag_IntoBeforeAndRefused()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("examples");
        var log = Page.Locator("#drag-log > span");

        var target = Row("drag-tree", "Bay A");
        var box = (await target.BoundingBoxAsync())!;
        await Row("drag-tree", "Column form 2").DragToAsync(target, new() { TargetPosition = new() { X = 60, Y = (float)(box.Height / 2) } });
        await Expect(log).ToHaveTextAsync("Column form 2 inside Bay A");
        // Bay A's own item: a Has locator is relative to each li, so it must not start at #drag-tree (the ul itself).
        var bayA = Page.Locator("#drag-tree li").Filter(new() { Has = Page.Locator("xpath=./div[contains(@class,'nx-treeview-row')]//span[contains(@class,'nx-treeview-content')][normalize-space()='Bay A']") });
        await Expect(bayA.Locator(".nx-treeview-group .nx-treeview-content", new() { HasText = "Column form 2" })).ToHaveCountAsync(1);

        await Row("drag-tree", "Repair shop").DragToAsync(Row("drag-tree", "Main yard"), new() { TargetPosition = new() { X = 60, Y = 3 } });
        await Expect(log).ToHaveTextAsync("Repair shop before Main yard");
        await Expect(Page.Locator("#drag-tree > li >.nx-treeview-row .nx-treeview-content").First).ToHaveTextAsync("Repair shop");

        await Row("drag-tree", "Main yard").DragToAsync(Row("drag-tree", "Bay B"), new() { TargetPosition = new() { X = 60, Y = (float)(box.Height / 2) } });
        await Expect(log).ToHaveTextAsync("Repair shop before Main yard");
    }

    [Test]
    public async Task Drag_LowerThird_DropsAfter()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("examples");
        var bayA = Page.Locator("#drag-tree li").Filter(new() { Has = Page.Locator("xpath=./div[contains(@class,'nx-treeview-row')]//span[contains(@class,'nx-treeview-content')][normalize-space()='Bay A']") });
        var forms = bayA.Locator(".nx-treeview-group .nx-treeview-content");
        await Expect(forms).ToHaveTextAsync(["Wall form 12", "Wall form 14"]);

        var target = Row("drag-tree", "Wall form 14");
        var box = (await target.BoundingBoxAsync())!;
        await Row("drag-tree", "Wall form 12").DragToAsync(target, new() { TargetPosition = new() { X = 60, Y = (float)(box.Height - 3) } });
        await Expect(Page.Locator("#drag-log > span")).ToHaveTextAsync("Wall form 12 after Wall form 14");
        await Expect(forms).ToHaveTextAsync(["Wall form 14", "Wall form 12"]);
    }
}
