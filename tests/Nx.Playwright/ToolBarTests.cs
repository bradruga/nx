using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxToolBar: overflow measuring and the roving tab stop need a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ToolBarTests : PageTest
{
    private readonly string _baseUrl;

    public ToolBarTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.GotoAsync($"{_baseUrl}/toolbar/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Bar => Page.Locator("#overview-toolbar");
    private ILocator More => Bar.Locator(".nx-toolbar-more button");

    [Test]
    public async Task Wide_EverythingFits_NoMore()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("overview");

        await Expect(Page.Locator("#tb-schedule")).ToBeVisibleAsync();
        await Expect(More).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Narrow_OverflowsIntoMore_MenuClicksTheButton()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToPage("overview");

        await Expect(More).ToBeVisibleAsync();
        await Expect(Page.Locator("#tb-pdf")).ToBeHiddenAsync();
        await Expect(Page.Locator("#tb-new")).ToBeVisibleAsync();
        var bar = (await Bar.BoundingBoxAsync())!;
        var more = (await More.BoundingBoxAsync())!;
        Assert.That(more.X + more.Width, Is.LessThanOrEqualTo(bar.X + bar.Width));
        Assert.That(await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth"), Is.LessThanOrEqualTo(0));

        await More.ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Export PDF" }).ClickAsync();
        await Expect(Page.Locator("#overview-log")).ToHaveTextAsync("Exported PDF");
    }

    [Test]
    public async Task Resizing_MovesButtonsBackOut()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToPage("overview");
        await Expect(More).ToBeVisibleAsync();

        await Page.SetViewportSizeAsync(1280, 900);

        await Expect(More).ToHaveCountAsync(0);
        await Expect(Page.Locator("#tb-pdf")).ToBeVisibleAsync();
    }

    [Test]
    public async Task OneTabStop_ArrowsMove_SkipDisabled()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("overview");

        await Page.Locator("#tb-new").FocusAsync();
        await Expect(Page.Locator("#tb-new")).ToHaveAttributeAsync("tabindex", "0");
        await Expect(Page.Locator("#tb-edit")).ToHaveAttributeAsync("tabindex", "-1");

        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Page.Locator("#tb-edit")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Page.Locator("#tb-finalize")).ToBeFocusedAsync();     // Save is disabled until editing
        await Page.Keyboard.PressAsync("End");
        await Expect(Page.Locator("#tb-schedule")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Page.Locator("#tb-new")).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("Shift+Tab");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#tb-new")).ToBeFocusedAsync();
    }

    [Test]
    public async Task TextChange_Remeasures()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("overview");

        await Page.Locator("#tb-edit").ClickAsync();
        await Expect(Page.Locator("#tb-edit")).ToHaveTextAsync("Cancel");
        await Expect(Page.Locator("#tb-save")).ToBeEnabledAsync();
        await Page.Locator("#tb-finalize").ClickAsync();

        await Expect(Page.Locator("#tb-edit")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-log")).ToHaveTextAsync("Finalized");
    }

    [Test]
    public async Task WrapAndScroll_Modes()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("examples");

        var wrap = Page.Locator("#overflow-wrap");
        var heights = await wrap.Locator("[data-nx-toolbar-item]").EvaluateAllAsync<double[]>("es => es.map(e => e.getBoundingClientRect().top)");
        Assert.That(heights.Distinct().Count(), Is.GreaterThan(1), "wrapped onto more lines");
        await Expect(wrap.Locator(".nx-toolbar-more")).ToHaveCountAsync(0);

        var scroll = Page.Locator("#overflow-scroll");
        Assert.That(await scroll.EvaluateAsync<bool>("e => e.scrollWidth > e.clientWidth"), Is.True);
        await Expect(Page.Locator("#overflow-menu .nx-toolbar-more")).ToBeVisibleAsync();
    }
}
