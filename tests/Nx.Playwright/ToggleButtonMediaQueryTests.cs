using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxToggleButton and NxMediaQuery.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ToggleButtonMediaQueryTests : PageTest
{
    private readonly string _baseUrl;

    public ToggleButtonMediaQueryTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    // ── NxToggleButton ────────────────────────────────────────────────────────

    [Test]
    public async Task Toggle_ClickAndKeys_FlipAriaPressed()
    {
        await GoToPage("/togglebutton/overview");

        var button = Page.Locator("#overview-deleted");
        await Expect(button).ToHaveAttributeAsync("aria-pressed", "false");
        await button.ClickAsync();
        await Expect(button).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Showing 6 holidays, 2 deleted");

        await Page.Keyboard.PressAsync("Space");
        await Expect(button).ToHaveAttributeAsync("aria-pressed", "false");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(button).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(button).ToBeFocusedAsync();
    }

    [Test]
    public async Task Toggle_PressedLooksPressed()
    {
        await GoToPage("/togglebutton/examples");

        var on = Page.Locator("#icon-schedule");
        var off = Page.Locator("#icon-colors");
        await Expect(on).ToHaveAttributeAsync("aria-pressed", "true");
        var onBg = await on.EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor");
        var offBg = await off.EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor");
        Assert.That(onBg, Is.Not.EqualTo(offBg));

        await off.ClickAsync();
        await Expect(off).ToHaveCSSAsync("background-color", onBg);
    }

    [Test]
    public async Task Toggle_IconOnlyLabelled_PanelOpens_DisabledStays()
    {
        await GoToPage("/togglebutton/examples");

        await Expect(Page.Locator("#icon-feed")).ToHaveAttributeAsync("aria-label", "Activity feed");
        await Expect(Page.Locator("#panel-rail")).ToHaveCountAsync(0);
        await Page.Locator("#panel-toggle").ClickAsync();
        await Expect(Page.Locator("#panel-rail")).ToBeVisibleAsync();

        var disabled = Page.Locator("#disabled-on");
        await Expect(disabled).ToBeDisabledAsync();
        await Expect(disabled).ToHaveAttributeAsync("aria-pressed", "true");
    }

    [Test]
    public async Task Toggle_InAGroup_TakesTheGroupSize()
    {
        await GoToPage("/togglebutton/examples");

        var bold = Page.Locator("#toggle-bold");
        await Expect(bold).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-button-sm"));
        await Expect(bold).ToHaveAttributeAsync("aria-pressed", "true");
        await Page.Locator("#toggle-group button[aria-label=Italic]").ClickAsync();
        await Expect(Page.Locator("#toggle-group button[aria-pressed=true]")).ToHaveCountAsync(2);
    }

    // ── NxMediaQuery ──────────────────────────────────────────────────────────

    private ILocator Badge(string media) => Page.Locator($".demo-mq-badge[data-media='{media}'] strong");

    [Test]
    public async Task MediaQuery_FollowsTheWidth()
    {
        await Page.SetViewportSizeAsync(1280, 800);
        await GoToPage("/mediaquery/examples");

        await Expect(Badge("(min-width: 992px)")).ToHaveTextAsync("True");
        await Expect(Badge("(max-width: 767px)")).ToHaveTextAsync("False");
        await Expect(Page.Locator("#layout-table")).ToBeVisibleAsync();

        await Page.SetViewportSizeAsync(800, 800);
        await Expect(Badge("(min-width: 992px)")).ToHaveTextAsync("False");
        await Expect(Badge("(min-width: 768px)")).ToHaveTextAsync("True");

        await Page.SetViewportSizeAsync(390, 800);
        await Expect(Badge("(max-width: 767px)")).ToHaveTextAsync("True");
        await Expect(Page.Locator("#layout-cards")).ToBeVisibleAsync();
        await Expect(Page.Locator("#layout-table")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task MediaQuery_ColorScheme()
    {
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light });
        await GoToPage("/mediaquery/examples");
        await Expect(Page.Locator("#scheme-value strong")).ToHaveTextAsync("False");

        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await Expect(Page.Locator("#scheme-value strong")).ToHaveTextAsync("True");
    }
}
