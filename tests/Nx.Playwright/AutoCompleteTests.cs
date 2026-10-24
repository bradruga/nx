using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class AutoCompleteTests : PageTest
{
    private readonly string _baseUrl;

    public AutoCompleteTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/autocomplete/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Items => Page.Locator(".nx-combobox-popup .nx-combobox-item");

    [Test]
    public async Task Typing_Suggests_UnderTheField_NothingHighlighted()
    {
        await GoToPage("overview");

        var input = Page.Locator("#overview-city");
        await input.PressSequentiallyAsync("port");
        await Expect(Items).ToHaveCountAsync(3);
        await Expect(Page.Locator(".nx-combobox-item-active")).ToHaveCountAsync(0);

        var field = (await Page.Locator(".nx-autocomplete .nx-combobox-field").First.BoundingBoxAsync())!;
        var popup = (await Page.Locator(".nx-combobox-popup").BoundingBoxAsync())!;
        Assert.That(popup.Y, Is.EqualTo(field.Y + field.Height + 4).Within(3));
        Assert.That(popup.Width, Is.EqualTo(field.Width).Within(2));
        await Expect(Page.Locator(".nx-combobox-popup mark.nx-combobox-match").First).ToHaveTextAsync("Port", new() { IgnoreCase = true });
    }

    [Test]
    public async Task Enter_KeepsTypedText_ArrowEnterPicks()
    {
        await GoToPage("overview");

        var input = Page.Locator("#overview-city");
        await input.PressSequentiallyAsync("Port");
        await Expect(Items).ToHaveCountAsync(3);
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: Port");
        await Expect(Items).ToHaveCountAsync(0);

        await input.PressSequentiallyAsync("s");
        await Expect(Items).ToHaveCountAsync(1);
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: Portsmouth");
        await Expect(input).ToHaveValueAsync("Portsmouth");
    }

    [Test]
    public async Task FreeText_CommittedOnLeaving_EscapeKeepsIt()
    {
        await GoToPage("overview");

        var input = Page.Locator("#overview-city");
        await input.PressSequentiallyAsync("Sale");
        await Expect(Items).ToHaveCountAsync(1);
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Items).ToHaveCountAsync(0);
        await Expect(input).ToHaveValueAsync("Sale");

        await input.PressSequentiallyAsync("mburg");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: Salemburg");
    }

    [Test]
    public async Task NoArrowButton_ClickDoesNotOpen()
    {
        await GoToPage("overview");

        await Expect(Page.Locator(".nx-autocomplete .nx-combobox-toggle")).ToHaveCountAsync(0);
        await Page.Locator("#overview-city").ClickAsync();
        await Expect(Page.Locator(".nx-combobox-popup")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ItemTemplate_PickSetsTextAndRunsAfter()
    {
        await GoToPage();

        var input = Page.Locator("#places-city");
        await input.ScrollIntoViewIfNeededAsync();
        await input.PressSequentiallyAsync("po");
        await Expect(Items).ToHaveCountAsync(2);
        await Expect(Items.First).ToContainTextAsync("Cumberland County");
        await Items.Nth(1).ClickAsync();

        await Expect(Page.Locator("#places-value")).ToHaveTextAsync("Town: Portsmouth · County: Rockingham County");
    }

    [Test]
    public async Task MinLength_ShutUntilThree()
    {
        await GoToPage();

        var input = Page.Locator("#job-number");
        await input.ScrollIntoViewIfNeededAsync();
        await input.PressSequentiallyAsync("55");
        await Page.WaitForTimeoutAsync(200);
        await Expect(Page.Locator(".nx-combobox-popup")).ToHaveCountAsync(0);
        await input.PressSequentiallyAsync("1");
        await Expect(Items.First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Prefix_IconBeforeText()
    {
        await GoToPage();

        var icon = (await Page.Locator(".nx-autocomplete:has(#search-box) .nx-combobox-prefix svg").BoundingBoxAsync())!;
        var input = (await Page.Locator("#search-box").BoundingBoxAsync())!;
        Assert.That(icon.X + icon.Width, Is.LessThanOrEqualTo(input.X + 1));
    }

    [Test]
    public async Task Provider_LoadsAfterAPause()
    {
        await GoToPage();

        var input = Page.Locator("#provider-customer");
        await input.ScrollIntoViewIfNeededAsync();
        await input.PressSequentiallyAsync("bald", new() { Delay = 30 });
        await Expect(Items).ToHaveCountAsync(1, new() { Timeout = 5000 });
        await Expect(Page.Locator("#provider-calls")).ToHaveTextAsync("Provider calls: 1");
    }

    [Test]
    public async Task Virtualized_LongListStaysSmall()
    {
        await GoToPage();

        var input = Page.Locator("#part-search");
        await input.ScrollIntoViewIfNeededAsync();
        await input.PressSequentiallyAsync("vault");
        await Expect(Items.First).ToBeVisibleAsync();
        Assert.That(await Items.CountAsync(), Is.LessThan(60));
    }

    [Test]
    public async Task Validation_RequiredAndLength()
    {
        await GoToPage();

        await Page.Locator("#autocomplete-submit").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#autocomplete-submit").ClickAsync();
        await Expect(Page.Locator("#autocomplete-form .nx-validation-message")).ToHaveTextAsync("Enter the site's city.");

        await Page.Locator("#site-city").PressSequentiallyAsync("Burl");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");
        await Page.Locator("#autocomplete-submit").ClickAsync();
        await Expect(Page.Locator("#autocomplete-result")).ToHaveTextAsync("Saved Burlington");
    }

    [Test]
    public async Task Phone_PopupUnderTheField_NoPageScroll()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/autocomplete/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        var input = page.Locator("#overview-city");
        await input.TapAsync();
        await input.PressSequentiallyAsync("an");
        var popup = page.Locator(".nx-combobox-popup");
        await Expect(popup).ToBeVisibleAsync();
        await Expect(page.Locator(".nx-combobox-overlay")).ToHaveCountAsync(0);
        var box = (await popup.BoundingBoxAsync())!;
        Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(390));
        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
    }
}
