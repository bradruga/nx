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
}
