using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxPopup: placing next to the anchor, flipping and outside presses need a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class PopupTests : PageTest
{
    private readonly string _baseUrl;

    public PopupTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await Page.GotoAsync($"{_baseUrl}/popup/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Opens_BelowTheAnchor_StartAligned()
    {
        await GoToPage("overview");
        await Page.Locator("#overview-anchor").ClickAsync();
        var popup = Page.Locator("#overview-popup");
        await Expect(popup).ToBeVisibleAsync();

        var a = (await Page.Locator("#overview-anchor").BoundingBoxAsync())!;
        var p = (await popup.BoundingBoxAsync())!;
        Assert.That(p.Y - (a.Y + a.Height), Is.EqualTo(4).Within(1));
        Assert.That(p.X - a.X, Is.EqualTo(0).Within(1));
        Assert.That(p.Width, Is.EqualTo(240).Within(1));
    }

    [Test]
    public async Task InsideKeeps_OutsideAndEscapeClose_AnchorToggles()
    {
        await GoToPage("overview");
        var popup = Page.Locator("#overview-popup");
        await Page.Locator("#overview-anchor").ClickAsync();
        await popup.GetByText("Stripped").ClickAsync();
        await Expect(popup).ToBeVisibleAsync();

        await Page.Mouse.ClickAsync(5, 300);
        await Expect(popup).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-state")).ToHaveTextAsync("Closed");

        await Page.Locator("#overview-anchor").ClickAsync();
        await Expect(popup).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(popup).ToHaveCountAsync(0);

        await Page.Locator("#overview-anchor").ClickAsync();
        await Expect(popup).ToBeVisibleAsync();
        await Page.Locator("#overview-anchor").ClickAsync();
        await Expect(popup).ToHaveCountAsync(0);
    }

    [Test]
    public async Task EndAligned_FilterMenu()
    {
        await GoToPage("examples");
        await Page.Locator("#mix-filter").ClickAsync();
        var popup = Page.Locator("#filter-popup");
        await Expect(popup).ToBeVisibleAsync();
        var a = (await Page.Locator("#mix-filter").BoundingBoxAsync())!;
        var p = (await popup.BoundingBoxAsync())!;
        Assert.That(p.X + p.Width - (a.X + a.Width), Is.EqualTo(0).Within(1));

        await Page.Locator("#filter-search").FillAsync("psi");
        await Expect(popup.Locator(".nx-checkbox")).ToHaveCountAsync(2);
    }

    [TestCase("bottom")]
    [TestCase("top")]
    [TestCase("right")]
    [TestCase("left")]
    public async Task Placement_EachSide(string side)
    {
        await GoToPage("examples");
        await Page.Locator("#place-target").ScrollIntoViewIfNeededAsync();
        await Page.Locator($"#place-{side}").ClickAsync();
        var popup = Page.Locator("#place-popup");
        await Expect(popup).ToHaveAttributeAsync("data-placement", side);

        var t = (await Page.Locator("#place-target").BoundingBoxAsync())!;
        var p = (await popup.BoundingBoxAsync())!;
        var gap = side switch
        {
            "bottom" => p.Y - (t.Y + t.Height),
            "top" => t.Y - (p.Y + p.Height),
            "right" => p.X - (t.X + t.Width),
            _ => t.X - (p.X + p.Width)
        };
        Assert.That(gap, Is.EqualTo(8).Within(1));
    }

    [Test]
    public async Task NoRoomBelow_FlipsAbove_AndScrolls()
    {
        await GoToPage("playground");
        await Page.Locator("#pg-lines").FillAsync("20");
        await Page.Locator("#pg-lines").PressAsync("Tab");
        await Page.Locator("#playground-anchor").EvaluateAsync("a => window.scrollBy(0, a.getBoundingClientRect().top - (innerHeight - 140))");
        await Page.Locator("#playground-anchor").ClickAsync();

        var popup = Page.Locator("#playground-popup");
        await Expect(popup).ToHaveAttributeAsync("data-placement", "top");
        Assert.That(await popup.EvaluateAsync<bool>("p => p.scrollHeight > p.clientHeight"), Is.True);
        var p = (await popup.BoundingBoxAsync())!;
        Assert.That(p.Y, Is.GreaterThanOrEqualTo(8));
    }
}
