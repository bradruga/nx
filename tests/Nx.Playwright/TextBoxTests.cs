using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class TextBoxTests : PageTest
{
    private readonly string _baseUrl;

    public TextBoxTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Section(string heading) =>
        Page.Locator(".doc-section").Filter(new() { Has = Page.Locator("h2", new() { HasTextString = heading }) });

    /// <summary>The outer <c>.nx-textbox</c> of the text box whose input has this id.</summary>
    private ILocator TextBox(string inputId) =>
        Page.Locator(".nx-textbox").Filter(new() { Has = Page.Locator($"#{inputId}") });

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/textbox/{page}");
        // The page is prerendered; typing only reaches the component once the circuit is live.
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    // ── Binding modes ─────────────────────────────────────────────────────────

    [Test]
    public async Task OnChange_CommitsOnEnterOrBlur_NotWhileTyping()
    {
        await GoToPage();

        var input = Page.Locator("#bind-change");
        await input.PressSequentiallyAsync("Acme");
        await Expect(Page.Locator("#bind-change-count")).ToHaveTextAsync("× 0");

        await input.PressAsync("Enter");
        await Expect(Page.Locator("#bind-change-value")).ToHaveTextAsync("Value: Acme");
        await Expect(Page.Locator("#bind-change-count")).ToHaveTextAsync("× 1");

        await input.PressSequentiallyAsync(" Co");
        await input.BlurAsync();
        await Expect(Page.Locator("#bind-change-value")).ToHaveTextAsync("Value: Acme Co");
    }

    [Test]
    public async Task Immediate_CommitsEveryKeystroke()
    {
        await GoToPage();

        await Page.Locator("#bind-immediate").PressSequentiallyAsync("Acme");

        await Expect(Page.Locator("#bind-immediate-value")).ToHaveTextAsync("Value: Acme");
        await Expect(Page.Locator("#bind-immediate-count")).ToHaveTextAsync("× 4");
    }

    [Test]
    public async Task Debounce_CommitsOnceAfterPause()
    {
        await GoToPage();

        await Page.Locator("#bind-debounce").PressSequentiallyAsync("Acme", new() { Delay = 40 });
        await Expect(Page.Locator("#bind-debounce-count")).ToHaveTextAsync("× 0");

        await Expect(Page.Locator("#bind-debounce-value")).ToHaveTextAsync("Value: Acme", new() { Timeout = 3000 });
        await Expect(Page.Locator("#bind-debounce-count")).ToHaveTextAsync("× 1");
    }

    [Test]
    public async Task Debounce_BlurCommitsWithoutWaiting()
    {
        await GoToPage();

        var input = Page.Locator("#bind-debounce");
        await input.PressSequentiallyAsync("Ac");
        await input.BlurAsync();

        await Expect(Page.Locator("#bind-debounce-value")).ToHaveTextAsync("Value: Ac", new() { Timeout = 300 });
    }

    [Test]
    public async Task Typing_FastUnderImmediate_LosesNoCharacters()
    {
        await GoToPage();

        var text = "The quick brown fox jumps";
        await Page.Locator("#bind-immediate").PressSequentiallyAsync(text);

        await Expect(Page.Locator("#bind-immediate")).ToHaveValueAsync(text);
        await Expect(Page.Locator("#bind-immediate-value")).ToHaveTextAsync("Value: " + text);
    }

    // ── Labels ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Label_ClickFocusesInput()
    {
        await GoToPage();

        await TextBox("label-fixed").Locator("label").ClickAsync();

        await Expect(Page.Locator("#label-fixed")).ToBeFocusedAsync();
    }

    // ── Prefix and suffix ─────────────────────────────────────────────────────

    [Test]
    public async Task PrefixAndSuffix_SitInsideTheFieldAroundTheText()
    {
        await GoToPage();

        var box = TextBox("affix-price");
        var field = (await box.Locator(".nx-textbox-field").BoundingBoxAsync())!;
        var prefix = (await box.Locator(".nx-textbox-prefix").BoundingBoxAsync())!;
        var input = (await box.Locator("input").BoundingBoxAsync())!;
        var suffix = (await box.Locator(".nx-textbox-suffix").BoundingBoxAsync())!;

        Assert.That(prefix.X, Is.GreaterThanOrEqualTo(field.X));
        Assert.That(input.X, Is.GreaterThanOrEqualTo(prefix.X + prefix.Width - 1));
        Assert.That(suffix.X, Is.GreaterThanOrEqualTo(input.X + input.Width - 1));
        Assert.That(suffix.X + suffix.Width, Is.LessThanOrEqualTo(field.X + field.Width));
        await Expect(box.Locator(".nx-textbox-suffix")).ToHaveTextAsync("USD");
    }

    // ── Clear and password ────────────────────────────────────────────────────

    [Test]
    public async Task ClearButton_ClearsAndRefocuses()
    {
        await GoToPage();

        var box = TextBox("clear-demo");
        await box.Locator(".nx-textbox-clear").ClickAsync();

        await Expect(Page.Locator("#clear-demo")).ToHaveValueAsync("");
        await Expect(Page.Locator("#clear-demo")).ToBeFocusedAsync();
        await Expect(box.Locator(".nx-textbox-clear")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ClearButton_AppearsWhenTyping_EscapeClears()
    {
        await GoToPage();

        var input = Page.Locator("#clear-demo");
        await input.FillAsync("");
        await input.BlurAsync();
        await Expect(TextBox("clear-demo").Locator(".nx-textbox-clear")).ToHaveCountAsync(0);

        await input.PressSequentiallyAsync("temp");
        await Expect(TextBox("clear-demo").Locator(".nx-textbox-clear")).ToBeVisibleAsync();

        await input.PressAsync("Escape");
        await Expect(input).ToHaveValueAsync("");
    }

    // ── Escape in a window ────────────────────────────────────────────────────

    private ILocator EscapeWindow => Page.Locator("#escape-window");

    private async Task OpenEscapeWindowAsync()
    {
        await GoToPage();
        await Page.Locator("#escape-open").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#escape-open").ClickAsync();
        await Expect(EscapeWindow).ToBeVisibleAsync();
    }

    [TestCase("escape-name")]
    [TestCase("escape-phone")]
    [TestCase("escape-limit")]
    [TestCase("escape-city")]
    [TestCase("escape-notes")]
    public async Task EscapeInWindow_FirstClearsTheField_SecondClosesTheWindow(string id)
    {
        await OpenEscapeWindowAsync();
        var input = Page.Locator($"#{id}");
        await input.FocusAsync();
        await Expect(input).Not.ToHaveValueAsync("");

        await Page.Keyboard.PressAsync("Escape");

        // The masked box keeps its empty mask while focused; every other field is empty.
        await Expect(input).ToHaveValueAsync(id == "escape-phone" ? "(___) ___-____" : "");
        await Expect(EscapeWindow.Locator($".nx-field-box:has(#{id}) .nx-field-clear, .nx-combobox-field:has(#{id}) .nx-combobox-clear")).ToHaveCountAsync(0);
        // The window is told to close in the same turn as the key, so a wrongly closed one is gone by now.
        await Page.WaitForTimeoutAsync(300);
        await Expect(EscapeWindow).ToBeVisibleAsync();
        await Expect(input).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("Escape");

        await Expect(EscapeWindow).ToHaveCountAsync(0);
    }

    [Test]
    public async Task EscapeInWindow_NotClearableField_ClosesAtOnce_KeepingItsText()
    {
        await OpenEscapeWindowAsync();
        await Page.Locator("#escape-reference").FocusAsync();

        await Page.Keyboard.PressAsync("Escape");

        await Expect(EscapeWindow).ToHaveCountAsync(0);
        await Page.Locator("#escape-open").ClickAsync();
        await Expect(Page.Locator("#escape-reference")).ToHaveValueAsync("REF-118");
    }

    [Test]
    public async Task EscapeInWindow_EmptyClearableField_ClosesAtOnce()
    {
        await OpenEscapeWindowAsync();
        var name = Page.Locator("#escape-name");
        await name.FillAsync("");
        await Expect(TextBox("escape-name").Locator(".nx-textbox-clear")).ToHaveCountAsync(0);

        await Page.Keyboard.PressAsync("Escape");

        await Expect(EscapeWindow).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ClearButton_IsSkippedByTab()
    {
        await GoToPage();

        await Page.Locator("#clear-demo").FocusAsync();
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator("#password-demo")).ToBeFocusedAsync();
    }

    [Test]
    public async Task PasswordToggle_RevealsText_AndIsReachableByTab()
    {
        await GoToPage();

        var input = Page.Locator("#password-demo");
        var toggle = TextBox("password-demo").Locator(".nx-textbox-password-toggle");
        await Expect(input).ToHaveAttributeAsync("type", "password");

        await input.FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(toggle).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Enter");

        await Expect(input).ToHaveAttributeAsync("type", "text");
        await Expect(toggle).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(toggle).ToHaveAttributeAsync("aria-label", "Hide password");
        await Expect(input).ToHaveValueAsync("hunter22");
    }

    [Test]
    public async Task PasswordToggle_Off_NoButton()
    {
        await GoToPage();

        await Expect(TextBox("password-plain").Locator(".nx-textbox-password-toggle")).ToHaveCountAsync(0);
    }

    // ── Max length ────────────────────────────────────────────────────────────

    [Test]
    public async Task MaxLength_StopsInputAndCounterTracksIt()
    {
        await GoToPage();

        var input = Page.Locator("#counter-demo");
        await input.FillAsync("");
        await input.PressSequentiallyAsync("PO-1234567890123");

        await Expect(input).ToHaveValueAsync("PO-123456789");
        await Expect(Page.Locator("#counter-demo-counter")).ToHaveTextAsync("12 / 12");
        await Expect(input).ToHaveAttributeAsync("aria-describedby", "counter-demo-counter");
    }

    // ── Read-only and disabled ────────────────────────────────────────────────

    [Test]
    public async Task ReadOnly_FocusableNotEditable_NoClearButton()
    {
        await GoToPage();

        var input = Page.Locator("#readonly-demo");
        await input.FocusAsync();
        await Expect(input).ToBeFocusedAsync();
        await Page.Keyboard.TypeAsync("x");

        await Expect(input).ToHaveValueAsync("INV-2026-00418");
        await Expect(TextBox("readonly-demo").Locator(".nx-textbox-clear")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Disabled_IsDisabled()
    {
        await GoToPage();

        await Expect(Page.Locator("#disabled-demo")).ToBeDisabledAsync();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Test]
    public async Task Validation_EmptySubmit_ShowsMessagesAndMarksFieldsInvalid()
    {
        await GoToPage();

        await Page.Locator("#contact-submit").ClickAsync();

        var form = Page.Locator("#validation-form");
        await Expect(form.Locator(".validation-message")).ToHaveCountAsync(2);
        await Expect(form.Locator(".validation-message").First).ToHaveTextAsync("Enter a contact name.");
        await Expect(Page.Locator("#contact-name")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(TextBox("contact-name")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-textbox-invalid"));
    }

    [Test]
    public async Task Validation_FixingAField_ClearsItsMessage()
    {
        await GoToPage();

        await Page.Locator("#contact-submit").ClickAsync();
        await Expect(Page.Locator("#contact-name")).ToHaveAttributeAsync("aria-invalid", "true");

        await Page.Locator("#contact-name").FillAsync("Dana Ortiz");
        await Page.Locator("#contact-name").BlurAsync();

        await Expect(Page.Locator("#contact-name")).Not.ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(Page.Locator("#validation-form .validation-message")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Validation_ImmediateField_ValidatesWhileTyping()
    {
        await GoToPage();

        var po = Page.Locator("#contact-po");
        await po.PressSequentiallyAsync("PO");
        await Expect(po).ToHaveAttributeAsync("aria-invalid", "true");

        await po.PressSequentiallyAsync("-77");
        await Expect(po).Not.ToHaveAttributeAsync("aria-invalid", "true");
    }

    [Test]
    public async Task Validation_ValidForm_Submits()
    {
        await GoToPage();

        await Page.Locator("#contact-name").FillAsync("Dana Ortiz");
        await Page.Locator("#contact-email").FillAsync("dana@example.com");
        await Page.Locator("#contact-email").PressAsync("Enter");

        await Expect(Page.Locator("#contact-result")).ToHaveTextAsync("Saved Dana Ortiz", new() { Timeout = 5000 });
    }

    // ── Focus ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task AutoFocus_FocusesWhenPanelOpens()
    {
        await GoToPage();

        await Page.Locator("#autofocus-open").ClickAsync();

        await Expect(Page.Locator("#autofocus-panel")).ToBeFocusedAsync();
    }

    [Test]
    public async Task FocusAsync_FocusesFromButton()
    {
        await GoToPage();

        await Page.Locator("#autofocus-focus").ClickAsync();

        await Expect(Page.Locator("#autofocus-later")).ToBeFocusedAsync();
    }

    // ── Playground ────────────────────────────────────────────────────────────

    [Test]
    public async Task Playground_TypePassword_AddsToggle()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-type").SelectOptionAsync("Password");

        await Expect(Page.Locator("#playground-textbox")).ToHaveAttributeAsync("type", "password");
        await Expect(TextBox("playground-textbox").Locator(".nx-textbox-password-toggle")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Playground_ImmediateToggle_SwitchesBindingMode()
    {
        await GoToPage("playground");

        var input = Page.Locator("#playground-textbox");
        await input.PressSequentiallyAsync("ab");
        await Expect(Page.Locator("#playground-changes")).ToHaveTextAsync("ValueChanged × 0");

        await Page.Locator("#pg-immediate").CheckAsync();
        // Programmatic focus puts the caret at the start, as it does for a native input.
        await input.FocusAsync();
        await input.PressAsync("End");
        await input.PressSequentiallyAsync("c");
        await Expect(Page.Locator("#playground-changes")).Not.ToHaveTextAsync("ValueChanged × 0");
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: \"abc\"");
    }

    [Test]
    public async Task Playground_InputMode_SetsAttribute()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-inputmode").SelectOptionAsync("Decimal");

        await Expect(Page.Locator("#playground-textbox")).ToHaveAttributeAsync("inputmode", "decimal");
    }
}
