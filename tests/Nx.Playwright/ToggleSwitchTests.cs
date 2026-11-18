using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ToggleSwitchTests : PageTest
{
    private readonly string _baseUrl;

    public ToggleSwitchTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Switch(string id) => Page.Locator($"#{id}");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/switch/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Click_Flips_AndIsASwitchToScreenReaders()
    {
        await GoToPage("overview");

        var toggle = Page.GetByRole(AriaRole.Switch, new() { Name = "Email me when the order ships" });
        await Expect(toggle).ToHaveAttributeAsync("aria-checked", "true");
        await toggle.ClickAsync();

        await Expect(toggle).ToHaveAttributeAsync("aria-checked", "false");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: False");
    }

    [Test]
    public async Task Keyboard_SpaceAndEnterFlip()
    {
        await GoToPage("overview");

        await Switch("overview-switch").FocusAsync();
        await Page.Keyboard.PressAsync(" ");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: False");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: True");
    }

    [Test]
    public async Task ThumbSlidesToTheOtherEnd()
    {
        await GoToPage("overview");

        var thumb = Switch("overview-switch").Locator(".nx-switch-thumb");
        var onX = (await thumb.BoundingBoxAsync())!.X;
        await Switch("overview-switch").ClickAsync();
        await Page.WaitForTimeoutAsync(250);
        var offX = (await thumb.BoundingBoxAsync())!.X;

        Assert.That(onX, Is.GreaterThan(offX + 10));
    }

    [Test]
    public async Task Sizes_TrackHeightsIncrease()
    {
        await GoToPage();

        var heights = new List<float>();
        foreach (var size in new[] { "Small", "Medium", "Large" })
            heights.Add((await Switch($"size-{size}").BoundingBoxAsync())!.Height);

        Assert.That(heights, Is.Ordered.Ascending);
        Assert.That(heights.Distinct().Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task StateText_InsideFollowsValue_BesideToo()
    {
        await GoToPage();

        await Expect(Switch("text-inside").Locator(".nx-switch-state-inside")).ToHaveTextAsync("On");
        await Switch("text-inside").ClickAsync();
        await Expect(Switch("text-inside").Locator(".nx-switch-state-inside")).ToHaveTextAsync("Off");

        var beside = Page.Locator(".nx-switch").Filter(new() { Has = Switch("text-beside") }).Locator(".nx-switch-state-beside");
        await Expect(beside).ToHaveTextAsync("No");
        await Switch("text-beside").ClickAsync();
        await Expect(beside).ToHaveTextAsync("Yes");
    }

    [Test]
    public async Task Loading_BlocksFurtherClicks_UntilSaved()
    {
        await GoToPage();

        await Switch("loading-switch").ClickAsync();
        await Expect(Switch("loading-switch")).ToHaveAttributeAsync("aria-busy", "true");
        await Expect(Switch("loading-switch").Locator(".nx-spinner")).ToBeVisibleAsync();
        // Force: Playwright reads aria-disabled as disabled.
        await Switch("loading-switch").ClickAsync(new() { Force = true });

        await Expect(Switch("loading-switch")).Not.ToHaveAttributeAsync("aria-busy", "true", new() { Timeout = 4000 });
        await Expect(Switch("loading-switch")).ToHaveAttributeAsync("aria-checked", "true");
        await Expect(Page.Locator(".demo-log .demo-log-entry").Filter(new() { HasText = "Saving" })).ToHaveCountAsync(1);
    }

    [Test]
    public async Task LabelClick_Flips()
    {
        await GoToPage();

        await Page.Locator("label[for=pos-start-2]").ClickAsync();

        await Expect(Switch("pos-start-2")).ToHaveAttributeAsync("aria-checked", "true");
    }

    [Test]
    public async Task Disabled_IsDisabled()
    {
        await GoToPage();

        await Expect(Switch("disabled-on")).ToBeDisabledAsync();
        await Expect(Switch("disabled-on")).ToHaveAttributeAsync("aria-checked", "true");
    }

    [Test]
    public async Task Validation_MustBeOn()
    {
        await GoToPage();

        await Page.Locator("#form-submit").ClickAsync();
        await Expect(Page.Locator("#switch-form .validation-message")).ToHaveTextAsync("Certify the mix design first.");
        await Expect(Switch("form-switch")).ToHaveAttributeAsync("aria-invalid", "true");

        await Switch("form-switch").ClickAsync();
        await Page.Locator("#form-submit").ClickAsync();
        await Expect(Page.Locator("#form-result")).ToHaveTextAsync("Scheduled");
    }

    [Test]
    public async Task Playground_TextAndSize()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-text").CheckAsync();
        await Expect(Switch("playground-switch").Locator(".nx-switch-state-inside")).ToHaveTextAsync("On");
        await Page.Locator("#pg-text-Beside").ClickAsync();
        await Expect(Switch("playground-switch").Locator(".nx-switch-state-inside")).ToHaveCountAsync(0);

        await Page.Locator("#pg-size-Large").ClickAsync();
        await Expect(Page.Locator(".nx-switch").Filter(new() { Has = Switch("playground-switch") })).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-switch-lg"));
    }

    [Test]
    public async Task LabelPosition_StartPutsTheLabelLeftOfTheSwitch()
    {
        await GoToPage();
        await Switch("pos-start-1").ScrollIntoViewIfNeededAsync();

        foreach (var id in new[] { "pos-start-1", "pos-start-2", "pos-start-3" })
        {
            var label = (await Page.Locator($"label[for={id}]").BoundingBoxAsync())!;
            var toggle = (await Switch(id).BoundingBoxAsync())!;
            Assert.That(label.X + label.Width, Is.LessThanOrEqualTo(toggle.X), id);
            Assert.That(label.Y + label.Height / 2, Is.EqualTo(toggle.Y + toggle.Height / 2).Within(4), id);
        }
    }

    [Test]
    public async Task Loading_KeepsFocusAfterTheSave()
    {
        await GoToPage();
        var toggle = Switch("loading-switch");
        await toggle.ScrollIntoViewIfNeededAsync();

        await toggle.ClickAsync();
        await Expect(toggle).ToHaveAttributeAsync("aria-busy", "true");
        await Expect(toggle).ToBeFocusedAsync();
        await Expect(toggle).Not.ToHaveAttributeAsync("aria-busy", "true", new() { Timeout = 4000 });

        await Expect(toggle).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync(" ");
        await Expect(toggle).ToHaveAttributeAsync("aria-busy", "true");
    }
}
