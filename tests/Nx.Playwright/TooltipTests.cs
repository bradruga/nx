using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class TooltipTests : PageTest
{
    private readonly string _baseUrl;

    public TooltipTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Tip(string id) => Page.Locator($"#{id}");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/tooltip/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Hover_ShowsAfterDelay_LeaveHides_DescribedBy()
    {
        await GoToPage("overview");

        await Expect(Page.Locator("#overview-save")).ToHaveAttributeAsync("aria-describedby", "overview-tip-save");
        await Page.Locator("#overview-save").HoverAsync();
        await Expect(Tip("overview-tip-save")).ToBeVisibleAsync();
        await Expect(Tip("overview-tip-save")).ToHaveTextAsync("Save the pour and keep editing");

        await Page.Mouse.MoveAsync(5, 300);
        await Expect(Tip("overview-tip-save")).ToBeHiddenAsync();
    }

    [Test]
    public async Task Delay_NotShownBeforeIt()
    {
        await GoToPage();

        await Page.Locator("#trigger-slow").HoverAsync();
        await Page.WaitForTimeoutAsync(400);
        await Expect(Tip("tip-slow")).ToBeHiddenAsync();
        await Expect(Tip("tip-slow")).ToBeVisibleAsync(new() { Timeout = 3000 });
    }

    [Test]
    public async Task NeverBlocksThePointer()
    {
        await GoToPage();

        await Page.Locator("#place-top").HoverAsync();
        await Expect(Tip("tip-top")).ToBeVisibleAsync();
        Assert.That(await Tip("tip-top").EvaluateAsync<string>("t => getComputedStyle(t).pointerEvents"), Is.EqualTo("none"));
        var box = (await Tip("tip-top").BoundingBoxAsync())!;
        var hit = await Page.EvaluateAsync<string>($"() => document.elementFromPoint({box.X + box.Width / 2}, {box.Y + box.Height / 2})?.closest('.nx-tooltip')?.id ?? 'none'");
        Assert.That(hit, Is.EqualTo("none"));
    }

    [TestCase("top")]
    [TestCase("bottom")]
    [TestCase("left")]
    [TestCase("right")]
    public async Task Placement_SidesOfTheAnchor(string side)
    {
        await GoToPage();

        var anchor = Page.Locator($"#place-{side}");
        await anchor.HoverAsync();
        var tip = Tip($"tip-{side}");
        await Expect(tip).ToBeVisibleAsync();
        await Expect(tip).ToHaveAttributeAsync("data-placement", side);
        var a = (await anchor.BoundingBoxAsync())!;
        var t = (await tip.BoundingBoxAsync())!;
        switch (side)
        {
            case "top": Assert.That(t.Y + t.Height, Is.LessThanOrEqualTo(a.Y)); break;
            case "bottom": Assert.That(t.Y, Is.GreaterThanOrEqualTo(a.Y + a.Height)); break;
            case "left": Assert.That(t.X + t.Width, Is.LessThanOrEqualTo(a.X)); break;
            default: Assert.That(t.X, Is.GreaterThanOrEqualTo(a.X + a.Width)); break;
        }
    }

    [Test]
    public async Task Flip_WhenNoRoomAbove()
    {
        await GoToPage();

        // Put the Top button against the viewport's top edge: the tooltip must go below. The pointer event is
        // dispatched in place, since HoverAsync would first scroll the button out from under the sticky header.
        await Page.Locator("#place-top").EvaluateAsync("el => window.scrollBy(0, el.getBoundingClientRect().top - 2)");
        await Page.Locator("#place-top").DispatchEventAsync("pointerenter", new { pointerType = "mouse" });

        await Expect(Tip("tip-top")).ToHaveAttributeAsync("data-placement", "bottom");
    }

    [Test]
    public async Task Click_TogglesAndEscapeHides()
    {
        await GoToPage();

        await Page.Locator("#trigger-click").ClickAsync();
        await Expect(Tip("tip-click")).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Tip("tip-click")).ToBeHiddenAsync();
        await Page.Locator("#trigger-click").ClickAsync();
        await Page.Locator("#trigger-click").ClickAsync();
        await Expect(Tip("tip-click")).ToBeHiddenAsync();
    }

    [Test]
    public async Task Focus_KeyboardOnly()
    {
        await GoToPage();

        await Page.Locator("#trigger-focus").ClickAsync();
        await Page.WaitForTimeoutAsync(200);
        await Expect(Tip("tip-focus")).ToBeHiddenAsync();

        await Page.Locator("#trigger-click").FocusAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#trigger-focus")).ToBeFocusedAsync();
        await Expect(Tip("tip-focus")).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Tip("tip-focus")).ToBeHiddenAsync();
    }

    [Test]
    public async Task Target_BySelector()
    {
        await GoToPage();

        await Expect(Page.Locator("#target-badge")).ToHaveAttributeAsync("aria-describedby", "tip-target");
        await Page.Locator("#target-badge").HoverAsync();
        await Expect(Tip("tip-target")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Code_ShowHide_Disabled()
    {
        await GoToPage();

        await Page.Locator("#show-coded").ClickAsync();
        await Expect(Tip("tip-coded")).ToBeVisibleAsync();
        await Page.Locator("#hide-coded").ClickAsync();
        await Expect(Tip("tip-coded")).ToBeHiddenAsync();

        await Page.Locator("#trigger-disabled").HoverAsync();
        await Page.WaitForTimeoutAsync(300);
        await Expect(Tip("tip-disabled")).ToBeHiddenAsync();
        await Expect(Page.Locator("#trigger-disabled")).Not.ToHaveAttributeAsync("aria-describedby", "tip-disabled");
    }

    [Test]
    public async Task Rich_Content()
    {
        await GoToPage();

        await Page.Locator("#rich-anchor").HoverAsync();
        await Expect(Tip("tip-rich").Locator("strong")).ToHaveTextAsync("Pour P-1042");
    }

    [Test]
    public async Task Touch_LongPressShows_TapDoesNot()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/tooltip/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
        var anchor = page.Locator("#overview-delete");
        var box = (await anchor.BoundingBoxAsync())!;
        var x = box.X + box.Width / 2;
        var y = box.Y + box.Height / 2;

        await page.Touchscreen.TapAsync(x, y);
        await page.WaitForTimeoutAsync(700);
        await Expect(page.Locator("#overview-tip-delete")).ToBeHiddenAsync();

        // A long press: touchstart, rest, touchend.
        await anchor.DispatchEventAsync("touchstart", new { touches = Array.Empty<object>() });
        await page.WaitForTimeoutAsync(700);
        await Expect(page.Locator("#overview-tip-delete")).ToBeVisibleAsync();
        await anchor.DispatchEventAsync("touchend");
        await Expect(page.Locator("#overview-tip-delete")).ToBeHiddenAsync(new() { Timeout = 4000 });
    }
}
