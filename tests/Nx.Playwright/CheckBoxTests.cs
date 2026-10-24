using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class CheckBoxTests : PageTest
{
    private readonly string _baseUrl;

    public CheckBoxTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Box(string id) => Page.Locator($"#{id}");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/checkbox/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Click_Toggles_AndIsACheckboxToScreenReaders()
    {
        await GoToPage();

        var box = Page.GetByRole(AriaRole.Checkbox, new() { Name = "bool", Exact = true });
        await Expect(box).ToHaveAttributeAsync("aria-checked", "false");
        await box.ClickAsync();

        await Expect(box).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(Page.Locator("#bool-value")).ToHaveTextAsync("Value: True");
    }

    [Test]
    public async Task Keyboard_SpaceToggles()
    {
        await GoToPage();

        await Box("bool-box").FocusAsync();
        await Page.Keyboard.PressAsync(" ");
        await Expect(Page.Locator("#bool-value")).ToHaveTextAsync("Value: True");
        await Page.Keyboard.PressAsync(" ");
        await Expect(Page.Locator("#bool-value")).ToHaveTextAsync("Value: False");
    }

    [Test]
    public async Task LabelClick_Toggles()
    {
        await GoToPage();

        await Page.Locator("label[for=bool-box]").ClickAsync();

        await Expect(Page.Locator("#bool-value")).ToHaveTextAsync("Value: True");
    }

    [Test]
    public async Task Nullable_StartsMixed_FirstClickChecks()
    {
        await GoToPage();

        await Expect(Box("nullable-box")).ToHaveAttributeAsync("aria-checked", "mixed");
        await Box("nullable-box").ClickAsync();

        await Expect(Page.Locator("#nullable-value")).ToHaveTextAsync("Value: True");
    }

    [Test]
    public async Task SelectAll_ParentReflectsChildren_AndSetsThem()
    {
        await GoToPage();

        await Expect(Box("all-box")).ToHaveAttributeAsync("aria-checked", "mixed");
        await Box("all-box").ClickAsync();
        await Expect(Page.Locator("#all-count")).ToHaveTextAsync("Done: 3 of 3");
        await Expect(Box("all-box")).ToHaveAttributeAsync("aria-checked", "true");

        await Box("all-box").ClickAsync();
        await Expect(Page.Locator("#all-count")).ToHaveTextAsync("Done: 0 of 3");

        await Box("pour-2").ClickAsync();
        await Expect(Box("all-box")).ToHaveAttributeAsync("aria-checked", "mixed");
    }

    [Test]
    public async Task ThreeState_CyclesThroughEither()
    {
        await GoToPage();

        await Box("three-box").ClickAsync();
        await Expect(Page.Locator("#three-value")).ToHaveTextAsync("Value: False");
        await Box("three-box").ClickAsync();
        await Expect(Page.Locator("#three-value")).ToHaveTextAsync("Value: True");
        await Box("three-box").ClickAsync();
        await Expect(Page.Locator("#three-value")).ToHaveTextAsync("Value: null (either)");
    }

    [Test]
    public async Task LabelPositionStart_PutsLabelFirst()
    {
        await GoToPage();

        var label = (await Page.Locator("label[for=label-start]").BoundingBoxAsync())!;
        var box = (await Box("label-start").BoundingBoxAsync())!;
        Assert.That(label.X, Is.LessThan(box.X));
    }

    [Test]
    public async Task DisabledAndReadOnly()
    {
        await GoToPage();

        await Expect(Box("state-disabled")).ToBeDisabledAsync();
        await Box("state-readonly").ClickAsync();
        await Expect(Box("state-readonly")).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(Box("state-readonly")).ToHaveAttributeAsync("aria-readonly", "true");
        await Box("state-readonly").FocusAsync();
        await Expect(Box("state-readonly")).ToBeFocusedAsync();
    }

    [Test]
    public async Task FocusRing_OnKeyboardFocus()
    {
        await GoToPage();

        await Box("bool-box").FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Box("nullable-box")).ToBeFocusedAsync();

        await Expect(Box("nullable-box")).ToHaveCSSAsync("outline-style", "solid");
    }

    [Test]
    public async Task FocusRing_NotOnMouseClick()
    {
        await GoToPage();

        await Box("bool-box").ClickAsync();

        await Expect(Box("bool-box")).ToBeFocusedAsync();
        await Expect(Box("bool-box")).ToHaveCSSAsync("outline-style", "none");
    }

    [Test]
    public async Task Validation_MustBeChecked()
    {
        await GoToPage();

        await Page.Locator("#consent-submit").ClickAsync();
        await Expect(Page.Locator("#checkbox-form .validation-message")).ToHaveTextAsync("Review the drawings first.");
        await Expect(Box("consent-box")).ToHaveAttributeAsync("aria-invalid", "true");

        await Box("consent-box").ClickAsync();
        await Page.Locator("#consent-submit").ClickAsync();
        await Expect(Page.Locator("#consent-result")).ToHaveTextAsync("Released");
    }

    [Test]
    public async Task Playground_ThreeStateAndSetNull()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-set-null").ClickAsync();
        await Expect(Box("playground-checkbox")).ToHaveAttributeAsync("aria-checked", "mixed");

        await Page.Locator("#pg-three").CheckAsync();
        await Box("playground-checkbox").ClickAsync();
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: False");
    }

    [Test]
    public async Task Phone_HitAreaIs44px()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/checkbox/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        var box = (await page.Locator("#bool-box").BoundingBoxAsync())!;
        // Tap just outside the 18px box, inside the 44px hit area.
        await page.Touchscreen.TapAsync(box.X - 8, box.Y + box.Height / 2);

        await Expect(page.Locator("#bool-value")).ToHaveTextAsync("Value: True");
    }
}
