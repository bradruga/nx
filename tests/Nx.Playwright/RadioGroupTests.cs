using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class RadioGroupTests : PageTest
{
    private readonly string _baseUrl;

    public RadioGroupTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Radios(string groupId) => Page.Locator($"#{groupId} input[type=radio]");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/radio/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Click_Checks()
    {
        await GoToPage();

        await Page.Locator("#data-radio").GetByRole(AriaRole.Radio, new() { Name = "Hired flatbed" }).ClickAsync();

        await Expect(Page.Locator("#data-value")).ToHaveTextAsync("Value: Flatbed");
        await Expect(Radios("data-radio").Nth(1)).ToBeCheckedAsync();
        await Expect(Radios("data-radio").Nth(0)).Not.ToBeCheckedAsync();
    }

    [Test]
    public async Task LabelClick_Checks()
    {
        await GoToPage();

        await Page.Locator("#data-radio label").Filter(new() { HasText = "Customer pickup" }).ClickAsync();

        await Expect(Page.Locator("#data-value")).ToHaveTextAsync("Value: Pickup");
    }

    [Test]
    public async Task Keyboard_ArrowsMoveAndCheck_Wrapping()
    {
        await GoToPage();

        await Radios("data-radio").Nth(0).FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Page.Locator("#data-value")).ToHaveTextAsync("Value: Flatbed");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Page.Locator("#data-value")).ToHaveTextAsync("Value: Pickup");
        await Expect(Radios("data-radio").Nth(3)).ToBeFocusedAsync();
    }

    [Test]
    public async Task Keyboard_OneTabStop_EnteringAtTheCheckedButton()
    {
        await GoToPage();

        await Page.Locator("#data-radio").GetByRole(AriaRole.Radio, new() { Name = "Rail" }).ClickAsync();
        // Shift+Tab back into the group from the next one lands on its checked button, not its last.
        await Radios("declared-radio").Nth(0).FocusAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");

        await Expect(Radios("data-radio").Nth(2)).ToBeFocusedAsync();
    }

    [Test]
    public async Task Keyboard_ArrowsSkipDisabled()
    {
        await GoToPage();

        await Radios("disabled-radio").Nth(1).FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");

        await Expect(Radios("disabled-radio").Nth(3)).ToBeFocusedAsync();
        await Expect(Radios("disabled-radio").Nth(2)).ToBeDisabledAsync();
    }

    [Test]
    public async Task DeclaredButtons_BindRecordIds()
    {
        await GoToPage();

        await Radios("declared-radio").Nth(2).ClickAsync();

        await Expect(Page.Locator("#declared-value")).ToHaveTextAsync("Value: 3");
    }

    [Test]
    public async Task Horizontal_LaysOutInARow()
    {
        await GoToPage();

        var first = (await Radios("horizontal-radio").Nth(0).BoundingBoxAsync())!;
        var second = (await Radios("horizontal-radio").Nth(1).BoundingBoxAsync())!;
        Assert.That(second.Y, Is.EqualTo(first.Y).Within(1));
        Assert.That(second.X, Is.GreaterThan(first.X));
    }

    [Test]
    public async Task GroupDisabled_AllDisabled()
    {
        await GoToPage();

        for (var i = 0; i < 4; i++)
            await Expect(Radios("group-disabled").Nth(i)).ToBeDisabledAsync();
    }

    [Test]
    public async Task Group_IsLabelledRadiogroup()
    {
        await GoToPage();

        await Expect(Page.GetByRole(AriaRole.Radiogroup, new() { Name = "Delivery", Exact = true })).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Validation_Required()
    {
        await GoToPage();

        await Page.Locator("#form-submit").ClickAsync();
        await Expect(Page.Locator("#radio-form .validation-message")).ToHaveTextAsync("Choose how it ships.");
        await Expect(Page.Locator("#form-radio")).ToHaveAttributeAsync("aria-invalid", "true");

        await Radios("form-radio").Nth(1).ClickAsync();
        await Page.Locator("#form-submit").ClickAsync();
        await Expect(Page.Locator("#form-result")).ToHaveTextAsync("Ships by Hired flatbed");
    }

    [Test]
    public async Task Playground_DeclaredAndHorizontal()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-declared").ClickAsync();
        await Page.Locator("#pg-orientation-Horizontal").ClickAsync();
        await Radios("playground-radio").Nth(3).ClickAsync();

        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: Sandblast");
        await Expect(Page.Locator("#playground-radio")).ToHaveAttributeAsync("aria-orientation", "horizontal");
    }

    [Test]
    public async Task ItemTemplate_ClickingTheMetaTextChecksTheItem()
    {
        await GoToPage();
        await Page.Locator("#template-radio").ScrollIntoViewIfNeededAsync();
        await Expect(Radios("template-radio").Nth(1)).ToBeCheckedAsync();

        await Page.Locator("#template-radio .demo-item-meta", new() { HasText = "next open slot" }).ClickAsync();

        await Expect(Radios("template-radio").Nth(0)).ToBeCheckedAsync();
        await Expect(Radios("template-radio").Nth(1)).Not.ToBeCheckedAsync();
        await Expect(Radios("template-radio").Nth(0)).ToBeFocusedAsync();
    }
}
