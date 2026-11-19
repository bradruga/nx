using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxMaskedTextBox: the typing is nx-input.js's, so it is tested in a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class MaskedTextBoxTests : PageTest
{
    private readonly string _baseUrl;

    public MaskedTextBoxTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.GotoAsync($"{_baseUrl}/maskedtextbox/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    /// <summary>Focuses the field and empties it, leaving the caret on the first slot.</summary>
    private async Task<ILocator> EmptyAsync(string id)
    {
        var input = Page.Locator($"#{id}");
        await input.FocusAsync();
        await Page.Keyboard.PressAsync("ControlOrMeta+a");
        await Page.Keyboard.PressAsync("Backspace");
        await Page.Keyboard.PressAsync("Home");
        return input;
    }

    [Test]
    public async Task FastTyping_FillsTheSlots_BlurCommitsWithoutLiterals()
    {
        await GoToPage("examples");

        var input = await EmptyAsync("mask-phone");
        await Expect(input).ToHaveValueAsync("(___) ___-____");
        await input.PressSequentiallyAsync("8005551234");
        await Expect(input).ToHaveValueAsync("(800) 555-1234");

        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#mask-phone-value")).ToHaveTextAsync("Value: 8005551234");
        await Expect(input).ToHaveAttributeAsync("inputmode", "numeric");
    }

    [Test]
    public async Task WrongCharacters_Skipped_TypedLiteralsStepOver()
    {
        await GoToPage("examples");

        var part = await EmptyAsync("mask-part");
        await part.PressSequentiallyAsync("1ab-1x23c");
        await Expect(part).ToHaveValueAsync("ab-123-c");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#mask-part-value")).ToHaveTextAsync("Value: ab-123-c");
    }

    [Test]
    public async Task Backspace_ClearsBackOverLiterals_CaretStays()
    {
        await GoToPage("examples");

        var input = Page.Locator("#mask-phone");
        await input.FocusAsync();
        await Page.Keyboard.PressAsync("End");
        for (var i = 0; i < 5; i++)
            await Page.Keyboard.PressAsync("Backspace");

        await Expect(input).ToHaveValueAsync("(207) 55_-____");
        Assert.That(await input.EvaluateAsync<int>("e => e.selectionStart"), Is.EqualTo(8));

        await Page.Keyboard.TypeAsync("9");
        await Expect(input).ToHaveValueAsync("(207) 559-____");
        Assert.That(await input.EvaluateAsync<int>("e => e.selectionStart"), Is.EqualTo(10), "the caret skips the dash");
    }

    [Test]
    public async Task MaskOnFocus_PlaceholderUntilFocused()
    {
        await GoToPage("examples");

        var input = Page.Locator("#prompt-focus");
        await Expect(input).ToHaveValueAsync("");
        await Expect(input).ToHaveAttributeAsync("placeholder", "YY-MM00");

        await input.FocusAsync();
        await Expect(input).ToHaveValueAsync("__-____");
        Assert.That(await input.EvaluateAsync<int>("e => e.selectionStart"), Is.EqualTo(0));

        await Page.Keyboard.PressAsync("Tab");
        await Expect(input).ToHaveValueAsync("");
    }

    [Test]
    public async Task ReadOnly_TypingIgnored()
    {
        await GoToPage("examples");

        var input = Page.Locator("#readonly-mask");
        await input.FocusAsync();
        await Page.Keyboard.TypeAsync("9");
        await Expect(input).ToHaveValueAsync("26-0301");
        await Expect(Page.Locator("#disabled-mask")).ToBeDisabledAsync();
    }

    [Test]
    public async Task Form_PartialFailsTheRule_FullSaves()
    {
        await GoToPage("examples");

        var input = await EmptyAsync("mask-bid");
        await input.PressSequentiallyAsync("2604");
        await Page.Locator("#mask-submit").ClickAsync();
        await Expect(Page.Locator("#mask-form .validation-message, #mask-form .nx-validation-message"))
            .ToContainTextAsync("all six digits");
        await Expect(input).ToHaveAttributeAsync("aria-invalid", "true");

        await EmptyAsync("mask-bid");
        await input.PressSequentiallyAsync("260412");
        await Page.Locator("#mask-submit").ClickAsync();
        await Expect(Page.Locator("#mask-result")).ToHaveTextAsync("Saved 26-0412");
    }

    [Test]
    public async Task Paste_FillsTheSlots_AndTheBoundValue()
    {
        await GoToPage("examples");
        await Context.GrantPermissionsAsync(new[] { "clipboard-read", "clipboard-write" });
        await Page.EvaluateAsync("() => navigator.clipboard.writeText('2075550142')");

        var input = await EmptyAsync("mask-phone");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#mask-phone-value")).ToHaveTextAsync("Value: null");

        await input.FocusAsync();
        await Page.Keyboard.PressAsync("Home");
        await Page.Keyboard.PressAsync("ControlOrMeta+v");
        await Expect(input).ToHaveValueAsync("(207) 555-0142");

        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#mask-phone-value")).ToHaveTextAsync("Value: 2075550142");
        await Expect(input).ToHaveValueAsync("(207) 555-0142");
    }

    [Test]
    public async Task ClearButton_RestoresTheEmptyMask_FocusStays()
    {
        await GoToPage("examples");

        var input = Page.Locator("#clear-mask");
        await input.ScrollIntoViewIfNeededAsync();
        await Expect(input).ToHaveValueAsync("(800) 555-0199");
        await input.FocusAsync();

        await Page.Locator("button[aria-controls=clear-mask][aria-label=Clear]").ClickAsync();

        await Expect(input).ToHaveValueAsync("(___) ___-____");
        await Expect(input).ToBeFocusedAsync();
        await Expect(Page.Locator("button[aria-controls=clear-mask][aria-label=Clear]")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ClearButton_ThenTyping_FillsFromTheFirstSlot()
    {
        await GoToPage("examples");

        var input = Page.Locator("#clear-mask");
        await input.ScrollIntoViewIfNeededAsync();
        await input.ClickAsync();
        await Page.Locator("button[aria-controls=clear-mask][aria-label=Clear]").ClickAsync();
        await Expect(input).ToHaveValueAsync("(___) ___-____");

        await Page.Keyboard.TypeAsync("6");
        await Expect(input).ToHaveValueAsync("(6__) ___-____");
    }

    [Test]
    public async Task Prompt_TabbingIn_ShowsTheCustomPrompt_TypingFillsIt()
    {
        await GoToPage("examples");

        var input = Page.Locator("#prompt-dot");
        await input.ScrollIntoViewIfNeededAsync();
        await Expect(input).ToHaveValueAsync("···-···");
        await Page.Locator(".doc-section:has(#mask-time) .doc-example-code-toggle").FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(input).ToBeFocusedAsync();

        await Page.Keyboard.TypeAsync("12");
        await Expect(input).ToHaveValueAsync("12·-···");
        await Page.Keyboard.TypeAsync("3456");
        await Expect(input).ToHaveValueAsync("123-456");

        await Page.Keyboard.PressAsync("Tab");
        await Expect(input).ToHaveValueAsync("123-456");
    }

    [Test]
    public async Task Prompt_ClickingIntoAnEmptyMask_TypingFillsFromTheFirstSlot()
    {
        await GoToPage("examples");

        var input = Page.Locator("#prompt-dot");
        await input.ScrollIntoViewIfNeededAsync();
        await input.ClickAsync();
        await Page.Keyboard.TypeAsync("12");

        await Expect(input).ToHaveValueAsync("12·-···");
    }
}
