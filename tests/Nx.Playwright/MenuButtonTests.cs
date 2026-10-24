using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxDropDownButton and NxSplitButton.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class MenuButtonTests : PageTest
{
    private readonly string _baseUrl;

    public MenuButtonTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private Task<string?> FocusedTextAsync() =>
        Page.EvaluateAsync<string?>("() => document.activeElement?.querySelector('.nx-menu-text')?.textContent ?? null");

    // ── NxDropDownButton ──────────────────────────────────────────────────────

    [Test]
    public async Task DropDown_ClickOpensBelow_PickCloses_FocusBack()
    {
        await GoToPage("/dropdownbutton/overview");

        var button = Page.Locator("#overview-export");
        await button.ClickAsync();
        await Expect(button).ToHaveAttributeAsync("aria-expanded", "true");
        var list = Page.Locator("#overview-export-menu > ul");
        await Expect(list).ToBeVisibleAsync();
        var b = (await button.BoundingBoxAsync())!;
        var m = (await list.BoundingBoxAsync())!;
        Assert.That(m.Y, Is.EqualTo(b.Y + b.Height + 4).Within(2));
        Assert.That(m.X, Is.EqualTo(b.X).Within(2));

        await list.Locator(".nx-menu-item", new() { HasText = "CSV" }).ClickAsync();
        await Expect(Page.Locator("#overview-picked")).ToHaveTextAsync("Picked: CSV");
        await Expect(button).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(button).ToBeFocusedAsync();
    }

    [Test]
    public async Task DropDown_Keyboard_EnterOpensAtFirst_EscapeBack()
    {
        await GoToPage("/dropdownbutton/overview");

        await Page.Locator("#overview-export").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#overview-export-menu > ul")).ToBeVisibleAsync();
        Assert.That(await FocusedTextAsync(), Is.EqualTo("Excel (.xlsx)"));
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator("#overview-export-menu")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-export")).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Page.Locator("#overview-export-menu > ul")).ToBeVisibleAsync();
    }

    [Test]
    public async Task DropDown_AlignEnd_RightEdges()
    {
        await GoToPage("/dropdownbutton/overview");

        var button = Page.Locator("#overview-kebab");
        await button.ClickAsync();
        var list = Page.Locator("#overview-kebab-menu > ul");
        await Expect(list).ToBeVisibleAsync();
        var b = (await button.BoundingBoxAsync())!;
        var m = (await list.BoundingBoxAsync())!;
        Assert.That(m.X + m.Width, Is.EqualTo(b.X + b.Width).Within(2));
    }

    [Test]
    public async Task DropDown_SubmenuCheckItem()
    {
        await GoToPage("/dropdownbutton/examples");

        await Page.Locator("#view-button").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#view-button").ClickAsync();
        await Page.Locator("#view-button-menu .nx-menu-item", new() { HasText = "Density" }).HoverAsync();
        await Page.Locator("#view-button-menu .nx-menu-item", new() { HasText = "Compact" }).ClickAsync();
        await Expect(Page.Locator("#view-state")).ToContainTextAsync("Density: Compact");

        await Page.Locator("#view-button").ClickAsync();
        await Page.Locator("#view-button-menu .nx-menu-item", new() { HasText = "Show totals" }).ClickAsync();
        await Expect(Page.Locator("#view-state")).ToContainTextAsync("Totals: off");
    }

    [Test]
    public async Task DropDown_Disabled()
    {
        await GoToPage("/dropdownbutton/examples");

        await Expect(Page.Locator("#disabled-button")).ToBeDisabledAsync();
    }

    // ── NxSplitButton ─────────────────────────────────────────────────────────

    [Test]
    public async Task Split_MainRuns_ArrowOpensAlignedRight()
    {
        await GoToPage("/splitbutton/overview");

        await Page.Locator("#overview-save").ClickAsync();
        await Expect(Page.Locator("#overview-result")).ToHaveTextAsync("Saved");

        var toggle = Page.Locator("#overview-save-toggle");
        await Expect(toggle).ToHaveAttributeAsync("aria-label", "More save options");
        await toggle.ClickAsync();
        var list = Page.Locator("#overview-save-menu > ul");
        await Expect(list).ToBeVisibleAsync();
        var group = (await Page.Locator(".nx-splitbutton:has(#overview-save)").BoundingBoxAsync())!;
        var m = (await list.BoundingBoxAsync())!;
        Assert.That(m.X + m.Width, Is.EqualTo(group.X + group.Width).Within(2));

        await list.Locator(".nx-menu-item", new() { HasText = "Save and release" }).ClickAsync();
        await Expect(Page.Locator("#overview-result")).ToHaveTextAsync("Save and release");
    }

    [Test]
    public async Task Split_JoinedHalves()
    {
        await GoToPage("/splitbutton/overview");

        var main = (await Page.Locator("#overview-save").BoundingBoxAsync())!;
        var toggle = (await Page.Locator("#overview-save-toggle").BoundingBoxAsync())!;
        Assert.That(toggle.X, Is.EqualTo(main.X + main.Width - 1).Within(1));
        Assert.That(toggle.Height, Is.EqualTo(main.Height).Within(1));
        Assert.That(await Page.Locator("#overview-save").EvaluateAsync<string>("b => getComputedStyle(b).borderTopRightRadius"), Is.EqualTo("0px"));
    }

    [Test]
    public async Task Split_KeyboardTwoStops_DownOnArrowOpens()
    {
        await GoToPage("/splitbutton/overview");

        await Page.Locator("#overview-save").FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#overview-save-toggle")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Page.Locator("#overview-save-menu > ul")).ToBeVisibleAsync();
        Assert.That(await FocusedTextAsync(), Is.EqualTo("Save as draft"));
    }

    [Test]
    public async Task Split_MenuDisabled_And_Loading()
    {
        await GoToPage("/splitbutton/examples");

        await Expect(Page.Locator("#menu-disabled-split-toggle")).ToBeDisabledAsync();
        await Page.Locator("#menu-disabled-split").ClickAsync();
        await Expect(Page.Locator("#variants-result")).ToHaveTextAsync("Primary only");

        await Page.Locator("#loading-split").ClickAsync();
        await Expect(Page.Locator("#loading-split")).ToHaveAttributeAsync("aria-busy", "true");
        await Expect(Page.Locator("#variants-result")).ToHaveTextAsync("Saved", new() { Timeout = 5000 });
    }

    [Test]
    public async Task Phone_DropDownMenuIsABottomSheet()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/dropdownbutton/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
        await page.Locator("#overview-export").TapAsync();
        // The list renders hidden at its desktop size; nx-menu.js makes it the sheet and reveals it.
        await Expect(page.Locator("#overview-export-menu.nx-menu-sheet > ul")).ToBeVisibleAsync();
        var sheet = (await page.Locator("#overview-export-menu > ul").BoundingBoxAsync())!;
        Assert.That(sheet.Width, Is.EqualTo(390).Within(1));
        Assert.That(sheet.Y + sheet.Height, Is.EqualTo(844).Within(1));
        await page.Locator("#overview-export-menu .nx-menu-item", new() { HasText = "PDF" }).TapAsync();
        await Expect(page.Locator("#overview-picked")).ToHaveTextAsync("Picked: PDF");
    }
}
