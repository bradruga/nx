using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class NumericTextBoxTests : PageTest
{
    private readonly string _baseUrl;

    public NumericTextBoxTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Input(string id) => Page.Locator($"#{id}");
    private ILocator Field(string id) => Page.Locator(".nx-field").Filter(new() { Has = Input(id) });
    private ILocator Shown(string id) => Page.Locator($"#{id}-value");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/numeric/{page}");
        // The page is prerendered; keys only reach the component once the circuit is live.
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    // ── Typing and types ──────────────────────────────────────────────────────

    [Test]
    public async Task Int_TypeAndLeave_Commits()
    {
        await GoToPage();

        await Input("type-int").FillAsync("128");
        await Input("type-int").BlurAsync();

        await Expect(Shown("type-int")).ToHaveTextAsync("Value: 128");
    }

    [Test]
    public async Task NullableDecimal_EmptyIsNull_NonNullableReverts()
    {
        await GoToPage();

        await Input("type-decimal").FillAsync("9.5");
        await Input("type-decimal").PressAsync("Enter");
        await Expect(Shown("type-decimal")).ToHaveTextAsync("Value: 9.5");
        await Input("type-decimal").FillAsync("");
        await Input("type-decimal").PressAsync("Enter");
        await Expect(Shown("type-decimal")).ToHaveTextAsync("Value: null");

        await Input("type-int").FillAsync("");
        await Input("type-int").BlurAsync();
        await Expect(Input("type-int")).ToHaveValueAsync("42");
    }

    [Test]
    public async Task InvalidText_Reverts()
    {
        await GoToPage();

        await Input("type-int").FillAsync("twelve");
        await Input("type-int").BlurAsync();

        await Expect(Input("type-int")).ToHaveValueAsync("42");
        await Expect(Shown("type-int")).ToHaveTextAsync("Value: 42");
    }

    [Test]
    public async Task Double_ArrowStepsByStep()
    {
        await GoToPage();

        await Input("type-double").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowUp");

        await Expect(Shown("type-double")).ToHaveTextAsync("Value: 2.6");
    }

    // ── Keyboard, range ───────────────────────────────────────────────────────

    [Test]
    public async Task Keyboard_ArrowsAndPageKeys_ClampToRange()
    {
        await GoToPage();

        await Input("range-pct").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Shown("range-pct")).ToHaveTextAsync("Value: 20");
        await Page.Keyboard.PressAsync("PageUp");
        await Expect(Shown("range-pct")).ToHaveTextAsync("Value: 70");
        await Page.Keyboard.PressAsync("PageUp");
        await Expect(Shown("range-pct")).ToHaveTextAsync("Value: 100");

        await Expect(Field("range-pct").Locator(".nx-numeric-increase")).ToBeDisabledAsync();
    }

    [Test]
    public async Task Keyboard_ArrowDoesNotMoveCaret()
    {
        await GoToPage();

        await Input("range-thickness").ClickAsync();
        await Page.Keyboard.PressAsync("End");
        await Page.Keyboard.PressAsync("ArrowUp");

        await Expect(Shown("range-thickness")).ToHaveTextAsync("Value: 8.25");
        var caret = await Input("range-thickness").EvaluateAsync<int>("el => el.selectionStart");
        var length = await Input("range-thickness").EvaluateAsync<int>("el => el.value.length");
        Assert.That(caret, Is.EqualTo(length), "ArrowUp moved the caret to the start");
    }

    [Test]
    public async Task TypedValueOutOfRange_IsClamped()
    {
        await GoToPage();

        await Input("range-thickness").FillAsync("99");
        await Input("range-thickness").PressAsync("Enter");

        await Expect(Shown("range-thickness")).ToHaveTextAsync("Value: 24");
    }

    // ── Spin buttons ──────────────────────────────────────────────────────────

    [Test]
    public async Task Spin_ClickStepsOnce()
    {
        await GoToPage();

        await Field("spin-hold").Locator(".nx-numeric-increase").ClickAsync();
        await Expect(Shown("spin-hold")).ToHaveTextAsync("Value: 25");
        await Field("spin-hold").Locator(".nx-numeric-decrease").ClickAsync();
        await Expect(Shown("spin-hold")).ToHaveTextAsync("Value: 24");
    }

    [Test]
    public async Task Spin_HoldRepeats_AndStopsOnRelease()
    {
        await GoToPage();

        var button = Field("spin-hold").Locator(".nx-numeric-increase");
        // The mouse can only press what is on screen; the section is below the fold at 720 px.
        await button.ScrollIntoViewIfNeededAsync();
        var box = (await button.BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();

        // Holding repeats: wait until it has stepped well past one click, however fast this machine is.
        await Page.WaitForFunctionAsync(
            @"() => parseInt(document.getElementById('spin-hold-value').textContent.replace(/\D/g, '')) >= 30",
            null, new() { Timeout = 5000 });
        await Page.Mouse.UpAsync();

        // Released: the value settles and stays put.
        await Page.WaitForTimeoutAsync(300);
        var released = await Shown("spin-hold").TextContentAsync();
        await Page.WaitForTimeoutAsync(600);
        await Expect(Shown("spin-hold")).ToHaveTextAsync(released!);
    }

    [Test]
    public async Task Spin_KeepsFocusInInput()
    {
        await GoToPage();

        await Input("spin-hold").FocusAsync();
        await Field("spin-hold").Locator(".nx-numeric-increase").ClickAsync();

        await Expect(Input("spin-hold")).ToBeFocusedAsync();
    }

    [Test]
    public async Task ShowSpinButtonsFalse_HasNone()
    {
        await GoToPage();

        await Expect(Field("spin-none").Locator(".nx-numeric-spin")).ToHaveCountAsync(0);
    }

    // ── Format and culture ────────────────────────────────────────────────────

    [Test]
    public async Task Culture_FormatsEachBox_FocusShowsEditText()
    {
        await GoToPage();

        await Expect(Input("culture-en-US")).ToHaveValueAsync("$1,234,567.89");
        // .NET separates the euro sign with a no-break space or a plain one depending on the ICU data on the
        // machine; either is right.
        await Expect(Input("culture-de-DE")).ToHaveValueAsync(new System.Text.RegularExpressions.Regex(@"^1\.234\.567,89[\s\u00A0\u202F]€$"));

        await Input("culture-de-DE").FocusAsync();
        await Expect(Input("culture-de-DE")).ToHaveValueAsync("1234567,891");
    }

    [Test]
    public async Task Culture_ParsesTypedTextInThatCulture()
    {
        await GoToPage();

        await Input("culture-de-DE").FillAsync("2.500,75");
        await Input("culture-de-DE").PressAsync("Enter");

        await Expect(Page.Locator("#culture-value")).ToHaveTextAsync("Value: 2500.75");
        await Expect(Input("culture-en-US")).ToHaveValueAsync("$2,500.75");
    }

    [Test]
    public async Task Decimals_RoundTypedValue()
    {
        await GoToPage();

        await Input("decimals-demo").FillAsync("3.14159");
        await Input("decimals-demo").BlurAsync();

        await Expect(Input("decimals-demo")).ToHaveValueAsync("3.14");
        await Expect(Page.Locator("#decimals-value")).ToHaveTextAsync("Value: 3.14");
    }

    [Test]
    public async Task Parse_AccountingParenthesesAndCurrency()
    {
        await GoToPage();

        await Input("affix-cost").FillAsync("($1,250.00)");
        await Input("affix-cost").BlurAsync();

        await Expect(Input("affix-cost")).ToHaveValueAsync("-1,250.00");
    }

    [Test]
    public async Task PrefixAndSuffix_SitInsideTheField()
    {
        await GoToPage();

        await Expect(Field("affix-cost").Locator(".nx-numeric-prefix")).ToHaveTextAsync("$");
        await Expect(Field("affix-margin").Locator(".nx-numeric-suffix")).ToHaveTextAsync("%");
    }

    // ── Select on focus ───────────────────────────────────────────────────────

    [Test]
    public async Task SelectOnFocus_ClickSelectsAll_TypingReplaces()
    {
        await GoToPage();

        await Input("select-on").ClickAsync();
        await Page.WaitForFunctionAsync(
            "id => { const el = document.getElementById(id); return el.value.length > 0 && el.selectionStart === 0 && el.selectionEnd === el.value.length; }",
            "select-on");

        await Page.Keyboard.TypeAsync("7");
        await Expect(Input("select-on")).ToHaveValueAsync("7");
    }

    [Test]
    public async Task SelectOnFocus_Off_ClickPlacesCaret()
    {
        await GoToPage();

        await Input("select-off").ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        var selection = await Input("select-off").EvaluateAsync<int[]>("el => [el.selectionStart, el.selectionEnd]");
        Assert.That(selection[0], Is.EqualTo(selection[1]), "text was selected without SelectOnFocus");
    }

    // ── Labels, clear, state ──────────────────────────────────────────────────

    [Test]
    public async Task ClearButton_Clears()
    {
        await GoToPage();

        await Field("state-clear").Locator(".nx-numeric-clear").ClickAsync();

        await Expect(Input("state-clear")).ToHaveValueAsync("");
        await Expect(Field("state-clear").Locator(".nx-numeric-clear")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ReadOnly_KeysDoNothing_DisabledIsDisabled()
    {
        await GoToPage();

        await Input("state-readonly").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Input("state-readonly")).ToHaveValueAsync("1,250");

        await Expect(Input("state-disabled")).ToBeDisabledAsync();
    }

    [Test]
    public async Task Aria_SpinbuttonValues()
    {
        await GoToPage();

        await Expect(Input("range-pct")).ToHaveAttributeAsync("role", "spinbutton");
        await Expect(Input("range-pct")).ToHaveAttributeAsync("aria-valuenow", "15");
        await Expect(Input("range-pct")).ToHaveAttributeAsync("aria-valuemin", "0");
        await Expect(Input("range-pct")).ToHaveAttributeAsync("aria-valuemax", "100");
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Test]
    public async Task Validation_RequiredAndRange()
    {
        await GoToPage();

        await Page.Locator("#line-submit").ClickAsync();
        var messages = Page.Locator("#numeric-form .validation-message");
        await Expect(messages).ToHaveTextAsync(new[] { "Enter a quantity.", "Enter a unit price." });
        await Expect(Input("line-qty")).ToHaveAttributeAsync("aria-invalid", "true");

        await Input("line-qty").FillAsync("0");
        await Input("line-qty").BlurAsync();
        await Expect(messages.First).ToHaveTextAsync("Quantity is 1 to 10,000.");

        await Input("line-qty").FillAsync("4");
        await Input("line-price").FillAsync("12.5");
        await Input("line-price").PressAsync("Enter");
        await Page.Locator("#line-submit").ClickAsync();
        await Expect(Page.Locator("#line-result")).ToHaveTextAsync("Added 4 × $12.50");
    }

    // ── Playground ────────────────────────────────────────────────────────────

    [Test]
    public async Task Playground_TypeFormatAndCulture()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-format").SelectOptionAsync("N2");
        await Page.Locator("#pg-culture").SelectOptionAsync("de-DE");
        await Expect(Input("playground-numeric")).ToHaveValueAsync("1.234,50");

        await Page.Locator("#pg-type").SelectOptionAsync("int");
        await Expect(Input("playground-numeric")).ToHaveValueAsync("12,00");
        await Input("playground-numeric").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: 13");
    }

    [Test]
    public async Task Playground_MaxAndSpinToggle()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-max").FillAsync("1000");
        await Page.Locator("#pg-max").BlurAsync();
        // The new Max arrives a round trip later; typing before it lands is overwritten by that render.
        await Expect(Input("playground-numeric")).ToHaveAttributeAsync("aria-valuemax", "1000");
        await Input("playground-numeric").FillAsync("5000");
        await Input("playground-numeric").PressAsync("Enter");
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: 1000");

        await Page.Locator("#pg-spin").UncheckAsync();
        await Expect(Field("playground-numeric").Locator(".nx-numeric-spin")).ToHaveCountAsync(0);
    }
}
