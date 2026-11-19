using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>The demo's theme builder (/guides/theme) and the home page's category filter.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class DemoThemeHomeTests : PageTest
{
    private readonly string _baseUrl;

    public DemoThemeHomeTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private Task<int> HorizontalOverflowAsync() =>
        Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

    private ILocator PrimaryButton => Page.Locator("#theme-primary");
    private ILocator Css => Page.Locator("#theme-css");

    private async Task PickAccentAsync(string hex)
    {
        await Page.Locator("#theme-accent").ClickAsync();
        await Page.Locator("#theme-accent-tab-palette").ClickAsync();
        await Page.Locator($".nx-colorpicker-popup [aria-label='{hex}']").ClickAsync();
    }

    // ── Theme builder ─────────────────────────────────────────────────────────

    [Test]
    public async Task Theme_AccentRecolorsTheSample_AndTheCssShowsIt()
    {
        await GoToPage("/guides/theme");
        await Expect(PrimaryButton).ToHaveCSSAsync("background-color", "rgb(0, 120, 212)");
        await Expect(Css).ToContainTextAsync("Nothing differs");

        await PickAccentAsync("#7030a0");

        await Expect(PrimaryButton).ToHaveCSSAsync("background-color", "rgb(112, 48, 160)");
        await Expect(Css).ToContainTextAsync(new Regex(@"--nx-accent:\s+#7030a0;"));
        await Expect(Css).ToContainTextAsync("--nx-accent-hover:");
        // Derived tokens follow the accent set on the sample.
        var selected = Page.Locator("#theme-sample .nx-toggle[aria-checked=true]");
        await Expect(selected).ToHaveCSSAsync("color", "rgb(112, 48, 160)");
    }

    [Test]
    public async Task Theme_ContainerAccent_ReachesFocusRing_SelectedToggle_TabIndicator_PopupAndDialog()
    {
        await GoToPage("/guides/theme");
        const string purple = "rgb(112, 48, 160)";
        const string accentOf = "el => getComputedStyle(el).getPropertyValue('--nx-accent').trim().toLowerCase()";
        const string indicatorOf = "el => getComputedStyle(el, '::after').backgroundColor";
        var sample = Page.Locator("#theme-sample");
        var selected = sample.Locator(".nx-toggle[aria-checked=true]");
        var activeTab = sample.Locator(".nx-tabstrip-tab-active");

        await PickAccentAsync("#7030a0");
        await Expect(PrimaryButton).ToHaveCSSAsync("background-color", purple);

        // Only the base tokens are set on the sample; the derived ones are not restated anywhere.
        Assert.That(await sample.GetAttributeAsync("style"), Does.Not.Contain("--nx-toggle").And.Not.Contain("--nx-tab").And.Not.Contain("--nx-focus"));
        await Expect(selected).ToHaveCSSAsync("color", purple);
        await Expect(selected).ToHaveCSSAsync("border-top-color", purple);
        Assert.That(await activeTab.EvaluateAsync<string>(indicatorOf), Is.EqualTo(purple));

        await activeTab.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(activeTab).ToBeFocusedAsync();
        await Expect(activeTab).ToHaveCSSAsync("outline-color", purple);

        // The controls beside the sample keep the site's accent.
        var outside = Page.Locator("#theme-controls .nx-switch-track");
        Assert.That(await outside.EvaluateAsync<string>("el => getComputedStyle(el).outlineColor"), Is.Not.EqualTo(purple));

        // A popup and a dialog declared inside the container render there, so they take its tokens too.
        await sample.Locator(".nx-combobox:has(#theme-customer) .nx-combobox-toggle").ClickAsync();
        var popup = sample.Locator(".nx-combobox-popup");
        await Expect(popup).ToBeVisibleAsync();
        Assert.That(await popup.EvaluateAsync<string>(accentOf), Is.EqualTo("#7030a0"));
        await Expect(popup.Locator(".nx-combobox-item[aria-selected=true]")).ToHaveCSSAsync("color", purple);
        await Page.Keyboard.PressAsync("Escape");

        await Page.Locator("#theme-delete").ClickAsync();
        var dialog = sample.Locator("#theme-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        Assert.That(await dialog.EvaluateAsync<string>(accentOf), Is.EqualTo("#7030a0"));
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();

        // Dark on the container: the dark theme's own accent, and the derived tokens follow it.
        await Page.Locator("#theme-reset").ClickAsync();
        await Page.Locator("#theme-dark").ClickAsync();
        const string darkAccent = "rgb(76, 163, 255)";
        await Expect(PrimaryButton).ToHaveCSSAsync("background-color", darkAccent);
        await Expect(selected).ToHaveCSSAsync("color", darkAccent);
        await Expect(selected).ToHaveCSSAsync("border-top-color", darkAccent);
        Assert.That(await activeTab.EvaluateAsync<string>(indicatorOf), Is.EqualTo(darkAccent));
    }

    [Test]
    public async Task Theme_ShapeControls_ChangeTheSample_AndOnlyDiffsAreListed()
    {
        await GoToPage("/guides/theme");

        await Page.Locator("input#theme-height").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");

        await Expect(PrimaryButton).ToHaveCSSAsync("height", "36px");
        await Expect(Css).ToContainTextAsync("--nx-control-height-md: 36px;");
        await Expect(Css).Not.ToContainTextAsync("--nx-accent");
        await Expect(Css).Not.ToContainTextAsync("--nx-radius");
    }

    [Test]
    public async Task Theme_Reset_RestoresTheDefaults()
    {
        await GoToPage("/guides/theme");
        await PickAccentAsync("#c00000");
        await Expect(PrimaryButton).ToHaveCSSAsync("background-color", "rgb(192, 0, 0)");

        await Page.Locator("#theme-reset").ClickAsync();

        await Expect(PrimaryButton).ToHaveCSSAsync("background-color", "rgb(0, 120, 212)");
        await Expect(Css).ToContainTextAsync("Nothing differs");
        await Expect(Page.Locator("#theme-reset")).ToBeDisabledAsync();
    }

    [Test]
    public async Task Theme_OverridesStayInsideTheSample()
    {
        await GoToPage("/guides/theme");
        const string accentOf = "el => getComputedStyle(el).getPropertyValue('--nx-accent').trim().toLowerCase()";
        var header = Page.Locator(".top-nav");
        var controls = Page.Locator("#theme-controls");
        var headerBefore = await header.EvaluateAsync<string>(accentOf);
        var controlsBefore = await controls.EvaluateAsync<string>(accentOf);

        await PickAccentAsync("#7030a0");
        await Expect(PrimaryButton).ToHaveCSSAsync("background-color", "rgb(112, 48, 160)");

        Assert.That(await header.EvaluateAsync<string>(accentOf), Is.EqualTo(headerBefore));
        Assert.That(await controls.EvaluateAsync<string>(accentOf), Is.EqualTo(controlsBefore));
        Assert.That(await Page.EvaluateAsync<string>("() => getComputedStyle(document.documentElement).getPropertyValue('--nx-accent').trim().toLowerCase()"),
            Is.EqualTo(headerBefore));
    }

    [Test]
    public async Task Theme_DarkSample_UsesTheDarkTokens_AndTheCssGoesInADarkRule()
    {
        await GoToPage("/guides/theme");
        var sample = Page.Locator("#theme-sample");

        await Page.Locator("#theme-dark").ClickAsync();

        await Expect(sample).ToHaveClassAsync(new Regex(@"\bnx-theme-dark\b"));
        await Expect(sample).ToHaveCSSAsync("background-color", "rgb(22, 22, 26)");
        await Expect(PrimaryButton).ToHaveCSSAsync("background-color", "rgb(76, 163, 255)");

        await PickAccentAsync("#7030a0");
        await Expect(Css).ToContainTextAsync(".nx-theme-dark {");
        await Expect(Css).Not.ToContainTextAsync(":root");
    }

    [Test]
    public async Task Theme_Phone_NoHorizontalScroll()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToPage("/guides/theme");

        Assert.That(await HorizontalOverflowAsync(), Is.LessThanOrEqualTo(0));
    }

    // ── Home category filter ──────────────────────────────────────────────────

    private ILocator Filter(string name) =>
        Page.Locator("#home-filter [role=radio]").Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(name)}\\s*\\d*$") });

    [Test]
    public async Task Home_CategoryFilter_HidesOtherCategories_AndPutsItInTheUrl()
    {
        await GoToPage("/");
        await Expect(Filter("All")).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(Page.Locator("#card-button")).ToBeVisibleAsync();

        await Filter("Dates").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"\?category=Dates$"));
        await Expect(Page.Locator("h3.home-category")).ToHaveTextAsync(new[] { "Dates" });
        await Expect(Page.Locator("#card-datepicker")).ToBeVisibleAsync();
        await Expect(Page.Locator("#card-button")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#card-textbox")).ToHaveCountAsync(0);

        await Filter("All").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/$"));
        await Expect(Page.Locator("#card-button")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Home_CategoryInTheUrl_SurvivesAReload()
    {
        await GoToPage("/?category=Menus%20%26%20Overlays");

        await Expect(Filter("Menus & Overlays")).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(Page.Locator("h3.home-category")).ToHaveTextAsync(new[] { "Menus & Overlays" });

        await Page.ReloadAsync();
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
        await Expect(Filter("Menus & Overlays")).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(Page.Locator("#card-tooltip")).ToBeVisibleAsync();
        await Expect(Page.Locator("#card-button")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Home_Phone_FilterScrollsInsideItself_44pxTargets()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToPage("/?category=Inputs");

        Assert.That(await HorizontalOverflowAsync(), Is.LessThanOrEqualTo(0));
        var filter = Page.Locator("#home-filter");
        Assert.That(await filter.EvaluateAsync<bool>("el => el.scrollWidth > el.clientWidth"), Is.True, "the filter fits; nothing to check");
        Assert.That((await Filter("Inputs").BoundingBoxAsync())!.Height, Is.GreaterThanOrEqualTo(44));
    }
}
