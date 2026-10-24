using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ContextMenuTests : PageTest
{
    private readonly string _baseUrl;

    public ContextMenuTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Menu(string id) => Page.Locator($"#{id}");
    private ILocator RootItems(string id) => Page.Locator($"#{id} > ul > li > .nx-menu-item");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/contextmenu/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private Task<string?> FocusedTextAsync() =>
        Page.EvaluateAsync<string?>("() => document.activeElement?.querySelector('.nx-menu-text')?.textContent ?? null");

    [Test]
    public async Task RightClick_OpensAtPointer_FocusFirst_EnterPicks()
    {
        await GoToPage("overview");

        var card = Page.Locator("#overview-card");
        // Room below and to the right of the point, so the menu opens there without flipping.
        await card.EvaluateAsync("el => window.scrollBy(0, el.getBoundingClientRect().top - 120)");
        var box = (await card.BoundingBoxAsync())!;
        await card.ClickAsync(new() { Button = MouseButton.Right, Position = new() { X = 40, Y = 20 } });

        var list = Page.Locator("#overview-menu > ul");
        await Expect(list).ToBeVisibleAsync();
        var menu = (await list.BoundingBoxAsync())!;
        Assert.That(menu.X, Is.EqualTo(box.X + 40).Within(2));
        Assert.That(menu.Y, Is.EqualTo(box.Y + 20).Within(2));
        Assert.That(await FocusedTextAsync(), Is.EqualTo("Edit"));

        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Menu("overview-menu")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-picked")).ToHaveTextAsync("Picked: Duplicate");
    }

    [Test]
    public async Task Keyboard_SkipsDisabledAndSeparators_Wraps_Letters()
    {
        await GoToPage("overview");

        await Page.Locator("#overview-card").ClickAsync(new() { Button = MouseButton.Right });
        await Expect(Page.Locator("#overview-menu > ul")).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("End");
        Assert.That(await FocusedTextAsync(), Is.EqualTo("Delete"));
        await Page.Keyboard.PressAsync("ArrowUp");
        Assert.That(await FocusedTextAsync(), Is.EqualTo("Download PDF"));   // Archive is disabled
        await Page.Keyboard.PressAsync("End");
        await Page.Keyboard.PressAsync("ArrowDown");
        Assert.That(await FocusedTextAsync(), Is.EqualTo("Edit"));            // wraps
        await Page.Keyboard.PressAsync("d");
        Assert.That(await FocusedTextAsync(), Is.EqualTo("Duplicate"));
        await Page.Keyboard.PressAsync("d");
        Assert.That(await FocusedTextAsync(), Is.EqualTo("Download PDF"));
    }

    [Test]
    public async Task Escape_ClosesAndReturnsFocus()
    {
        await GoToPage("examples");

        await Page.Locator("#open-coded").ClickAsync();
        await Expect(Page.Locator("#coded-menu > ul")).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Menu("coded-menu")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#open-coded")).ToBeFocusedAsync();
    }

    [Test]
    public async Task OutsidePress_Closes()
    {
        await GoToPage("overview");

        await Page.Locator("#overview-card").ClickAsync(new() { Button = MouseButton.Right });
        await Expect(Page.Locator("#overview-menu > ul")).ToBeVisibleAsync();
        await Page.Mouse.ClickAsync(1200, 150);

        await Expect(Menu("overview-menu")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-picked")).ToHaveTextAsync("Picked: —");
    }

    [Test]
    public async Task Rows_ContextAndOpeningItems()
    {
        await GoToPage();

        await Page.Locator("#row-P-1043").ClickAsync(new() { Button = MouseButton.Right });
        await Expect(RootItems("rows-menu")).ToHaveCountAsync(5);
        await RootItems("rows-menu").Filter(new() { HasText = "Edit" }).ClickAsync();
        await Expect(Page.Locator("#rows-picked")).ToHaveTextAsync("Edit P-1043");

        await Page.Locator("#row-P-1044").ClickAsync(new() { Button = MouseButton.Right });
        await Expect(RootItems("rows-menu")).ToHaveCountAsync(1);
        await Expect(RootItems("rows-menu")).ToHaveTextAsync("View");
    }

    [Test]
    public async Task Submenu_KeyboardInAndOut_PickNested()
    {
        await GoToPage();

        await Page.Locator("#view-card").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#view-card").ClickAsync(new() { Button = MouseButton.Right });
        await Expect(Page.Locator("#view-menu > ul")).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        Assert.That(await FocusedTextAsync(), Is.EqualTo("Element"));
        await Expect(RootItems("view-menu").First).ToHaveAttributeAsync("aria-expanded", "true");
        await Page.Keyboard.PressAsync("ArrowLeft");
        Assert.That(await FocusedTextAsync(), Is.EqualTo("Columns"));
        await Expect(RootItems("view-menu").First).ToHaveAttributeAsync("aria-expanded", "false");

        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");          // Sort by → submenu
        await Page.Keyboard.PressAsync("End");            // More
        await Page.Keyboard.PressAsync("ArrowRight");     // → Status
        await Page.Keyboard.PressAsync("ArrowDown");      // Crew
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#view-card span")).ToContainTextAsync("Sort: Crew");
    }

    [Test]
    public async Task Submenu_HoverOpens_SitsBesideParent_CheckItem()
    {
        await GoToPage();

        await Page.Locator("#view-card").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#view-card").ClickAsync(new() { Button = MouseButton.Right });
        var parent = RootItems("view-menu").Filter(new() { HasText = "Columns" });
        await parent.HoverAsync();
        var sub = Page.Locator("#view-menu > ul > li > ul").First;
        await Expect(sub).ToBeVisibleAsync();
        var p = (await parent.BoundingBoxAsync())!;
        var s = (await sub.BoundingBoxAsync())!;
        Assert.That(s.X, Is.GreaterThanOrEqualTo(p.X + p.Width - 4));

        var crew = sub.Locator(".nx-menu-item", new() { HasText = "Crew" });
        await Expect(crew).ToHaveAttributeAsync("aria-checked", "false");
        await crew.ClickAsync();
        await Expect(Page.Locator("#view-card span")).ToContainTextAsync("Crew");
    }

    [Test]
    public async Task Flips_AtTheViewportEdge()
    {
        await GoToPage("overview");

        var vw = Page.ViewportSize!.Width;
        var vh = Page.ViewportSize!.Height;
        await Page.EvaluateAsync("() => window.scrollTo(0, 0)");
        // Open from code at the bottom-right corner: the menu must end up left of and above the point.
        await Page.EvaluateAsync($"() => document.querySelector('#overview-card').dispatchEvent(new MouseEvent('contextmenu', {{ bubbles: true, clientX: {vw - 10}, clientY: {vh - 10} }}))");
        var list = Page.Locator("#overview-menu > ul");
        await Expect(list).ToBeVisibleAsync();
        var box = (await list.BoundingBoxAsync())!;
        Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(vw - 10 + 1));
        Assert.That(box.Y + box.Height, Is.LessThanOrEqualTo(vh - 10 + 1));
    }

    [Test]
    public async Task Target_BySelector_AndCodeOpen()
    {
        await GoToPage();

        await Page.Locator("#target-chip").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#target-chip").ClickAsync(new() { Button = MouseButton.Right });
        await RootItems("target-menu").Filter(new() { HasText = "Track" }).ClickAsync();
        await Expect(Page.Locator("#target-picked")).ToHaveTextAsync("Picked: Track");

        await Page.Locator("#open-coded").ClickAsync();
        await RootItems("coded-menu").Filter(new() { HasText = "Settings" }).ClickAsync();
        await Expect(Page.Locator("#coded-picked")).ToHaveTextAsync("Picked: Settings");
    }

    [Test]
    public async Task Playground_DisabledLetsBrowserMenuThrough()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-disabled").CheckAsync();
        await Expect(Page.Locator("#playground-card span")).ToHaveTextAsync("Disabled: the browser menu shows");
        // The listener is removed after that render, a round trip later: retry until the right click is not cancelled.
        await Page.WaitForFunctionAsync(
            "() => { const e = new MouseEvent('contextmenu', { bubbles: true, cancelable: true }); document.querySelector('#playground-card').dispatchEvent(e); return !e.defaultPrevented; }");
    }

    [Test]
    public async Task Phone_LongPress_BottomSheet_DrillIn()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/contextmenu/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        var card = page.Locator("#view-card");
        await card.ScrollIntoViewIfNeededAsync();
        await card.DispatchEventAsync("touchstart", new { touches = new[] { new { identifier = 1, clientX = 100, clientY = 300 } } });
        await page.WaitForTimeoutAsync(800);
        await card.DispatchEventAsync("touchend");

        await Expect(page.Locator("#view-menu")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-menu-sheet"));
        var sheet = (await page.Locator("#view-menu > ul").BoundingBoxAsync())!;
        Assert.That(sheet.Width, Is.EqualTo(390).Within(1));
        Assert.That(sheet.Y + sheet.Height, Is.EqualTo(844).Within(1));
        var row = (await page.Locator("#view-menu > ul > li > .nx-menu-item").First.BoundingBoxAsync())!;
        Assert.That(row.Height, Is.GreaterThanOrEqualTo(44));

        await page.Locator("#view-menu .nx-menu-item", new() { HasText = "Sort by" }).TapAsync();
        await Expect(page.Locator("#view-menu .nx-menu-back", new() { HasText = "Sort by" })).ToBeVisibleAsync();
        await page.Locator("#view-menu .nx-menu-back", new() { HasText = "Sort by" }).TapAsync();
        await Expect(page.Locator("#view-menu .nx-menu-back", new() { HasText = "Sort by" })).ToBeHiddenAsync();

        await page.Locator("#view-menu .nx-menu-backdrop").TapAsync(new() { Position = new() { X = 20, Y = 20 } });
        await Expect(page.Locator("#view-menu")).ToHaveCountAsync(0);
    }
}
