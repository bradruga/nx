using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxChat: Enter to send and following the newest message need a real browser.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ChatTests : PageTest
{
    private readonly string _baseUrl;

    public ChatTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page)
    {
        await Page.GotoAsync($"{_baseUrl}/chat/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private Task<bool> AtBottom(string chat) =>
        Page.Locator($"#{chat} .nx-chat-messages").EvaluateAsync<bool>("l => l.scrollHeight - l.scrollTop - l.clientHeight < 2");

    [Test]
    public async Task Enter_Sends_ShiftEnter_NewLine()
    {
        await GoToPage("overview");
        var box = Page.Locator("#overview-chat-input");
        await box.ClickAsync();
        await Page.Keyboard.TypeAsync("Line one");
        await Page.Keyboard.PressAsync("Shift+Enter");
        await Page.Keyboard.TypeAsync("line two");
        await Page.Keyboard.PressAsync("Enter");

        var last = Page.Locator("#overview-chat .nx-chat-group-own .nx-chat-text").Last;
        await Expect(last).ToHaveTextAsync("Line one\nline two");
        await Expect(box).ToHaveValueAsync("");
        await Expect(box).ToBeFocusedAsync();
    }

    [Test]
    public async Task List_StartsAndStaysAtTheBottom()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("overview");
        await Page.WaitForFunctionAsync("() => { const l = document.querySelector('#overview-chat .nx-chat-messages'); return l.scrollHeight - l.scrollTop - l.clientHeight < 2; }");

        await Page.Locator("#overview-chat-input").FillAsync("Another one");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#overview-chat .nx-chat-text").Last).ToHaveTextAsync("Another one");
        Assert.That(await AtBottom("overview-chat"), Is.True);
    }

    [Test]
    public async Task ScrolledUp_StaysPut_WhileAnswersArrive()
    {
        await GoToPage("examples");
        var list = Page.Locator("#typing-chat .nx-chat-messages");
        var reply = "On it. I'll call when the trucks leave.";
        var answers = Page.Locator("#typing-chat .nx-chat-text").Filter(new() { HasTextString = reply });
        var before = await answers.CountAsync();
        for (var i = 0; i < 3; i++)
        {
            await Page.Locator("#typing-chat-input").FillAsync($"Message {i}");
            await Page.Keyboard.PressAsync("Enter");
            // Every answer has the same text: count them, or the next message goes out before this answer came.
            await Expect(answers).ToHaveCountAsync(before + i + 1);
        }

        await Page.Locator("#typing-chat-input").FillAsync("While I read");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#typing-chat .nx-chat-typing")).ToBeVisibleAsync();
        await list.EvaluateAsync("l => l.scrollTop = 0");
        await Expect(Page.Locator("#typing-chat .nx-chat-typing")).ToHaveCountAsync(0);

        Assert.That(await list.EvaluateAsync<double>("l => l.scrollTop"), Is.LessThan(5));
        Assert.That(await AtBottom("typing-chat"), Is.False);
    }

    [Test]
    public async Task Typing_DotsThenTheAnswer()
    {
        await GoToPage("examples");
        await Page.Locator("#typing-chat-input").FillAsync("Plant status?");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#typing-chat .nx-chat-typing")).ToBeVisibleAsync();
        await Expect(Page.Locator("#typing-chat .nx-chat-typing")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#typing-chat .nx-chat-text").Last).ToHaveTextAsync("On it. I'll call when the trucks leave.");
    }

    [Test]
    public async Task Assistant_ChoiceAsksTheQuestion()
    {
        await GoToPage("examples");
        await Page.Locator("#assistant-chat .demo-chat-choices button", new() { HasText = "When is the next pour?" }).ClickAsync();

        await Expect(Page.Locator("#assistant-chat .nx-chat-group-own").Last).ToContainTextAsync("When is the next pour?");
        await Expect(Page.Locator("#assistant-chat .nx-chat-bubble").Last).ToContainTextAsync("The next pour is P-14");
        await Expect(Page.Locator("#assistant-chat .demo-chat-choices")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Disabled_BoxAndSendOff()
    {
        await GoToPage("examples");
        await Expect(Page.Locator("#disabled-chat-input")).ToBeDisabledAsync();
        await Expect(Page.Locator("#disabled-chat .nx-chat-send")).ToBeDisabledAsync();
    }

    [Test]
    public async Task Playground_AuthorId_SwitchesSides()
    {
        await GoToPage("playground");
        var own = Page.Locator("#playground-chat .nx-chat-group-own");
        await Expect(own.First).ToContainTextAsync("Any bug holes");

        await Page.Locator("#pg-author").SelectOptionAsync("sam");
        await Expect(own.First).ToContainTextAsync("Trucks for the Route 9 pour");
    }
}
