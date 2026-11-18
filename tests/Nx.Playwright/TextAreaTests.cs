using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class TextAreaTests : PageTest
{
    private readonly string _baseUrl;

    public TextAreaTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Area(string id) => Page.Locator($"#{id}");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/textarea/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private async Task<float> HeightAsync(string id) => (await Area(id).BoundingBoxAsync())!.Height;

    [Test]
    public async Task AutoResize_GrowsToMaxRowsThenScrolls()
    {
        await GoToPage();

        var area = Area("auto-grow");
        await area.ScrollIntoViewIfNeededAsync();
        var start = await HeightAsync("auto-grow");
        await area.ClickAsync();
        await area.PressSequentiallyAsync("one\ntwo\nthree\nfour");
        var four = await HeightAsync("auto-grow");
        Assert.That(four - start, Is.EqualTo(40).Within(2));          // two more 20 px lines than MinRows

        await area.PressSequentiallyAsync("\nfive\nsix\nseven\neight");
        var capped = await HeightAsync("auto-grow");
        Assert.That(capped - start, Is.EqualTo(80).Within(2));        // MaxRows 6
        Assert.That(await area.EvaluateAsync<string>("t => getComputedStyle(t).overflowY"), Is.EqualTo("auto"));
        await Expect(Page.Locator("#auto-lines")).ToHaveTextAsync("Lines: 8");
    }

    [Test]
    public async Task FastTyping_LosesNothing()
    {
        await GoToPage();

        var area = Area("bind-change");
        await area.ScrollIntoViewIfNeededAsync();
        await area.ClickAsync();
        await area.PressSequentiallyAsync("The quick brown fox\njumps over the lazy dog", new() { Delay = 0 });
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator("#bind-change-value")).ToHaveTextAsync("Value: The quick brown fox\njumps over the lazy dog");
    }

    [Test]
    public async Task OnChange_NotWhileTyping_DebounceAfterPause()
    {
        await GoToPage();

        var change = Area("bind-change");
        await change.ScrollIntoViewIfNeededAsync();
        await change.FillAsync("abc");
        await Expect(Page.Locator("#bind-change-value")).ToHaveTextAsync("Value:");

        var debounced = Area("bind-debounce");
        await debounced.ClickAsync();
        await debounced.PressSequentiallyAsync("xyz");
        await Expect(Page.Locator("#bind-debounce-value")).ToHaveTextAsync("Value: xyz", new() { Timeout = 3000 });
        await Expect(Page.Locator("#bind-change-value")).ToHaveTextAsync("Value: abc");
    }

    [Test]
    public async Task ResizeHandles()
    {
        await GoToPage();

        Assert.That(await Area("rows-two").EvaluateAsync<string>("t => getComputedStyle(t).resize"), Is.EqualTo("vertical"));
        Assert.That(await Area("rows-both").EvaluateAsync<string>("t => getComputedStyle(t).resize"), Is.EqualTo("both"));
        Assert.That(await Area("rows-none").EvaluateAsync<string>("t => getComputedStyle(t).resize"), Is.EqualTo("none"));
        Assert.That(await HeightAsync("rows-both"), Is.GreaterThan(await HeightAsync("rows-two")));
    }

    [Test]
    public async Task Counter_Clear_Enter()
    {
        await GoToPage();

        var counter = Area("counter-area");
        await counter.ScrollIntoViewIfNeededAsync();
        await counter.ClickAsync();
        await counter.PressSequentiallyAsync("line\nline");
        await Expect(Page.Locator(".nx-field:has(#counter-area) .nx-textarea-counter")).ToHaveTextAsync("9 / 140");

        await Page.Locator(".nx-field:has(#clear-area) .nx-field-clear").ClickAsync();
        await Expect(Area("clear-area")).ToHaveValueAsync("");
        await Expect(Area("clear-area")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Validation_RequiredAndLength()
    {
        await GoToPage();

        await Page.Locator("#textarea-submit").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#textarea-submit").ClickAsync();
        await Expect(Page.Locator("#textarea-form .validation-message")).ToHaveTextAsync("Give a reason.");
        await Expect(Area("report-reason")).ToHaveAttributeAsync("aria-invalid", "true");

        await Area("report-reason").FillAsync("Slump too high on the second truck");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Locator("#textarea-submit").ClickAsync();
        await Expect(Page.Locator("#textarea-result")).ToHaveTextAsync("Hold placed");
    }

    [Test]
    public async Task Phone_NoPageScroll_FullWidth()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/textarea/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
    }
}
