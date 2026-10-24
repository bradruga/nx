using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxColorPicker and NxNotification.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ColorPickerNotificationTests : PageTest
{
    private readonly string _baseUrl;

    public ColorPickerNotificationTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private Task<string?> FocusedLabelAsync() =>
        Page.EvaluateAsync<string?>("() => document.activeElement?.getAttribute('aria-label')");

    /// <summary>Waits for a message's slide-in to end; measured mid-animation it is up to 24 px off its place.</summary>
    private static Task SettledAsync(ILocator toast) =>
        toast.EvaluateAsync("e => Promise.all(e.getAnimations().map(a => a.finished))");

    // ── NxColorPicker ─────────────────────────────────────────────────────────

    [Test]
    public async Task Color_OpensUnderButton_FocusOnSelected()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("/colorpicker/overview");

        var button = Page.Locator("#overview-color");
        await button.EvaluateAsync("el => window.scrollBy(0, el.getBoundingClientRect().top - 150)");
        await button.ClickAsync();
        var popup = Page.Locator(".nx-colorpicker-popup");
        await Expect(popup).ToBeVisibleAsync();
        await Expect(button).ToHaveAttributeAsync("aria-expanded", "true");

        var b = (await button.BoundingBoxAsync())!;
        var p = (await popup.BoundingBoxAsync())!;
        Assert.That(p.Y, Is.EqualTo(b.Y + b.Height + 4).Within(2));
        await Expect(Page.Locator(".nx-colorpicker-cell[aria-selected=true]")).ToBeFocusedAsync();
        Assert.That(await FocusedLabelAsync(), Is.EqualTo("#4371c4"));
    }

    [Test]
    public async Task Color_ArrowsMoveByCellAndRow_EnterPicks_FocusReturns()
    {
        await GoToPage("/colorpicker/overview");

        var button = Page.Locator("#overview-color");
        await button.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Page.Locator(".nx-colorpicker-cell[aria-selected=true]")).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("ArrowRight");
        Assert.That(await FocusedLabelAsync(), Is.EqualTo("#ed7e32"));
        await Page.Keyboard.PressAsync("ArrowDown");
        Assert.That(await FocusedLabelAsync(), Is.EqualTo("#fce5d4"));
        await Page.Keyboard.PressAsync("Home");
        Assert.That(await FocusedLabelAsync(), Is.EqualTo("#ffffff"));
        await Page.Keyboard.PressAsync("End");
        Assert.That(await FocusedLabelAsync(), Is.EqualTo("#7030a0"));

        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: #7030a0");
        await Expect(Page.Locator(".nx-colorpicker-popup")).ToHaveCountAsync(0);
        await Expect(button).ToBeFocusedAsync();
        await Expect(button).ToHaveAttributeAsync("aria-label", "Crew color: #7030a0");
    }

    [Test]
    public async Task Color_EscapeClosesWithoutPicking_OutsideClickCloses()
    {
        await GoToPage("/colorpicker/overview");

        var button = Page.Locator("#overview-color");
        await button.ClickAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(".nx-colorpicker-popup")).ToHaveCountAsync(0);
        await Expect(button).ToBeFocusedAsync();
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: #4371c4");

        await button.ClickAsync();
        await Expect(Page.Locator(".nx-colorpicker-popup")).ToBeVisibleAsync();
        await Page.Locator("h1").ClickAsync();
        await Expect(Page.Locator(".nx-colorpicker-popup")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Color_TabOutCloses()
    {
        await GoToPage("/colorpicker/overview");

        await Page.Locator("#overview-color").ClickAsync();
        await Expect(Page.Locator(".nx-colorpicker-cell[aria-selected=true]")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator(".nx-colorpicker-popup")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Color_ClickPicks_UsedByThePage()
    {
        await GoToPage("/colorpicker/examples");

        var fill = Page.Locator("#fill-color");
        await fill.ScrollIntoViewIfNeededAsync();
        await fill.ClickAsync();
        await Page.Locator(".nx-colorpicker-cell[aria-label='#ffc000']").ClickAsync();
        // The page repaints a round trip after the pick: a retrying assertion, not a single read.
        await Expect(Page.Locator("#schedule-item")).ToHaveCSSAsync("background-color", "rgb(255, 192, 0)");
        await Expect(Page.Locator(".nx-colorpicker:has(#fill-color) .nx-colorpicker-value")).ToHaveTextAsync("#ffc000");
    }

    [Test]
    public async Task Color_OwnPaletteColumns_NoColor()
    {
        await GoToPage("/colorpicker/examples");

        await Page.Locator("#basic-palette").ClickAsync();
        var cells = Page.Locator(".nx-colorpicker-cell");
        await Expect(cells).ToHaveCountAsync(12);
        var first = (await cells.Nth(0).BoundingBoxAsync())!;
        var seventh = (await cells.Nth(6).BoundingBoxAsync())!;
        Assert.That(seventh.Y, Is.GreaterThan(first.Y + first.Height - 1), "6 columns: the 7th swatch starts a row");
        Assert.That(seventh.X, Is.EqualTo(first.X).Within(0.5));
        await Page.Keyboard.PressAsync("Escape");

        var clear = Page.Locator("#clear-color");
        await clear.ScrollIntoViewIfNeededAsync();
        await clear.ClickAsync();
        await Page.Locator(".nx-colorpicker-clear").ClickAsync();
        await Expect(Page.Locator("#clear-value")).ToHaveTextAsync("Value: null");
        await Expect(Page.Locator(".nx-colorpicker:has(#clear-color) .nx-colorpicker-swatch")).ToHaveClassAsync(new Regex("nx-colorpicker-swatch-empty"));
    }

    [Test]
    public async Task Color_DisabledDoesNotOpen_ValidationRequired()
    {
        await GoToPage("/colorpicker/examples");

        await Expect(Page.Locator("#disabled-color")).ToBeDisabledAsync();

        await Page.Locator("#color-submit").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#color-submit").ClickAsync();
        await Expect(Page.Locator("#color-form .nx-validation-message")).ToHaveTextAsync("Pick a color for the driver.");
        await Expect(Page.Locator("#load-color")).ToHaveAttributeAsync("aria-invalid", "true");

        await Page.Locator("#load-color").ClickAsync();
        await Page.Locator(".nx-colorpicker-cell[aria-label='#00b050']").ClickAsync();
        await Page.Locator("#color-submit").ClickAsync();
        await Expect(Page.Locator("#color-result")).ToHaveTextAsync("Saved with #00b050");
    }

    [Test]
    public async Task Color_Phone_PaletteFitsAndTargetsAreBigger()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/colorpicker/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        await page.Locator("#overview-color").TapAsync();
        var popup = (await page.Locator(".nx-colorpicker-popup").BoundingBoxAsync())!;
        Assert.That(popup.X, Is.GreaterThanOrEqualTo(0));
        Assert.That(popup.X + popup.Width, Is.LessThanOrEqualTo(390));
        var cell = (await page.Locator(".nx-colorpicker-cell").First.BoundingBoxAsync())!;
        Assert.That(cell.Width, Is.GreaterThanOrEqualTo(30));

        await page.Locator(".nx-colorpicker-cell[aria-label='#c00000']").TapAsync();
        await Expect(page.Locator("#overview-value")).ToHaveTextAsync("Value: #c00000");
        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
    }

    // ── NxNotification ────────────────────────────────────────────────────────

    [Test]
    public async Task Notify_ShowsBottomRight_StatusOrAlert()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("/notification/overview");

        await Page.Locator("#overview-saved").ClickAsync();
        var toast = Page.Locator("#overview-notification .nx-notification").First;
        await Expect(toast).ToBeVisibleAsync();
        await Expect(toast).ToHaveAttributeAsync("role", "status");
        await Expect(toast).ToContainTextAsync("Saved by Dana Ortiz");

        await SettledAsync(toast);
        var box = (await toast.BoundingBoxAsync())!;
        var area = await FixedArea.SizeAsync(Page);
        Assert.That(area.Width - (box.X + box.Width), Is.EqualTo(16).Within(2));
        Assert.That(area.Height - (box.Y + box.Height), Is.EqualTo(16).Within(2));

        await Page.Locator("#overview-error").ClickAsync();
        await Expect(Page.Locator("#overview-notification .nx-notification[role=alert]")).ToContainTextAsync("could not be saved");
    }

    [Test]
    public async Task Notify_CloseButton_AndTimeout()
    {
        await GoToPage("/notification/examples");

        await Page.Locator("#timing-sticky").ClickAsync();
        var sticky = Page.Locator("#examples-notification .nx-notification", new() { HasText = "Stays until" });
        await Expect(sticky).ToBeVisibleAsync();
        await sticky.Locator(".nx-notification-close").ClickAsync();
        await Expect(sticky).ToHaveCountAsync(0);

        await Page.Locator("#timing-short").ClickAsync();
        var quick = Page.Locator("#examples-notification .nx-notification", new() { HasText = "Gone in a moment" });
        await Expect(quick).ToBeVisibleAsync();
        await Expect(quick).ToHaveCountAsync(0, new() { Timeout = 5000 });
    }

    [Test]
    public async Task Notify_HoverHoldsItOpen()
    {
        await GoToPage("/notification/examples");

        await Page.Locator("#timing-short").ClickAsync();
        var quick = Page.Locator("#examples-notification .nx-notification", new() { HasText = "Gone in a moment" });
        await quick.HoverAsync();
        await Page.WaitForTimeoutAsync(2500);
        await Expect(quick).ToBeVisibleAsync();

        await Page.Mouse.MoveAsync(10, 10);
        await Expect(quick).ToHaveCountAsync(0, new() { Timeout = 5000 });
    }

    [Test]
    public async Task Notify_NewestNearestTheEdge_MaxVisible()
    {
        await GoToPage("/notification/examples");

        await Page.Locator("#many-burst").ClickAsync();
        var shown = Page.Locator("#examples-notification .nx-notification:not(.nx-notification-leaving)");
        await Expect(shown).ToHaveCountAsync(5);
        await Expect(shown.Last).ToContainTextAsync("Message 8");
        await Expect(shown.First).ToContainTextAsync("Message 4");
        await Page.Locator("#timing-hide-all").ClickAsync();
        await Expect(Page.Locator("#examples-notification .nx-notification")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Notify_TopLeftPosition_NewestFirst()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("/notification/examples");

        await Page.Locator("#position-Top-Left").ClickAsync();
        var toast = Page.Locator("#positioned-notification .nx-notification").First;
        await Expect(toast).ToContainTextAsync("Top Left");
        await SettledAsync(toast);
        var box = (await toast.BoundingBoxAsync())!;
        Assert.That(box.X, Is.EqualTo(16).Within(2));
        Assert.That(box.Y, Is.EqualTo(16).Within(2));
    }

    [Test]
    public async Task Notify_EmptyStackTakesNoClicks()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("/notification/overview");

        var events = await Page.Locator("#overview-notification").EvaluateAsync<string>("s => getComputedStyle(s).pointerEvents");
        Assert.That(events, Is.EqualTo("none"));
    }

    [Test]
    public async Task Notify_Phone_FullWidth()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/notification/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        await page.Locator("#overview-saved").TapAsync();
        var toast = page.Locator("#overview-notification .nx-notification").First;
        await Expect(toast).ToBeVisibleAsync();
        await SettledAsync(toast);
        var box = (await toast.BoundingBoxAsync())!;
        Assert.That(box.X, Is.EqualTo(16).Within(2));
        Assert.That(box.Width, Is.EqualTo(390 - 32).Within(2));
        var close = (await toast.Locator(".nx-notification-close").BoundingBoxAsync())!;
        Assert.That(close.Height, Is.GreaterThanOrEqualTo(40));
    }

    [Test]
    public async Task Notify_Playground_TypeAndNoIcon()
    {
        await GoToPage("/notification/playground");

        await Page.Locator("#pg-type").SelectOptionAsync("Warning");
        await Page.Locator("#pg-show-icon").UncheckAsync();
        await Page.Locator("#playground-show").ClickAsync();
        var toast = Page.Locator("#playground-notification .nx-notification").First;
        await Expect(toast).ToHaveClassAsync(new Regex("nx-notification-warning"));
        await Expect(toast.Locator(".nx-notification-icon")).ToHaveCountAsync(0);
    }
}
