using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxButtonGroup and NxProgressBar.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ButtonGroupProgressTests : PageTest
{
    private readonly string _baseUrl;

    public ButtonGroupProgressTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private static Task<string> Css(ILocator l, string property) =>
        l.EvaluateAsync<string>($"e => getComputedStyle(e).{property}");

    // ── NxButtonGroup ─────────────────────────────────────────────────────────

    [Test]
    public async Task Group_ButtonsJoined_OnlyOuterCornersRounded()
    {
        await GoToPage("/buttongroup/overview");

        var today = Page.Locator("#overview-today");
        var prev = Page.Locator("#overview-prev");
        var next = Page.Locator("#overview-next");
        var a = (await today.BoundingBoxAsync())!;
        var b = (await prev.BoundingBoxAsync())!;
        var c = (await next.BoundingBoxAsync())!;
        Assert.That(b.X, Is.EqualTo(a.X + a.Width - 1).Within(0.5), "borders overlap by 1 px");
        Assert.That(c.X, Is.EqualTo(b.X + b.Width - 1).Within(0.5));
        Assert.That(b.Y, Is.EqualTo(a.Y).Within(0.5));

        Assert.That(await Css(today, "borderTopRightRadius"), Is.EqualTo("0px"));
        Assert.That(await Css(today, "borderTopLeftRadius"), Is.Not.EqualTo("0px"));
        Assert.That(await Css(prev, "borderTopLeftRadius"), Is.EqualTo("0px"));
        Assert.That(await Css(prev, "borderTopRightRadius"), Is.EqualTo("0px"));
        Assert.That(await Css(next, "borderBottomRightRadius"), Is.Not.EqualTo("0px"));
        await Expect(Page.Locator("#overview-week")).ToHaveAttributeAsync("role", "group");
    }

    [Test]
    public async Task Group_ButtonsAct_EachIsATabStop()
    {
        await GoToPage("/buttongroup/overview");

        var before = await Page.Locator("#overview-week-value").TextContentAsync();
        await Page.Locator("#overview-next").ClickAsync();
        await Expect(Page.Locator("#overview-week-value")).Not.ToHaveTextAsync(before!);

        await Page.Locator("#overview-today").FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#overview-prev")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#overview-week-value")).ToHaveTextAsync(before!);
    }

    [Test]
    public async Task Group_SizeAndVariantFromTheGroup_OwnVariantWins()
    {
        await GoToPage("/buttongroup/examples");

        var buttons = Page.Locator("#variant-group .nx-button");
        await Expect(buttons.Nth(0)).ToHaveClassAsync(new Regex(@"\bnx-button-sm\b"));
        await Expect(buttons.Nth(0)).ToHaveClassAsync(new Regex(@"\bnx-button-secondary\b"));
        await Expect(Page.Locator("#variant-save")).ToHaveClassAsync(new Regex(@"\bnx-button-primary\b"));
        await Expect(Page.Locator("#variant-save")).ToHaveClassAsync(new Regex(@"\bnx-button-sm\b"));

        await Page.Locator("#variant-save").ClickAsync();
        await Expect(Page.Locator("#variant-log")).ToHaveTextAsync("Last: Save");
    }

    [Test]
    public async Task Group_MenuButtonJoins_AndOpens()
    {
        await GoToPage("/buttongroup/examples");

        var hold = (await Page.Locator("#menu-group .nx-button", new() { HasText = "Hold" }).BoundingBoxAsync())!;
        var more = Page.Locator("#menu-more");
        var box = (await more.BoundingBoxAsync())!;
        Assert.That(box.X, Is.EqualTo(hold.X + hold.Width - 1).Within(0.5));
        Assert.That(await Css(more, "borderTopLeftRadius"), Is.EqualTo("0px"));

        await more.ClickAsync();
        await Page.Locator("#menu-more-menu .nx-menu-item", new() { HasText = "Split pour" }).ClickAsync();
        await Expect(Page.Locator("#variant-log")).ToHaveTextAsync("Last: Split pour");
    }

    [Test]
    public async Task Group_FullWidthSharesEqually_DisabledTogglesAll()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("/buttongroup/examples");

        var group = (await Page.Locator("#full-group").BoundingBoxAsync())!;
        var widths = await Page.Locator("#full-group .nx-button").EvaluateAllAsync<double[]>("bs => bs.map(b => b.getBoundingClientRect().width)");
        Assert.That(widths.Max() - widths.Min(), Is.LessThan(2));
        Assert.That(widths.Sum() - 2, Is.EqualTo(group.Width).Within(2));

        await Expect(Page.Locator("#disabled-group .nx-button").First).ToBeDisabledAsync();
        await Page.Locator(".nx-checkbox:has(#disabled-toggle)").ClickAsync();
        await Expect(Page.Locator("#disabled-group .nx-button").First).ToBeEnabledAsync();
        await Expect(Page.Locator("#disabled-group .nx-button").Last).ToBeEnabledAsync();
    }

    [Test]
    public async Task Group_Playground_SizeReachesButtons()
    {
        await GoToPage("/buttongroup/playground");

        await Page.Locator("#pg-size").SelectOptionAsync("Large");
        await Expect(Page.Locator("#playground-group .nx-button").First).ToHaveClassAsync(new Regex(@"\bnx-button-lg\b"));
        await Page.Locator("#pg-disabled").CheckAsync();
        await Expect(Page.Locator("#playground-group .nx-button").First).ToBeDisabledAsync();
    }

    [Test]
    public async Task Group_Phone_FitsWithoutPageScroll()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/buttongroup/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        var full = (await page.Locator("#full-group").BoundingBoxAsync())!;
        Assert.That(full.X + full.Width, Is.LessThanOrEqualTo(390));
        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
    }

    // ── NxProgressBar ─────────────────────────────────────────────────────────

    [Test]
    public async Task Progress_FillMatchesValue_AriaValues()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("/progressbar/overview");

        var bar = Page.Locator("#overview-produced");
        await Expect(bar).ToHaveAttributeAsync("aria-valuenow", "68");
        await Expect(bar).ToHaveAttributeAsync("aria-valuetext", "68%");
        var track = (await bar.BoundingBoxAsync())!;
        var fill = (await bar.Locator(".nx-progressbar-fill").BoundingBoxAsync())!;
        Assert.That(fill.Width / track.Width, Is.EqualTo(0.68).Within(0.01));

        await Page.Locator("#overview-more").ClickAsync();
        await Expect(bar).ToHaveAttributeAsync("aria-valuenow", "78");
        await Expect(bar.Locator(".nx-progressbar-label").First).ToHaveTextAsync("78%");
    }

    [Test]
    public async Task Progress_LabelReadableOverFillAndTrack()
    {
        await GoToPage("/progressbar/examples");

        var bar = Page.Locator("#template-bar");
        var colors = await bar.Locator(".nx-progressbar-label").EvaluateAllAsync<string[]>("ls => ls.map(l => getComputedStyle(l).color)");
        Assert.That(colors[0], Is.Not.EqualTo(colors[1]), "the label over the fill has its own color");
        var clip = await bar.Locator(".nx-progressbar-label-on-fill").EvaluateAsync<string>("l => getComputedStyle(l).clipPath");
        Assert.That(clip, Does.StartWith("inset("));
    }

    [Test]
    public async Task Progress_CountsUpToComplete_TemplateChanges()
    {
        await GoToPage("/progressbar/examples");

        await Page.Locator("#count-start").ClickAsync();
        await Expect(Page.Locator("#count-value")).ToHaveTextAsync("8 of 8 chunks", new() { Timeout = 6000 });
        await Expect(Page.Locator("#count-bar")).ToHaveAttributeAsync("aria-valuenow", "8");
        await Expect(Page.Locator("#template-bar .nx-progressbar-label").First).ToHaveTextAsync("Processing transcript complete.");
    }

    [Test]
    public async Task Progress_ThinHasNoLabel_IndeterminateAnimates()
    {
        await GoToPage("/progressbar/examples");

        var thin = (await Page.Locator("#thin-bar").BoundingBoxAsync())!;
        Assert.That(thin.Height, Is.EqualTo(4).Within(0.5));
        await Expect(Page.Locator("#thin-bar .nx-progressbar-label").First).ToBeHiddenAsync();

        await Page.Locator("#thin-next").ClickAsync();
        await Expect(Page.Locator("#thin-bar")).ToHaveAttributeAsync("aria-valuenow", "3");

        var indeterminate = Page.Locator("#indeterminate-bar");
        Assert.That(await indeterminate.GetAttributeAsync("aria-valuenow"), Is.Null);
        var animation = await indeterminate.Locator(".nx-progressbar-fill").EvaluateAsync<string>("f => getComputedStyle(f).animationName");
        Assert.That(animation, Does.Contain("nx-progressbar-slide"));
    }

    [Test]
    public async Task Progress_Playground_MaxAndIndeterminate()
    {
        await GoToPage("/progressbar/playground");

        await Page.Locator("#pg-max").FillAsync("80");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#playground-bar")).ToHaveAttributeAsync("aria-valuetext", "50%");
        await Page.Locator("#pg-indeterminate").CheckAsync();
        await Expect(Page.Locator("#playground-bar")).ToHaveClassAsync(new Regex("nx-progressbar-indeterminate"));
    }
}
