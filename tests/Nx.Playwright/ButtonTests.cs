using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ButtonTests : PageTest
{
    private readonly string _baseUrl;

    public ButtonTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Section(string heading) =>
        Page.Locator(".doc-section").Filter(new() { Has = Page.Locator("h2", new() { HasTextString = heading }) });

    private ILocator PlaygroundButton => Page.Locator("#playground-button");
    private ILocator PlaygroundClicks => Page.Locator("#playground-clicks");

    private ILocator Control(string label) =>
        Section("Playground").Locator(".demo-control").Filter(new() { HasText = label });

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/button/{page}");
        // The page is prerendered; clicks only count once the circuit is live.
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private async Task SetHandlerDelay(int ms)
    {
        var input = Control("Handler delay").Locator("input");
        await input.FillAsync(ms.ToString());
        await input.BlurAsync();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Variants_AllFiveRender()
    {
        await GoToPage();

        var stage = Section("Variants").Locator(".demo-stage");
        await Expect(stage.Locator("button.nx-button")).ToHaveCountAsync(5);
        foreach (var variant in new[] { "primary", "secondary", "danger", "ghost", "link" })
            await Expect(stage.Locator($"button.nx-button-{variant}")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task StatusColors_FilledFromTheirTokens_WarningDarkText()
    {
        await GoToPage();

        foreach (var (id, token, fg) in new[] { ("status-success", "--nx-success", "--nx-success-fg"),
                                                ("status-warning", "--nx-warning-fill", "--nx-warning-fg"),
                                                ("status-info", "--nx-info", "--nx-info-fg") })
        {
            var button = Page.Locator($"#{id}");
            var colors = await button.EvaluateAsync<string[]>(
                "(e, t) => { const probe = document.createElement('span'); e.after(probe); probe.style.color = `var(${t[0]})`; probe.style.backgroundColor = `var(${t[1]})`; const s = getComputedStyle(probe); const r = [s.color, s.backgroundColor]; probe.remove(); return r; }",
                new[] { token, fg });
            await Expect(button).ToHaveCSSAsync("background-color", colors[0]);
            await Expect(button).ToHaveCSSAsync("color", colors[1]);
        }
        Assert.That(await Page.Locator("#status-warning").EvaluateAsync<double>(
            "e => { const [r, g, b] = getComputedStyle(e).color.match(/\\d+/g).map(Number); return (r + g + b) / 3; }"), Is.LessThan(80),
            "warning text is dark on amber");
    }

    [Test]
    public async Task Sizes_FollowControlHeightScale()
    {
        await GoToPage();

        var rows = Section("Sizes").Locator(".demo-row");
        var heights = new List<float>();
        for (var i = 0; i < 3; i++)
            heights.Add((await rows.Nth(i).Locator("button").First.BoundingBoxAsync())!.Height);

        Assert.That(heights[0], Is.EqualTo(28).Within(0.5));
        Assert.That(heights[1], Is.EqualTo(34).Within(0.5));
        Assert.That(heights[2], Is.EqualTo(42).Within(0.5));
    }

    [Test]
    public async Task IconOnly_IsSquareWithAccessibleName()
    {
        await GoToPage();

        var edit = Section("Icons").GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true });
        await Expect(edit).ToHaveClassAsync(new Regex("nx-button-icon-only"));
        var box = (await edit.BoundingBoxAsync())!;
        Assert.That(box.Width, Is.EqualTo(box.Height).Within(0.5));
    }

    [Test]
    public async Task IconEnd_RendersAfterLabel()
    {
        await GoToPage();

        var next = Section("Icons").GetByRole(AriaRole.Button, new() { Name = "Next" });
        var label = (await next.Locator(".nx-button-label").BoundingBoxAsync())!;
        var icon = (await next.Locator(".nx-button-icon").BoundingBoxAsync())!;
        Assert.That(icon.X, Is.GreaterThan(label.X));
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Keyboard_EnterAndSpaceActivate()
    {
        await GoToPage("playground");
        await SetHandlerDelay(0);

        await PlaygroundButton.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(PlaygroundClicks).ToHaveTextAsync("Clicks handled: 1");

        await Page.Keyboard.PressAsync(" ");
        await Expect(PlaygroundClicks).ToHaveTextAsync("Clicks handled: 2");
    }

    [Test]
    public async Task Keyboard_FocusRingShowsOnTabFocus()
    {
        await GoToPage();

        var stage = Section("Variants").Locator(".demo-stage");
        await stage.Locator("button").First.FocusAsync();
        await Page.Keyboard.PressAsync("Tab");

        var second = stage.Locator("button").Nth(1);
        await Expect(second).ToBeFocusedAsync();
        await Expect(second).ToHaveCSSAsync("outline-style", "solid");
        await Expect(second).ToHaveCSSAsync("outline-width", "2px");
    }

    /// <summary>
    /// The reported bug: clicking a button drew the browser's rectangular focus box (and, on touch, the tap
    /// flash) over the rounded control. Mouse focus must draw nothing; keyboard focus draws the ring, which
    /// follows the button's corners.
    /// </summary>
    [Test]
    public async Task Focus_MouseClickShowsNoOutline_KeyboardShowsShapedRing()
    {
        await GoToPage();

        var primary = Section("Variants").Locator("button.nx-button-primary");
        await primary.ClickAsync();
        await Expect(primary).ToBeFocusedAsync();
        await Expect(primary).ToHaveCSSAsync("outline-style", "none");
        await Expect(primary).ToHaveCSSAsync("-webkit-tap-highlight-color", "rgba(0, 0, 0, 0)");

        await Page.Locator("h1").FocusAsync();
        await primary.FocusAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(primary).ToBeFocusedAsync();
        await Expect(primary).ToHaveCSSAsync("outline-style", "solid");
        await Expect(primary).ToHaveCSSAsync("outline-width", "2px");
        await Expect(primary).ToHaveCSSAsync("outline-offset", "2px");
        // The ring is an outline, which browsers draw along the border radius rather than as a rectangle.
        Assert.That(await primary.EvaluateAsync<string>("el => getComputedStyle(el).borderTopLeftRadius"), Is.Not.EqualTo("0px"));
    }

    [Test]
    public async Task Disabled_AllVariantsNativeDisabled_AndSkippedByTab()
    {
        await GoToPage();

        var disabled = Section("Disabled").Locator("button");
        await Expect(disabled).ToHaveCountAsync(8);
        for (var i = 0; i < 8; i++)
            await Expect(disabled.Nth(i)).ToBeDisabledAsync();

        // The last focusable control before the Disabled section is the AutoLoading button;
        // Tab from there jumps straight to the form below.
        await Page.Locator("#guard-auto").FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#order-number")).ToBeFocusedAsync();
    }

    // ── States ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Playground_Loading_BlocksClicksAndStaysFocusable()
    {
        await GoToPage("playground");
        await SetHandlerDelay(0);

        await Page.Locator("#pg-loading").CheckAsync();
        await Expect(PlaygroundButton).ToHaveAttributeAsync("aria-busy", "true");
        await Expect(PlaygroundButton).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(PlaygroundButton.Locator(".nx-spinner")).ToBeVisibleAsync();
        // Playwright reads aria-disabled as disabled; what matters is that the native attribute is absent.
        Assert.That(await PlaygroundButton.EvaluateAsync<bool>("el => el.hasAttribute('disabled')"), Is.False);

        await PlaygroundButton.ClickAsync(new() { Force = true });
        await PlaygroundButton.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");

        await Expect(PlaygroundButton).ToBeFocusedAsync();
        await Expect(PlaygroundClicks).ToHaveTextAsync("Clicks handled: 0");
    }

    [Test]
    public async Task Playground_LoadingText_ReplacesLabel()
    {
        await GoToPage("playground");

        await Control("LoadingText").Locator("input").FillAsync("Saving…");
        await Page.Locator("#pg-loading").CheckAsync();

        await Expect(PlaygroundButton.Locator(".nx-button-label")).ToHaveTextAsync("Saving…");
    }

    [Test]
    public async Task Playground_Disabled_RendersNativeDisabled()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-disabled").CheckAsync();

        await Expect(PlaygroundButton).ToBeDisabledAsync();
    }

    [Test]
    public async Task Playground_IconOnly_DropsLabelAndUsesAriaLabel()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-icon-only").CheckAsync();

        await Expect(PlaygroundButton).ToHaveAttributeAsync("aria-label", "Save changes");
        await Expect(PlaygroundButton.Locator(".nx-button-label")).ToHaveCountAsync(0);
        await Expect(PlaygroundButton.Locator(".nx-button-icon svg")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Playground_VariantSelect_ChangesClassAndCode()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-variant").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Danger", Exact = true }).ClickAsync();

        await Expect(PlaygroundButton).ToHaveClassAsync(new Regex("nx-button-danger"));
        await Expect(Section("Playground").Locator(".demo-playground-code")).ToContainTextAsync("NxButtonVariant.Danger");
    }

    // ── Busy-click guard ──────────────────────────────────────────────────────

    [Test]
    public async Task BusyGuard_On_RunsHandlerOnce()
    {
        await GoToPage();

        // Force: Playwright treats aria-disabled as disabled and would wait out the guard before clicking.
        var button = Page.Locator("#guard-on");
        await button.ClickAsync();
        await button.ClickAsync(new() { Force = true });
        await button.ClickAsync(new() { Force = true });

        await Expect(Page.Locator("#guard-on-count")).ToHaveTextAsync("Handler ran 1×");
    }

    [Test]
    public async Task BusyGuard_Off_RunsHandlerPerClick()
    {
        await GoToPage();

        var button = Page.Locator("#guard-off");
        await button.ClickAsync();
        await button.ClickAsync();
        await button.ClickAsync();

        await Expect(Page.Locator("#guard-off-count")).ToHaveTextAsync("Handler ran 3×");
    }

    [Test]
    public async Task AutoLoading_SpinnerWhileRunning_ThenRestores()
    {
        await GoToPage();

        var button = Page.Locator("#guard-auto");
        await button.ClickAsync();

        await Expect(button).ToHaveAttributeAsync("aria-busy", "true");
        await Expect(button.Locator(".nx-spinner")).ToBeVisibleAsync();
        await Expect(button.Locator(".nx-spinner")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Expect(button).Not.ToHaveAttributeAsync("aria-busy", "true");
    }

    // ── Form types ────────────────────────────────────────────────────────────

    [Test]
    public async Task Submit_EmptyForm_ShowsValidation()
    {
        await GoToPage();

        await Page.Locator("#form-submit").ClickAsync();

        await Expect(Section("Form Types").Locator(".validation-message")).ToHaveTextAsync("Enter an order number.");
    }

    [Test]
    public async Task Submit_EnterInField_SubmitsOnceWhileLoading()
    {
        await GoToPage();

        var field = Page.Locator("#order-number");
        await field.FillAsync("SO-1001");
        await field.PressAsync("Enter");

        var submit = Page.Locator("#form-submit");
        await Expect(submit).ToHaveAttributeAsync("aria-busy", "true");
        await field.PressAsync("Enter");
        await Expect(submit).Not.ToHaveAttributeAsync("aria-busy", "true", new() { Timeout = 5000 });

        var log = Section("Form Types").Locator(".demo-log");
        await Expect(log.Locator(".demo-log-entry").Filter(new() { HasText = "OnValidSubmit" })).ToHaveCountAsync(1);
    }

    [Test]
    public async Task PlainButton_InForm_DoesNotSubmit()
    {
        await GoToPage();

        await Page.Locator("#order-number").FillAsync("SO-1002");
        await Page.Locator("#form-plain").ClickAsync();

        var log = Section("Form Types").Locator(".demo-log");
        await Expect(log).ToContainTextAsync("Plain button clicked");
        await Expect(log).Not.ToContainTextAsync("OnValidSubmit");
    }

    [Test]
    public async Task Reset_RestoresNativeFormInputs()
    {
        await GoToPage();

        var input = Page.Locator("#native-input");
        await input.FillAsync("changed");
        await Page.Locator("#form-reset").ClickAsync();

        await Expect(input).ToHaveValueAsync("Type, then reset");
    }

    // ── Full width ────────────────────────────────────────────────────────────

    [Test]
    public async Task FullWidth_FillsContainer()
    {
        await GoToPage();

        var stage = Section("Full Width").Locator(".demo-stage");
        var stageBox = (await stage.BoundingBoxAsync())!;
        var buttonBox = (await stage.Locator("button").First.BoundingBoxAsync())!;

        // The stage has 22px padding and a 1px border on each side.
        Assert.That(buttonBox.Width, Is.EqualTo(stageBox.Width - 46).Within(1));
    }
}
