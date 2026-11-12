using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxTreeList: columns lining up under the header at every depth, and scrolling, need real layout.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class TreeListTests : PageTest
{
    private readonly string _baseUrl;

    public TreeListTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page, int width = 1280)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync($"{_baseUrl}/treelist/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    // Rows whose non-tree cells do not start where their header does.
    private Task<int> Misaligned(string tree) => Page.EvaluateAsync<int>("""
        id => {
            const root = document.getElementById(id).closest('.nx-treelist');
            const th = [...root.querySelectorAll('.nx-treelist-th')].slice(1).map(e => e.getBoundingClientRect().left);
            return [...root.querySelectorAll('.nx-treeview-row')].filter(r =>
                [...r.querySelectorAll('.nx-treelist-td')].slice(1).some((td, i) => Math.abs(td.getBoundingClientRect().left - th[i]) > 1)).length;
        }
        """, tree);

    [TestCase(1280)]
    [TestCase(390)]
    public async Task Columns_AlignAtEveryDepth(int width)
    {
        await GoToPage("examples", width);
        await Page.Locator("#expand-all").ClickAsync();
        await Expect(Page.Locator("#template-tree .nx-treeview-row")).ToHaveCountAsync(27);
        Assert.That(await Misaligned("template-tree"), Is.Zero);

        await Page.Locator("#detail-tree .nx-treeview-toggle").First.ClickAsync();
        await Expect(Page.Locator("#detail-tree .nx-treeview-row")).ToHaveCountAsync(6);
        Assert.That(await Misaligned("detail-tree"), Is.Zero);
    }

    [Test]
    public async Task Narrow_ScrollsInside_NotThePage()
    {
        await GoToPage("examples", 390);
        await Page.Locator("#expand-all").ClickAsync();
        var scroll = Page.Locator("#template-tree").Locator("xpath=ancestor::div[contains(@class,'nx-treelist-scroll')]");
        Assert.That(await scroll.EvaluateAsync<bool>("s => s.scrollWidth > s.clientWidth"), Is.True);
        Assert.That(await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth"), Is.LessThanOrEqualTo(0));
    }

    [Test]
    public async Task StartExpanded_ClickSelects_DrivesTheDetail()
    {
        await GoToPage("overview");
        await Expect(Page.Locator("#overview-tree [role=treeitem][aria-expanded=true]")).ToHaveCountAsync(2);
        await Page.Locator("#overview-tree [role=treeitem]").Filter(new() { HasText = "Stockyards" }).Locator(".nx-treeview-row").First.ClickAsync();
        await Expect(Page.Locator("#overview-selected")).ToHaveTextAsync("Stockyards: 38 pieces");

        await GoToPage("examples");
        await Page.Locator("#detail-tree .nx-treeview-row").First.ClickAsync();
        await Expect(Page.Locator("#detail-panel")).ToContainTextAsync("Lincoln Ave pieces");
    }

    [Test]
    public async Task Keyboard_ArrowsExpandAndSelect()
    {
        await GoToPage("examples");
        var tree = Page.Locator("#detail-tree");
        await tree.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(tree.Locator("[role=treeitem][aria-expanded=true]")).ToHaveCountAsync(1);
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#detail-panel")).ToContainTextAsync("A pieces");
    }

    [Test]
    public async Task HeaderSticks_WhileRowsScroll()
    {
        await GoToPage("examples");
        await Page.Locator("#expand-all").ClickAsync();
        var scroll = Page.Locator("#template-tree").Locator("xpath=ancestor::div[contains(@class,'nx-treelist-scroll')]");
        var header = scroll.Locator(".nx-treelist-header");
        var before = (await header.BoundingBoxAsync())!.Y;
        await scroll.EvaluateAsync("s => s.scrollTop = 200");
        await Page.WaitForFunctionAsync("() => true");
        Assert.That((await header.BoundingBoxAsync())!.Y, Is.EqualTo(before).Within(1));
    }
}
