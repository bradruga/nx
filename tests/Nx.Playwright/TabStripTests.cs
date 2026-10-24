using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class TabStripTests : PageTest
{
    private readonly string _baseUrl;

    public TabStripTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Tab(string id) => Page.Locator($"#{id}");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/tabstrip/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Click_ActivatesTabAndPanel()
    {
        await GoToPage("overview");

        await Tab("overview-tabs-tab-pours").ClickAsync();

        await Expect(Tab("overview-tabs-tab-pours")).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Page.Locator("#overview-tabs-panel-pours")).ToBeVisibleAsync();
        await Expect(Page.Locator("#overview-tab-value")).ToHaveTextAsync("ActiveTab: pours");
    }

    [Test]
    public async Task Keyboard_OneTabStop_ArrowsActivate_TabEntersPanel()
    {
        await GoToPage("overview");

        await Tab("overview-tabs-tab-details").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Tab("overview-tabs-tab-pours")).ToBeFocusedAsync();
        await Expect(Page.Locator("#overview-tab-value")).ToHaveTextAsync("ActiveTab: pours");

        await Page.Keyboard.PressAsync("End");
        await Expect(Page.Locator("#overview-tab-value")).ToHaveTextAsync("ActiveTab: drawings");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Page.Locator("#overview-tab-value")).ToHaveTextAsync("ActiveTab: details");

        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#overview-tabs-panel-details")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Keyboard_FastArrows_LandOnTheLastTab()
    {
        await GoToPage();

        await Tab("icon-tabs-tab-lines").FocusAsync();
        // Lines → Shipping → Issues → (Billing is disabled) → Lines, faster than a server round trip.
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");

        await Expect(Tab("icon-tabs-tab-lines")).ToBeFocusedAsync();
        await Expect(Page.Locator("#icon-tab-value")).ToHaveTextAsync("ActiveTab: lines");
    }

    [Test]
    public async Task Disabled_NotClickable()
    {
        await GoToPage();

        await Tab("icon-tabs-tab-billing").ClickAsync(new() { Force = true });

        await Expect(Page.Locator("#icon-tab-value")).ToHaveTextAsync("ActiveTab: lines");
        await Expect(Tab("icon-tabs-tab-billing")).ToHaveAttributeAsync("aria-disabled", "true");
    }

    [Test]
    public async Task Closable_ButtonDeleteAndAdd()
    {
        await GoToPage();

        await Tab("closable-tabs-tab-p-1042").Locator(".nx-tabstrip-close").ClickAsync();
        await Expect(Page.Locator("#closable-value")).ToHaveTextAsync("ActiveTab: P-1043 · 2 open");
        await Expect(Tab("closable-tabs-tab-p-1042")).ToHaveCountAsync(0);

        await Page.Locator("#add-tab").ClickAsync();
        await Expect(Page.Locator("#closable-value")).ToHaveTextAsync("ActiveTab: P-1045 · 3 open");

        await Tab("closable-tabs-tab-p-1045").FocusAsync();
        await Page.Keyboard.PressAsync("Delete");
        await Expect(Page.Locator("#closable-value")).ToHaveTextAsync("ActiveTab: P-1044 · 2 open");
        await Expect(Page.Locator("#closable-log")).ToHaveTextAsync("Closed P-1045");
    }

    [Test]
    public async Task Overflow_ArrowsScroll_ActiveKeptInView()
    {
        await GoToPage();

        var header = Page.Locator("#overflow-tabs .nx-tabstrip-header");
        await Expect(header).ToHaveAttributeAsync("data-overflow", "");
        await Expect(header.Locator(".nx-tabstrip-scroll-end")).ToBeVisibleAsync();
        Assert.That(await header.EvaluateAsync<bool>("h => h.hasAttribute('data-can-scroll-start')"), Is.False);

        await header.Locator(".nx-tabstrip-scroll-end").ClickAsync();
        await Page.WaitForFunctionAsync("() => document.querySelector('#overflow-tabs .nx-tabstrip-header').hasAttribute('data-can-scroll-start')");

        await Tab("overflow-tabs-tab-january").FocusAsync();
        await Page.Keyboard.PressAsync("End");
        await Expect(Page.Locator("#overflow-value")).ToHaveTextAsync("ActiveTab: December");
        await Expect(Tab("overflow-tabs-tab-december")).ToBeInViewportAsync(new() { Ratio = 1 });
        var list = (await Page.Locator("#overflow-tabs .nx-tabstrip-list").BoundingBoxAsync())!;
        var december = (await Tab("overflow-tabs-tab-december").BoundingBoxAsync())!;
        Assert.That(december.X + december.Width, Is.LessThanOrEqualTo(list.X + list.Width + 1));
    }

    [Test]
    public async Task Position_LeftIsVertical_UpDownMove()
    {
        await GoToPage();

        await Page.Locator("#position-picker").GetByText("Left").ClickAsync();
        await Expect(Page.Locator("#position-tabs [role=tablist]")).ToHaveAttributeAsync("aria-orientation", "vertical");

        var casting = (await Tab("position-tabs-tab-casting").BoundingBoxAsync())!;
        var yard = (await Tab("position-tabs-tab-yard").BoundingBoxAsync())!;
        Assert.That(yard.Y, Is.GreaterThan(casting.Y));
        var panel = (await Page.Locator("#position-tabs [role=tabpanel]").BoundingBoxAsync())!;
        Assert.That(panel.X, Is.GreaterThan(casting.X + casting.Width - 1));

        await Tab("position-tabs-tab-casting").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Tab("position-tabs-tab-yard")).ToBeFocusedAsync();
        await Expect(Tab("position-tabs-tab-yard")).ToHaveAttributeAsync("aria-selected", "true");
    }

    [Test]
    public async Task Position_Bottom_TabsUnderPanel()
    {
        await GoToPage();

        await Page.Locator("#position-picker").GetByText("Bottom").ClickAsync();
        await Expect(Page.Locator("#position-tabs")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-tabstrip-bottom"));

        var tab = (await Tab("position-tabs-tab-casting").BoundingBoxAsync())!;
        var panel = (await Page.Locator("#position-tabs [role=tabpanel]").BoundingBoxAsync())!;
        Assert.That(tab.Y, Is.GreaterThan(panel.Y));
    }

    [Test]
    public async Task Lazy_Rebuilds_KeepAlive_Keeps()
    {
        await GoToPage();

        await Page.Locator("#lazy-input").FillAsync("lazy text");
        await Page.Locator("#alive-input").FillAsync("alive text");
        await Tab("lazy-tabs-tab-two").ClickAsync();
        await Tab("alive-tabs-tab-two").ClickAsync();
        await Tab("lazy-tabs-tab-one").ClickAsync();
        await Tab("alive-tabs-tab-one").ClickAsync();

        await Expect(Page.Locator("#lazy-input")).ToHaveValueAsync("");
        await Expect(Page.Locator("#alive-input")).ToHaveValueAsync("alive text");
    }

    [Test]
    public async Task FocusRing_KeyboardOnly()
    {
        await GoToPage("overview");

        await Tab("overview-tabs-tab-pours").ClickAsync();
        await Expect(Tab("overview-tabs-tab-pours")).ToHaveCSSAsync("outline-style", "none");

        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Tab("overview-tabs-tab-drawings")).ToBeFocusedAsync();
        await Expect(Tab("overview-tabs-tab-drawings")).ToHaveCSSAsync("outline-style", "solid");
    }

    [Test]
    public async Task Playground_CountClosableAndPosition()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-count").SelectOptionAsync("12");
        await Expect(Page.Locator("#playground-tabs [role=tab]")).ToHaveCountAsync(12);
        await Page.Locator("#pg-closable").CheckAsync();
        await Tab("playground-tabs-tab-tab-1").Locator(".nx-tabstrip-close").ClickAsync();
        await Expect(Page.Locator("#playground-tabs [role=tab]")).ToHaveCountAsync(11);
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("ActiveTab: Tab 2");

        await Page.Locator("#pg-position").SelectOptionAsync("Right");
        await Expect(Page.Locator("#playground-tabs")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-tabstrip-right"));
    }

    [Test]
    public async Task Phone_SideTabsMoveToTop_44pxTargets_NoPageScroll()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/tabstrip/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
        Assert.That((await page.Locator("#icon-tabs-tab-lines").BoundingBoxAsync())!.Height, Is.GreaterThanOrEqualTo(44));

        await page.Locator("#position-picker").GetByText("Left").TapAsync();
        await Expect(page.Locator("#position-tabs")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-tabstrip-left"));
        var casting = (await page.Locator("#position-tabs-tab-casting").BoundingBoxAsync())!;
        var yard = (await page.Locator("#position-tabs-tab-yard").BoundingBoxAsync())!;
        Assert.That(yard.Y, Is.EqualTo(casting.Y).Within(1));
        Assert.That(yard.X, Is.GreaterThan(casting.X));
    }
}
