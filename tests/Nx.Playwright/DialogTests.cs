using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxDialog and NxDialogService.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class DialogTests : PageTest
{
    private readonly string _baseUrl;

    public DialogTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.GotoAsync($"{_baseUrl}/dialog/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Dialog => Page.Locator(".nx-window.nx-dialog");

    private ILocator DialogButton(string text) => Dialog.Locator(".nx-window-footer button", new() { HasText = text });

    [Test]
    public async Task Alert_KeepsLineBreaks_ConfirmFocused_EnterCloses()
    {
        await GoToPage("examples");

        await Page.Locator("#ex-alert").ClickAsync();
        await Expect(Dialog).ToBeVisibleAsync();
        await Expect(Dialog).ToHaveAttributeAsync("role", "dialog");
        await Expect(Dialog.Locator(".nx-window-title")).ToHaveTextAsync("Lines closed");
        var message = Dialog.Locator(".nx-dialog-message");
        await Expect(message).ToContainTextAsync("PO 4410 line 3");
        var height = await message.EvaluateAsync<double>("e => e.getBoundingClientRect().height");
        var line = await message.EvaluateAsync<double>("e => parseFloat(getComputedStyle(e).lineHeight)");
        Assert.That(height, Is.GreaterThan(line * 3), "the message's line breaks show");
        await Expect(DialogButton("OK")).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("Enter");

        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Page.Locator("#ex-result")).ToHaveTextAsync("Result: alert closed");
        await Expect(Page.Locator("#ex-alert")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Confirm_Escape_IsNo_YesNoTexts()
    {
        await GoToPage("examples");

        await Page.Locator("#ex-confirm").ClickAsync();
        await Expect(DialogButton("Cancel")).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator("#ex-result")).ToHaveTextAsync("Result: false");

        await Page.Locator("#ex-yesno").ClickAsync();
        await DialogButton("Yes").ClickAsync();
        await Expect(Page.Locator("#ex-result")).ToHaveTextAsync("Result: yes");

        await Page.Locator("#ex-yesno").ClickAsync();
        await DialogButton("No").ClickAsync();
        await Expect(Page.Locator("#ex-result")).ToHaveTextAsync("Result: no");
    }

    [Test]
    public async Task Prompt_FocusInTheField_EnterAnswers()
    {
        await GoToPage("examples");

        await Page.Locator("#ex-prompt").ClickAsync();
        var input = Dialog.Locator("input");
        await Expect(input).ToBeFocusedAsync();
        await Expect(input).ToHaveAttributeAsync("placeholder", "Draft name");
        await Page.Keyboard.TypeAsync("North yard");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#ex-result")).ToHaveTextAsync("Result: \"North yard\"");

        await Page.Locator("#ex-prompt").ClickAsync();
        await DialogButton("Cancel").ClickAsync();
        await Expect(Page.Locator("#ex-result")).ToHaveTextAsync("Result: cancelled");
    }

    [Test]
    public async Task Show_ValidateKeepsOpen_ThenBusySave()
    {
        await GoToPage("examples");

        await Page.Locator("#ex-show").ClickAsync();
        await Expect(Page.Locator("#crew-name")).ToBeFocusedAsync();
        await Expect(Page.Locator("#crew-lead")).ToBeVisibleAsync();

        await DialogButton("Create crew").ClickAsync();
        await Expect(Dialog.Locator(".nx-dialog-error")).ToHaveTextAsync("Give the crew a name.");

        await Page.Locator("#crew-name").FillAsync("Night pour");
        await DialogButton("Create crew").ClickAsync();
        await Expect(DialogButton("Create crew")).ToHaveAttributeAsync("aria-busy", "true");

        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Page.Locator("#ex-show-result")).ToContainTextAsync("Created Night pour");
    }

    [Test]
    public async Task ContentClosesTheDialog_CodeClosesTheDialog()
    {
        await GoToPage("examples");

        await Page.Locator("#ex-pick").ClickAsync();
        await Page.Locator("#truck-21").DblClickAsync();
        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Page.Locator("#ex-pick-result")).ToHaveTextAsync("Truck 21");

        await Page.Locator("#ex-timed").ClickAsync();
        await Expect(Dialog).ToBeVisibleAsync();
        await Expect(Dialog).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Expect(Page.Locator("#ex-pick-result")).ToHaveTextAsync("Closed by code");
    }

    [Test]
    public async Task Progress_ShowsEachReport_ThenCloses()
    {
        await GoToPage("examples");

        await Page.Locator("#ex-progress").ClickAsync();
        var progress = Page.Locator(".nx-dialog-progress");
        await Expect(progress.Locator("[role=status]")).ToContainTextAsync("File 2 of 4");
        await Expect(progress.Locator(".nx-window-close")).ToHaveCountAsync(0);
        await Expect(progress).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Expect(Page.Locator("#ex-progress-result")).ToHaveTextAsync("Imported 4 files");
    }

    [TestCase(390)]
    [TestCase(1280)]
    public async Task Markup_CompactCardAtAnyWidth_Centered(int width)
    {
        await Page.SetViewportSizeAsync(width, 800);
        await GoToPage("examples");

        await Page.Locator("#ex-markup").ClickAsync();
        var dialog = Page.Locator("#ex-review");
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.EvaluateAsync("e => Promise.all(e.getAnimations().map(a => a.finished))");

        var box = (await dialog.BoundingBoxAsync())!;
        var area = await FixedArea.SizeAsync(Page);
        Assert.That(box.Width, Is.LessThanOrEqualTo(Math.Min(460, area.Width - 16) + 1));
        Assert.That(box.X + box.Width / 2, Is.EqualTo(area.Width / 2).Within(2));
        Assert.That(box.Height, Is.LessThan(area.Height - 16));

        await Page.Locator("#ex-review-ok").ClickAsync();
        await Expect(Page.Locator("#ex-markup-result")).ToHaveTextAsync("Approved, crew emailed");
    }
}
