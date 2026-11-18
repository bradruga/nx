using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ToggleGroupTests : PageTest
{
    private readonly string _baseUrl;

    public ToggleGroupTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Items(string groupId) => Page.Locator($"#{groupId} .nx-toggle");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/togglegroup/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Single_ClickSelects_AsARadioGroup()
    {
        await GoToPage();

        var group = Page.GetByRole(AriaRole.Radiogroup, new() { Name = "Date range" });
        await group.GetByRole(AriaRole.Radio, new() { Name = "Month" }).ClickAsync();

        await Expect(Page.Locator("#single-value")).ToHaveTextAsync("Value: Month");
        await Expect(group.GetByRole(AriaRole.Radio, new() { Name = "Month" })).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(group.GetByRole(AriaRole.Radio, new() { Name = "Week" })).ToHaveAttributeAsync("aria-checked", "false");
    }

    [Test]
    public async Task Single_OneTabStop_ArrowsMoveAndSelect()
    {
        await GoToPage();

        await Expect(Items("single-group")).ToHaveCountAsync(4);
        var tabIndexes = await Items("single-group").EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('tabindex'))");
        Assert.That(tabIndexes, Is.EqualTo(new[] { "-1", "0", "-1", "-1" }), "only the selected item is in the tab order");

        await Items("single-group").Nth(1).FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Items("single-group").Nth(2)).ToBeFocusedAsync();
        await Expect(Page.Locator("#single-value")).ToHaveTextAsync("Value: Month");

        await Page.Keyboard.PressAsync("End");
        await Expect(Page.Locator("#single-value")).ToHaveTextAsync("Value: Quarter");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Page.Locator("#single-value")).ToHaveTextAsync("Value: Day");
        await Page.Keyboard.PressAsync("Home");
        await Expect(Items("single-group").Nth(0)).ToBeFocusedAsync();
    }

    [Test]
    public async Task Keyboard_TabLeavesTheGroupInOneStep()
    {
        await GoToPage();

        await Items("single-group").Nth(1).FocusAsync();
        await Page.Keyboard.PressAsync("Tab");

        var focusedInGroup = await Page.Locator("#single-group").EvaluateAsync<bool>("el => el.contains(document.activeElement)");
        Assert.That(focusedInGroup, Is.False);
    }

    [Test]
    public async Task Keyboard_ArrowsDoNotScrollThePage()
    {
        await GoToPage();

        await Items("single-group").Nth(1).FocusAsync();
        var before = await Page.EvaluateAsync<double>("() => window.scrollY");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        var after = await Page.EvaluateAsync<double>("() => window.scrollY");

        Assert.That(after, Is.EqualTo(before));
    }

    [Test]
    public async Task Multiple_ClickToggles_PressedButtons()
    {
        await GoToPage();

        var group = Page.GetByRole(AriaRole.Group, new() { Name = "Status filter" });
        var poured = group.GetByRole(AriaRole.Button, new() { Name = "Poured" });
        await poured.ClickAsync();
        await Expect(poured).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator("#multi-value")).ToHaveTextAsync("Values: [Open, Scheduled, Poured]");

        await group.GetByRole(AriaRole.Button, new() { Name = "Open" }).ClickAsync();
        await Expect(Page.Locator("#multi-value")).ToHaveTextAsync("Values: [Scheduled, Poured]");
    }

    [Test]
    public async Task Multiple_ArrowsMove_SpaceToggles()
    {
        await GoToPage();

        await Items("multi-group").Nth(0).FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Page.Locator("#multi-value")).ToHaveTextAsync("Values: [Open, Scheduled]");
        await Page.Keyboard.PressAsync(" ");
        await Expect(Page.Locator("#multi-value")).ToHaveTextAsync("Values: [Open, Scheduled, Poured]");
    }

    [Test]
    public async Task IconOnly_Toolbar_NamedByAriaLabel()
    {
        await GoToPage();

        var italic = Page.GetByRole(AriaRole.Button, new() { Name = "Italic" });
        await italic.ClickAsync();
        await Expect(italic).ToHaveAttributeAsync("aria-pressed", "true");

        var box = (await italic.BoundingBoxAsync())!;
        Assert.That(box.Width, Is.EqualTo(box.Height).Within(1), "icon-only items are square");

        await Page.GetByRole(AriaRole.Radio, new() { Name = "Center" }).ClickAsync();
        await Expect(Page.Locator("#format-value")).ToHaveTextAsync("[bold, italic] · center");
    }

    [Test]
    public async Task Sizes_HeightsFollowButtonScale()
    {
        await GoToPage();

        var heights = new List<float>();
        foreach (var size in new[] { "Small", "Medium", "Large" })
            heights.Add((await Items($"size-{size}").First.BoundingBoxAsync())!.Height);

        Assert.That(heights[0], Is.EqualTo(28).Within(0.5));
        Assert.That(heights[1], Is.EqualTo(34).Within(0.5));
        Assert.That(heights[2], Is.EqualTo(42).Within(0.5));
    }

    [Test]
    public async Task FullWidth_SharesTheWidth()
    {
        await GoToPage();

        var group = (await Page.Locator("#full-group").BoundingBoxAsync())!;
        var widths = new List<float>();
        for (var i = 0; i < 4; i++)
            widths.Add((await Items("full-group").Nth(i).BoundingBoxAsync())!.Width);

        Assert.That(widths.Sum(), Is.EqualTo(group.Width).Within(5));
        Assert.That(widths.Max() - widths.Min(), Is.LessThan(2));
    }

    [Test]
    public async Task DisabledItem_SkippedByArrows()
    {
        await GoToPage();

        await Expect(Items("disabled-items").Nth(1)).ToBeDisabledAsync();
        await Items("disabled-items").Nth(0).FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");

        await Expect(Items("disabled-items").Nth(2)).ToBeFocusedAsync();
    }

    [Test]
    public async Task Validation_MultipleNeedsTwo()
    {
        await GoToPage();

        await Page.Locator("#toggle-form #form-submit").ClickAsync();
        await Expect(Page.Locator("#toggle-form .validation-message")).ToHaveTextAsync("Pick at least two days.");

        await Items("form-days").Nth(0).ClickAsync();
        await Items("form-days").Nth(2).ClickAsync();
        await Page.Locator("#toggle-form #form-submit").ClickAsync();
        await Expect(Page.Locator("#form-result")).ToHaveTextAsync("Saved Mon, Wed");
    }

    [Test]
    public async Task Playground_MultipleAndDeselect()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-deselect").CheckAsync();
        await Items("playground-group").Nth(0).ClickAsync();
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: null");

        await Page.Locator("#pg-multiple").CheckAsync();
        await Items("playground-group").Nth(1).ClickAsync();
        await Expect(Page.Locator("#playground-values")).ToHaveTextAsync("Values: [Pipe, Box]");
    }

    [Test]
    public async Task IconOnly_MultiSelect_ArrowsMoveFocus_ValueStays()
    {
        await GoToPage();
        var bold = Page.GetByRole(AriaRole.Button, new() { Name = "Bold" });
        var italic = Page.GetByRole(AriaRole.Button, new() { Name = "Italic" });
        var underline = Page.GetByRole(AriaRole.Button, new() { Name = "Underline" });
        await bold.ScrollIntoViewIfNeededAsync();
        await Expect(Page.Locator("#format-value")).ToHaveTextAsync("[bold] · left");

        await bold.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(italic).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(underline).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(italic).ToBeFocusedAsync();

        await Expect(bold).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(italic).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(underline).ToHaveAttributeAsync("aria-pressed", "false");
        // Space after the arrows round-trips; only then does the value change, by the focused item alone.
        await Page.Keyboard.PressAsync(" ");
        await Expect(Page.Locator("#format-value")).ToHaveTextAsync("[bold, italic] · left");
    }
}
