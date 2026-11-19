using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>The demo site's search (top bar, Ctrl+K, /), its Recent and Pinned lists, the pin star and the 404 page.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class DemoSearchTests : PageTest
{
    private readonly string _baseUrl;

    public DemoSearchTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToAsync(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Dialog => Page.Locator("#site-search");
    private ILocator Input => Page.Locator("#site-search-input");
    private ILocator Button => Page.Locator("#site-search-button");
    private ILocator Options => Page.Locator("#site-search .nx-combobox-popup [role=option]");

    private Task<int> HorizontalOverflowAsync() =>
        Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

    // ── Opening and closing ───────────────────────────────────────────────────

    [Test]
    public async Task CtrlK_OpensTheSearch_WithFocusInTheField()
    {
        await GoToAsync("/button/overview");

        await Page.Keyboard.PressAsync("Control+k");

        await Expect(Dialog).ToBeVisibleAsync();
        await Expect(Input).ToBeFocusedAsync();
    }

    [Test]
    public async Task Slash_Opens_ButNotWhileTypingInAField()
    {
        await GoToAsync("/textbox/examples");

        var field = Page.Locator(".main-content input[type=text]").First;
        await field.FocusAsync();
        await Page.Keyboard.PressAsync("/");
        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(field).ToHaveValueAsync(new Regex("/$"));

        await field.EvaluateAsync("el => el.blur()");
        await Page.Keyboard.PressAsync("/");
        await Expect(Dialog).ToBeVisibleAsync();
        await Expect(Input).ToBeFocusedAsync();
        await Expect(Input).ToHaveValueAsync("");
    }

    [Test]
    public async Task Escape_Closes_AndFocusReturnsToTheButton()
    {
        await GoToAsync("/");

        await Button.ClickAsync();
        await Expect(Input).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Button).ToBeFocusedAsync();
    }

    [Test]
    public async Task Escape_ClosesInOnePress_WhileResultsAreOpen()
    {
        await GoToAsync("/");

        await Button.ClickAsync();
        await Input.PressSequentiallyAsync("combo");
        await Expect(Options.First).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Dialog).ToHaveCountAsync(0);
    }

    // ── Finding things ────────────────────────────────────────────────────────

    [Test]
    public async Task TelerikName_FindsTheNxComponent_AndEnterOpensIt()
    {
        await GoToAsync("/");

        await Button.ClickAsync();
        await Input.PressSequentiallyAsync("dropdownlist");

        await Expect(Options.First).ToContainTextAsync("NxDropDown");
        await Expect(Options.First.Locator("mark")).ToHaveTextAsync("DropDownList");
        await Expect(Page.Locator("#site-search .nx-combobox-group").First).ToHaveTextAsync("Components");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page).ToHaveURLAsync(new Regex("/dropdown/overview$"));
        await Expect(Page.Locator("h1")).ToHaveTextAsync("NxDropDown");
        await Expect(Dialog).ToHaveCountAsync(0);
    }

    [Test]
    public async Task PartialName_IgnoresThePrefix_AndMarksTheMatch()
    {
        await GoToAsync("/");

        await Button.ClickAsync();
        await Input.PressSequentiallyAsync("combo");

        await Expect(Options.First).ToContainTextAsync("NxComboBox");
        await Expect(Options.First.Locator("mark")).ToHaveTextAsync("Combo");
        await Expect(Options.First).ToContainTextAsync("/combobox/overview");
    }

    [Test]
    public async Task ExampleSection_OpensItsPageWithTheHash()
    {
        await GoToAsync("/");

        await Button.ClickAsync();
        await Input.PressSequentiallyAsync("type-ahead");
        var example = Options.Filter(new() { HasText = "NxDropDown › Examples" }).First;
        await Expect(example).ToBeVisibleAsync();
        await example.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/dropdown/examples#type-ahead$"));
        await Expect(Page.Locator("h1")).ToHaveTextAsync("NxDropDown");
    }

    [Test]
    public async Task Parameter_LandsOnTheApiPage()
    {
        await GoToAsync("/");

        await Button.ClickAsync();
        await Input.PressSequentiallyAsync("NxComboBox.Placeholder");
        await Expect(Page.Locator("#site-search .nx-combobox-group").First).ToHaveTextAsync("Parameters");
        await Expect(Options.First).ToContainTextAsync("Placeholder");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page).ToHaveURLAsync(new Regex("/combobox/api(#.*)?$"));
        await Expect(Page.Locator(".doc-tab.active")).ToHaveTextAsync("API");
    }

    [Test]
    public async Task ArrowKeys_MoveThroughGroups()
    {
        await GoToAsync("/");

        await Button.ClickAsync();
        await Input.PressSequentiallyAsync("validation");
        await Expect(Options.First).ToBeVisibleAsync();
        var first = await Input.GetAttributeAsync("aria-activedescendant");
        await Page.Keyboard.PressAsync("ArrowDown");

        await Expect(Input).Not.ToHaveAttributeAsync("aria-activedescendant", first!);
        await Expect(Page.Locator("#site-search .nx-combobox-group")).ToHaveTextAsync(new[] { "Components", "Examples", "Parameters" });
    }

    // ── Recent and pinned ─────────────────────────────────────────────────────

    [Test]
    public async Task RecentAndPinned_PersistAcrossReloads()
    {
        await GoToAsync("/chart/overview");
        var pin = Page.Locator(".doc-pin");
        await Expect(pin).ToHaveAttributeAsync("aria-pressed", "false");
        await pin.ClickAsync();
        await Expect(pin).ToHaveAttributeAsync("aria-pressed", "true");

        await GoToAsync("/gantt/examples");
        await Page.ReloadAsync();
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        var pinned = Page.Locator("#site-nav a.nav-pinned");
        await Expect(pinned).ToHaveTextAsync(new[] { "NxChart" });

        await Button.ClickAsync();
        var start = Page.Locator(".site-search-start");
        await Expect(start.Locator(".site-search-start-label")).ToHaveTextAsync(new[] { "Pinned", "Recent" });
        await Expect(start.Locator(".site-search-start-list").Last.Locator(".site-search-start-name"))
            .ToHaveTextAsync(new[] { "NxGantt", "NxChart" });
        await Page.Keyboard.PressAsync("Escape");

        await GoToAsync("/chart/api");
        await Expect(pin).ToHaveAttributeAsync("aria-pressed", "true");
        await pin.ClickAsync();
        await Expect(pinned).ToHaveCountAsync(0);
        await Page.ReloadAsync();
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
        await Expect(pin).ToHaveAttributeAsync("aria-pressed", "false");
    }

    // ── 404 ───────────────────────────────────────────────────────────────────

    [Test]
    public async Task UnknownAddress_SuggestsTheClosestPages()
    {
        await GoToAsync("/dropdownlist");

        await Expect(Page.Locator("h1")).ToHaveTextAsync("Page not found");
        await Expect(Page.Locator(".doc-intro")).ToContainTextAsync("/dropdownlist");
        var first = Page.Locator(".notfound-match").First;
        await Expect(first).ToContainTextAsync("NxDropDown");
        await Expect(first).ToHaveAttributeAsync("href", "dropdown/overview");

        await Page.Locator("#notfound-search").ClickAsync();
        await Expect(Dialog).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");

        await first.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/dropdown/overview$"));
    }

    [Test]
    public async Task MistypedAddress_StillFindsTheComponent()
    {
        await GoToAsync("/components/datepicker-old");

        await Expect(Page.Locator(".notfound-match").First).ToContainTextAsync("NxDatePicker");
    }

    // ── Phone ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Phone_IconButton_44pxTargets_NoHorizontalScroll()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToAsync("/button/overview");

        var button = (await Button.BoundingBoxAsync())!;
        Assert.That(button.Width, Is.GreaterThanOrEqualTo(44));
        Assert.That(button.Height, Is.GreaterThanOrEqualTo(44));
        await Expect(Page.Locator(".site-search-button-text")).ToBeHiddenAsync();
        var pin = (await Page.Locator(".doc-pin").BoundingBoxAsync())!;
        Assert.That(pin.Height, Is.GreaterThanOrEqualTo(44));

        await Button.ClickAsync();
        await Input.PressSequentiallyAsync("date");
        await Expect(Options.First).ToBeVisibleAsync();

        var dialog = (await Dialog.BoundingBoxAsync())!;
        Assert.That(dialog.X, Is.GreaterThanOrEqualTo(0));
        Assert.That(dialog.X + dialog.Width, Is.LessThanOrEqualTo(390));
        var option = (await Options.First.BoundingBoxAsync())!;
        Assert.That(option.Height, Is.GreaterThanOrEqualTo(44));
        Assert.That(option.X + option.Width, Is.LessThanOrEqualTo(390));
        Assert.That(await HorizontalOverflowAsync(), Is.LessThanOrEqualTo(0));
    }
}
