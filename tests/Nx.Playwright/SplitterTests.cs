using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class SplitterTests : PageTest
{
    private readonly string _baseUrl;

    public SplitterTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Bar(string splitterId, int index = 0) => Page.Locator($"#{splitterId} > .nx-splitter-bar").Nth(index);

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/splitter/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private async Task<float> WidthAsync(string id) => (await Page.Locator($"#{id}").BoundingBoxAsync())!.Width;

    private async Task DragAsync(ILocator bar, float dx, float dy)
    {
        await bar.ScrollIntoViewIfNeededAsync();
        var box = (await bar.BoundingBoxAsync())!;
        var x = box.X + box.Width / 2;
        var y = box.Y + Math.Min(box.Height / 2, 30);
        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x + dx, y + dy, new() { Steps = 6 });
        await Page.Mouse.UpAsync();
    }

    [Test]
    public async Task Drag_ResizesAndReportsPercent()
    {
        await GoToPage("overview");
        var before = await WidthAsync("overview-list");

        await DragAsync(Bar("overview-splitter"), 100, 0);

        await Expect(Page.Locator("#overview-size")).Not.ToHaveTextAsync("Size: 35%");
        Assert.That(await WidthAsync("overview-list") - before, Is.EqualTo(100).Within(2));
        var text = await Page.Locator("#overview-size").TextContentAsync();
        Assert.That(text, Does.EndWith("%"));
    }

    [Test]
    public async Task Keyboard_ArrowsHomeEnd_StopAtLimits()
    {
        await GoToPage("overview");
        var bar = Bar("overview-splitter");
        var before = await WidthAsync("overview-list");

        await bar.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        // .NET writes each step's size back a round trip later: wait for the last one, not the first that passes.
        await Page.WaitForFunctionAsync("w => Math.abs(document.querySelector('#overview-list').getBoundingClientRect().width - w) < 1", (double)before + 20);

        await Page.Keyboard.PressAsync("Home");
        await Page.WaitForFunctionAsync("() => Math.abs(document.querySelector('#overview-list').getBoundingClientRect().width - 160) < 1");

        await Page.Keyboard.PressAsync("End");
        await Expect(Page.Locator("#overview-size")).ToHaveTextAsync("Size: 70%");
    }

    [Test]
    public async Task Bar_FocusRingKeyboardOnly_AriaValueNow()
    {
        await GoToPage("overview");
        var bar = Bar("overview-splitter");

        await Expect(bar).ToHaveAttributeAsync("aria-valuenow", "35");
        await Page.Locator("#overview-splitter").ClickAsync(new() { Position = new() { X = 5, Y = 5 } });
        await Page.Keyboard.PressAsync("Tab");
        await Expect(bar).ToBeFocusedAsync();
        await Expect(bar).ToHaveCSSAsync("outline-style", "solid");

        await Page.Keyboard.PressAsync("Shift+ArrowRight");
        await Expect(bar).Not.ToHaveAttributeAsync("aria-valuenow", "35");
    }

    [Test]
    public async Task Vertical_ArrowDownGrowsTop()
    {
        await GoToPage();
        var top = Page.Locator("#vertical-top");
        var before = (await top.BoundingBoxAsync())!.Height;

        await Bar("vertical-splitter").FocusAsync();
        await Page.Keyboard.PressAsync("Shift+ArrowDown");

        await Page.WaitForFunctionAsync($"() => document.querySelector('#vertical-top').getBoundingClientRect().height > {before + 49}");
        await Expect(Page.Locator("#vertical-size")).Not.ToHaveTextAsync("Top: 40%");
    }

    [Test]
    public async Task ThreePanes_RightBarResizesRightPane_LeftStopsAtMax()
    {
        await GoToPage();
        var before = await WidthAsync("three-right");

        await DragAsync(Bar("three-splitter", 1), -60, 0);
        await Expect(Page.Locator("#three-size")).Not.ToContainTextAsync("Right: 25%");
        Assert.That(await WidthAsync("three-right") - before, Is.EqualTo(60).Within(4));

        await DragAsync(Bar("three-splitter", 0), 400, 0);
        await Expect(Page.Locator("#three-size")).ToContainTextAsync("Left: 320px");
    }

    [Test]
    public async Task Collapse_FromThePagesOwnButtons_KeepsContent_NoButtonOnTheBar()
    {
        await GoToPage();
        await Expect(Page.Locator("#collapse-splitter .nx-splitter-bar button")).ToHaveCountAsync(0);

        await Page.Locator("#collapse-filter").FillAsync("abc");
        await Page.Locator("#toggle-nav").ClickAsync();
        await Expect(Page.Locator("#collapse-state")).ToContainTextAsync("Navigation: collapsed");
        Assert.That(await WidthAsync("collapse-nav"), Is.EqualTo(0).Within(1));
        await Expect(Bar("collapse-splitter")).ToHaveAttributeAsync("aria-disabled", "true");

        await Page.Locator("#toggle-nav").ClickAsync();
        await Expect(Page.Locator("#collapse-state")).ToContainTextAsync("Navigation: open");
        await Expect(Page.Locator("#collapse-filter")).ToHaveValueAsync("abc");

        // Enter on a bar no longer collapses anything.
        await Bar("collapse-splitter", 1).FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Page.Locator("#toggle-props").ClickAsync();
        await Expect(Page.Locator("#collapse-state")).ToContainTextAsync("Properties: collapsed");
        await Page.Locator("#toggle-props").ClickAsync();
        await Expect(Page.Locator("#collapse-state")).ToContainTextAsync("Properties: open");
    }

    [Test]
    public async Task Collapse_LastPane_ItsBarGoes_TheOtherBarStillDrags()
    {
        await GoToPage();
        await Page.Locator("#collapse-splitter").ScrollIntoViewIfNeededAsync();
        await Expect(Bar("collapse-splitter", 1)).ToBeVisibleAsync();

        await Page.Locator("#toggle-props").ClickAsync();
        await Expect(Page.Locator("#collapse-state")).ToContainTextAsync("Properties: collapsed");
        await Expect(Bar("collapse-splitter", 1)).ToBeHiddenAsync();
        await Page.WaitForFunctionAsync("() => document.querySelector('#collapse-props').getBoundingClientRect().width < 1");

        var before = await WidthAsync("collapse-nav");
        await DragAsync(Bar("collapse-splitter", 0), 80, 0);
        await Page.WaitForFunctionAsync("w => Math.abs(document.querySelector('#collapse-nav').getBoundingClientRect().width - (w + 80)) < 3", (double)before);
        Assert.That(await WidthAsync("collapse-props"), Is.EqualTo(0).Within(1), "the collapsed pane stays collapsed");
    }

    [Test]
    public async Task Nested_InnerBarMovesOnlyInner()
    {
        await GoToPage();
        var outer = await WidthAsync("nested-list");

        await DragAsync(Bar("nested-inner"), 0, -40);

        await Expect(Page.Locator("#nested-size")).Not.ToHaveTextAsync("Detail: 55%");
        Assert.That(await WidthAsync("nested-list"), Is.EqualTo(outer).Within(1));
    }

    [Test]
    public async Task Fixed_DoesNotMove()
    {
        await GoToPage();

        await DragAsync(Bar("fixed-splitter"), 100, 0);

        Assert.That(await WidthAsync("fixed-left"), Is.EqualTo(160).Within(1));
        await Expect(Bar("fixed-splitter")).ToHaveAttributeAsync("tabindex", "-1");
    }

    [Test]
    public async Task Playground_Orientation_Collapse()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-collapsed").CheckAsync();
        await Expect(Page.Locator("#pg-first")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-splitter-pane-collapsed"));
        await Page.Locator("#pg-collapsed").UncheckAsync();

        await Page.Locator("#pg-orientation").SelectOptionAsync("Vertical");
        await Expect(Page.Locator("#playground-splitter")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-splitter-vertical"));
        var first = (await Page.Locator("#pg-first").BoundingBoxAsync())!;
        var second = (await Page.Locator("#pg-second").BoundingBoxAsync())!;
        Assert.That(second.Y, Is.GreaterThan(first.Y));
    }

    [Test]
    public async Task ToolWindows_HideFromThePanel_ShowFromTheStrip_BarGoesWithIt()
    {
        await GoToPage();
        var state = Page.Locator("#ide-state");
        await Page.Locator("#ide").ScrollIntoViewIfNeededAsync();
        await Expect(state).ToContainTextAsync("Explorer: shown");
        var editorBefore = await WidthAsync("ide-center");

        await Page.Locator("#ide-hide-explorer").ClickAsync();
        await Expect(state).ToContainTextAsync("Explorer: hidden");
        Assert.That(await WidthAsync("ide-explorer"), Is.EqualTo(0).Within(1));
        await Expect(Bar("ide-splitter")).ToBeHiddenAsync();
        Assert.That(await WidthAsync("ide-center"), Is.GreaterThan(editorBefore + 150), "the editor takes the room");
        await Expect(Page.Locator("#ide-show-explorer")).ToHaveAttributeAsync("aria-pressed", "false");

        await Page.Locator("#ide-show-explorer").ClickAsync();
        await Expect(state).ToContainTextAsync("Explorer: shown");
        await Expect(Bar("ide-splitter")).ToBeVisibleAsync();

        // The bottom panel, in the nested splitter; and one that starts hidden.
        await Page.Locator("#ide-hide-terminal").ClickAsync();
        await Expect(state).ToContainTextAsync("Terminal: hidden");
        await Page.Locator("#ide-show-terminal").ClickAsync();
        await Expect(state).ToContainTextAsync("Terminal: shown");
        await Expect(state).ToContainTextAsync("Structure: hidden");
        await Page.Locator("#ide-show-structure").ClickAsync();
        await Expect(Page.Locator("#ide-hide-structure")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Phone_PanesStack_UnderTheirTitles_NoBars()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/splitter/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
        await Expect(page.Locator("#collapse-splitter > .nx-splitter-bar").First).ToBeHiddenAsync();
        var nav = (await page.Locator("#collapse-nav").BoundingBoxAsync())!;
        var main = (await page.Locator("#collapse-main").BoundingBoxAsync())!;
        Assert.That(main.Y, Is.GreaterThan(nav.Y));
        Assert.That(main.Width, Is.EqualTo(nav.Width).Within(1));

        var header = page.Locator("#collapse-nav .nx-splitter-pane-header");
        Assert.That((await header.BoundingBoxAsync())!.Height, Is.GreaterThanOrEqualTo(44));
        await header.TapAsync();
        await Expect(page.Locator("#collapse-state")).ToContainTextAsync("Navigation: open");
        await page.Locator("#toggle-nav").TapAsync();
        await Expect(page.Locator("#collapse-state")).ToContainTextAsync("Navigation: collapsed");
    }
}
