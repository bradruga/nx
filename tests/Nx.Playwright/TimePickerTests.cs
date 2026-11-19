using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class TimePickerTests : PageTest
{
    private readonly string _baseUrl;

    public TimePickerTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Input(string id) => Page.Locator($"#{id}");
    private ILocator Toggle(string id) => Page.Locator($".nx-timepicker:has(#{id}) .nx-timepicker-toggle");
    private ILocator Popup => Page.Locator(".nx-timepicker-popup");
    private ILocator Sheet => Page.Locator(".nx-sheet");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/timepicker/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Typed_LooseForm_FormatsInEachClock()
    {
        await GoToPage();

        await Input("time-12").FillAsync("930p");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Input("time-12")).ToHaveValueAsync("9:30 PM");
        await Expect(Page.Locator("#time-12-value")).ToHaveTextAsync("Value: 21:30");
        await Expect(Input("time-24")).ToHaveValueAsync("21:30");
    }

    [Test]
    public async Task Mask_24Hour()
    {
        await GoToPage();

        var input = Input("time-24");
        await input.ClickAsync();
        await input.FillAsync("");
        await input.PressSequentiallyAsync("0745");
        await Expect(input).ToHaveValueAsync("07:45");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#time-12-value")).ToHaveTextAsync("Value: 07:45");
    }

    [Test]
    public async Task List_StepAndLimits_ArrowsEnterPick()
    {
        await GoToPage();

        var input = Input("time-step");
        await input.ScrollIntoViewIfNeededAsync();
        await input.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Popup.Locator("[role=option]").First).ToHaveTextAsync("6:00 AM");
        await Expect(Popup.Locator("[role=option]").Last).ToHaveTextAsync("6:00 PM");
        await Expect(Popup.Locator("[role=option]")).ToHaveCountAsync(49);
        await Expect(Popup.Locator(".nx-timepicker-item-active")).ToHaveTextAsync("7:00 AM");

        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Page.Locator("#time-step-value")).ToHaveTextAsync("Value: 07:30");
    }

    [Test]
    public async Task List_ActiveScrollsIntoView()
    {
        await GoToPage();

        await Toggle("time-step").ScrollIntoViewIfNeededAsync();
        await Toggle("time-step").ClickAsync();
        await Page.Keyboard.PressAsync("PageDown");
        await Page.Keyboard.PressAsync("PageDown");
        await Page.Keyboard.PressAsync("PageDown");

        await Expect(Popup.Locator(".nx-timepicker-item-active")).ToBeInViewportAsync();
    }

    [Test]
    public async Task Click_Picks_OutsideClickCloses()
    {
        await GoToPage("overview");

        await Toggle("overview-time").ClickAsync();
        await Popup.GetByRole(AriaRole.Option, new() { Name = "8:45 AM", Exact = true }).ClickAsync();
        await Expect(Page.Locator("#overview-time-value")).ToHaveTextAsync("Value: 08:45");
        await Expect(Popup).ToHaveCountAsync(0);

        await Toggle("overview-time").ClickAsync();
        await Expect(Popup).ToBeVisibleAsync();
        await Page.Locator("h1").ClickAsync();
        await Expect(Popup).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Types_TimeSpanAndDateTime()
    {
        await GoToPage();

        await Input("type-timespan").FillAsync("10:15 AM");
        await Input("type-datetime").FillAsync("7:45 AM");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator("#type-timespan-value")).ToHaveTextAsync("Value: 10:15");
        await Expect(Page.Locator("#type-datetime-value")).ToHaveTextAsync("Value: 2026-10-23 07:45");
    }

    [Test]
    public async Task Types_DateTimeOffset_KeepsItsDateAndOffset()
    {
        await GoToPage();

        await Expect(Input("type-offset")).ToHaveValueAsync("4:00 PM");
        await Input("type-offset").FillAsync("7:45 AM");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#type-offset-value")).ToHaveTextAsync("Value: 2026-10-23 07:45 +09:00");

        await Page.Keyboard.PressAsync("Shift+Tab");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Popup.GetByRole(AriaRole.Option, new() { Name = "9:30 AM", Exact = true }).ClickAsync();
        await Expect(Page.Locator("#type-offset-value")).ToHaveTextAsync("Value: 2026-10-23 09:30 +09:00");
    }

    [Test]
    public async Task Sheet_Columns_TapHourMinuteAndPeriod_DonePicks()
    {
        await GoToPage();

        await Toggle("time-sheet").ScrollIntoViewIfNeededAsync();
        await Toggle("time-sheet").ClickAsync();
        await Expect(Sheet).ToBeVisibleAsync();
        Assert.That(await Page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('nx-scroll-locked')"), Is.True);
        await Expect(Sheet.Locator(".nx-timepicker-item")).ToHaveCountAsync(0);
        await Expect(Sheet.GetByRole(AriaRole.Listbox)).ToHaveCountAsync(3);

        // Step is 30 here; :35 is on the minute column all the same.
        foreach (var (column, text) in new[] { ("Hour", "1"), ("Minute", "35"), ("AM or PM", "PM") })
        {
            var option = Sheet.GetByRole(AriaRole.Listbox, new() { Name = column, Exact = true }).GetByRole(AriaRole.Option, new() { Name = text, Exact = true });
            await option.ScrollIntoViewIfNeededAsync();
            Assert.That((await option.BoundingBoxAsync())!.Height, Is.GreaterThanOrEqualTo(44));
            await option.ClickAsync();
            await Expect(option).ToHaveAttributeAsync("aria-selected", "true");
        }
        await Expect(Sheet.Locator(".nx-timepicker-sheet-value")).ToHaveTextAsync("1:35 PM");
        await Expect(Page.Locator("#time-sheet-value")).ToHaveTextAsync("Value: null");

        await Sheet.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();

        await Expect(Sheet).ToHaveCountAsync(0);
        await Expect(Page.Locator("#time-sheet-value")).ToHaveTextAsync("Value: 13:35");
        await Expect(Toggle("time-sheet")).ToBeFocusedAsync();
        Assert.That(await Page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('nx-scroll-locked')"), Is.False);
    }

    private ILocator SheetOption(string column, string text) =>
        Sheet.GetByRole(AriaRole.Listbox, new() { Name = column, Exact = true }).GetByRole(AriaRole.Option, new() { Name = text, Exact = true });

    [Test]
    public async Task Sheet_Columns_Keyboard_ArrowsWithinAColumn_TabBetweenThem_EnterSets()
    {
        await GoToPage();
        await Input("time-sheet").ScrollIntoViewIfNeededAsync();
        await Input("time-sheet").FillAsync("9:30 AM");
        await Input("time-sheet").PressAsync("Enter");
        await Expect(Page.Locator("#time-sheet-value")).ToHaveTextAsync("Value: 09:30");

        await Toggle("time-sheet").ClickAsync();
        await Expect(SheetOption("Hour", "9")).ToBeFocusedAsync();

        // Arrows move through the hours without setting them; Enter sets.
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(SheetOption("Hour", "10")).ToBeFocusedAsync();
        await Expect(SheetOption("Hour", "9")).ToHaveAttributeAsync("aria-selected", "true");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(SheetOption("Hour", "10")).ToHaveAttributeAsync("aria-selected", "true");

        // One tab stop a column: Tab lands on the selected minute, then AM/PM, then Done.
        await Page.Keyboard.PressAsync("Tab");
        await Expect(SheetOption("Minute", "30")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(SheetOption("Minute", "25")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Space");
        await Expect(SheetOption("Minute", "25")).ToHaveAttributeAsync("aria-selected", "true");

        await Page.Keyboard.PressAsync("Tab");
        await Expect(SheetOption("AM or PM", "AM")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("End");
        await Expect(SheetOption("AM or PM", "PM")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Sheet.Locator(".nx-timepicker-sheet-value")).ToHaveTextAsync("10:25 PM");

        await Page.Keyboard.PressAsync("Tab");
        await Expect(Sheet.GetByRole(AriaRole.Button, new() { Name = "Done" })).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Sheet).ToHaveCountAsync(0);
        await Expect(Page.Locator("#time-sheet-value")).ToHaveTextAsync("Value: 22:25");
    }

    [Test]
    public async Task Sheet_Columns_EscapeDiscards()
    {
        await GoToPage();
        await Input("time-sheet").ScrollIntoViewIfNeededAsync();
        await Input("time-sheet").FillAsync("9:30 AM");
        await Input("time-sheet").PressAsync("Enter");

        await Toggle("time-sheet").ClickAsync();
        await Expect(SheetOption("Hour", "9")).ToBeFocusedAsync();
        await SheetOption("Hour", "4").ClickAsync();
        await Expect(Sheet.Locator(".nx-timepicker-sheet-value")).ToHaveTextAsync("4:30 AM");
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Sheet).ToHaveCountAsync(0);
        await Expect(Page.Locator("#time-sheet-value")).ToHaveTextAsync("Value: 09:30");
        await Expect(Input("time-sheet")).ToHaveValueAsync("9:30 AM");
    }

    [Test]
    public async Task Validation_CrossField()
    {
        await GoToPage();

        await Input("visit-start").FillAsync("10:00 AM");
        await Input("visit-end").FillAsync("9:00 AM");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Locator("#time-submit").ClickAsync();
        await Expect(Page.Locator("#time-form .validation-message")).ToHaveTextAsync("Leave after arriving.");

        await Input("visit-end").FillAsync("11:30 AM");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Locator("#time-submit").ClickAsync();
        await Expect(Page.Locator("#time-result")).ToHaveTextAsync("Booked 10:00–11:30");
    }

    [Test]
    public async Task Phone_AutoModeUsesSheet()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/timepicker/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        await page.Locator(".nx-timepicker:has(#overview-time) .nx-timepicker-toggle").TapAsync();

        var sheet = page.Locator(".nx-sheet");
        await Expect(sheet).ToBeVisibleAsync();
        await Expect(sheet.Locator(".nx-timecolumns-hours .nx-timecolumns-item-selected")).ToBeFocusedAsync();
        await Expect(sheet.Locator(".nx-timepicker-sheet-value")).ToHaveTextAsync("7:15 AM");

        // The three columns share the screen, each standing on its selected option; 12 hours and 12 minutes need no scrolling.
        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
        var body = (await sheet.Locator(".nx-sheet-body").BoundingBoxAsync())!;
        foreach (var selected in await sheet.Locator(".nx-timecolumns-item-selected").AllAsync())
        {
            var box = (await selected.BoundingBoxAsync())!;
            Assert.That(box.Height, Is.GreaterThanOrEqualTo(44));
            Assert.That(box.Y, Is.GreaterThanOrEqualTo(body.Y));
            Assert.That(box.Y + box.Height, Is.LessThanOrEqualTo(body.Y + body.Height));
        }
        Assert.That(await sheet.Locator(".nx-timecolumns-hours").EvaluateAsync<bool>("el => el.scrollHeight <= el.clientHeight"), Is.True);
        Assert.That(await sheet.Locator(".nx-timecolumns-minutes").EvaluateAsync<bool>("el => el.scrollHeight <= el.clientHeight"), Is.True);

        await sheet.Locator(".nx-timecolumns-minutes").GetByRole(AriaRole.Option, new() { Name = "20", Exact = true }).TapAsync();
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Done" }).TapAsync();
        await Expect(page.Locator("#overview-time-value")).ToHaveTextAsync("Value: 07:20");
    }

    [Test]
    public async Task Phone_24Hour_HourColumnScrolls_MinMaxDisable()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/timepicker/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        // Shift starts: 24-hour, 05:00 to 09:00, value 06:00.
        await page.Locator(".nx-timepicker:has(#overview-shift) .nx-timepicker-toggle").TapAsync();
        var sheet = page.Locator(".nx-sheet");
        await Expect(sheet.GetByRole(AriaRole.Listbox)).ToHaveCountAsync(2);
        var hours = sheet.Locator(".nx-timecolumns-hours");
        await Expect(hours.GetByRole(AriaRole.Option, new() { Name = "06", Exact = true })).ToBeFocusedAsync();
        await Expect(hours.GetByRole(AriaRole.Option, new() { Name = "04", Exact = true })).ToBeDisabledAsync();
        await Expect(hours.GetByRole(AriaRole.Option, new() { Name = "10", Exact = true })).ToBeDisabledAsync();

        // 09 at :00 is the last time: its other minutes are out.
        await hours.GetByRole(AriaRole.Option, new() { Name = "09", Exact = true }).TapAsync();
        await Expect(sheet.Locator(".nx-timecolumns-minutes").GetByRole(AriaRole.Option, new() { Name = "05", Exact = true })).ToBeDisabledAsync();
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Done" }).TapAsync();
        await Expect(page.Locator("#overview-shift-value")).ToHaveTextAsync("Value: 09:00");
    }
}
