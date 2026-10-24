using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class DateTimePickerTests : PageTest
{
    private readonly string _baseUrl;

    public DateTimePickerTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/datetimepicker/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    private ILocator Popup => Page.Locator(".nx-datetimepicker-popup");

    [Test]
    public async Task Popup_CalendarBesideTimes_UnderTheField_SelectedTimeInView()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await GoToPage("overview");

        var input = Page.Locator("#overview-start");
        await input.EvaluateAsync("el => window.scrollBy(0, el.getBoundingClientRect().top - 200)");
        await Page.Locator(".nx-field:has(#overview-start) .nx-datetimepicker-toggle").ClickAsync();
        await Expect(Popup).ToBeVisibleAsync();
        await Expect(input).ToBeFocusedAsync();

        var box = (await Page.Locator(".nx-field:has(#overview-start) .nx-field-box").BoundingBoxAsync())!;
        var popup = (await Popup.BoundingBoxAsync())!;
        Assert.That(popup.Y, Is.EqualTo(box.Y + box.Height + 4).Within(3));

        var calendar = (await Popup.Locator(".nx-calendar").BoundingBoxAsync())!;
        var times = (await Popup.Locator(".nx-datetimepicker-time-list").BoundingBoxAsync())!;
        Assert.That(times.X, Is.GreaterThanOrEqualTo(calendar.X + calendar.Width - 1));

        var selected = (await Popup.Locator(".nx-timepicker-item-selected").BoundingBoxAsync())!;
        Assert.That(selected.Y, Is.GreaterThanOrEqualTo(times.Y - 1));
        Assert.That(selected.Y + selected.Height, Is.LessThanOrEqualTo(times.Y + times.Height + 1));
    }

    [Test]
    public async Task Mouse_PickDayKeepsTime_PickTimeCloses()
    {
        await GoToPage("overview");

        await Page.Locator("#overview-start").ClickAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Popup).ToBeVisibleAsync();
        var before = await Page.Locator("#overview-value").TextContentAsync();

        await Popup.Locator(".nx-calendar-today-button").ClickAsync();
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync(new Regex(@$"Value: {DateTime.Today:yyyy-MM-dd} 07:00"));
        await Expect(Popup).ToBeVisibleAsync();
        Assert.That(before, Does.EndWith("07:00"));

        await Popup.Locator(".nx-timepicker-item", new() { HasText = "9:45 AM" }).ClickAsync();
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync($"Value: {DateTime.Today:yyyy-MM-dd} 09:45");
        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Page.Locator("#overview-start")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Keyboard_DayThenTime()
    {
        await GoToPage("overview");

        var input = Page.Locator("#overview-start");
        await input.ClickAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Popup).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("Enter");
        var day = DateTime.Today.AddDays(2);
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync($"Value: {day:yyyy-MM-dd} 07:00");
        await Expect(input).ToHaveAttributeAsync("aria-activedescendant", new Regex("-time-"));

        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync($"Value: {day:yyyy-MM-dd} 07:15");
        await Expect(Popup).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Typing_FreeFormatReadLeniently()
    {
        await GoToPage();

        var input = Page.Locator("#clear-nullable");
        await input.FillAsync("3/5/2026 2pm");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#clear-nullable-value")).ToHaveTextAsync("Value: 3/5/2026 2:00 PM");
        await Expect(input).ToHaveValueAsync("3/5/2026 2:00 PM");
    }

    [Test]
    public async Task Typing_MaskedFormat()
    {
        await GoToPage();

        var input = Page.Locator("#format-masked");
        await input.ClickAsync();
        await input.PressAsync("Control+A");
        await input.PressSequentiallyAsync("031520260930");
        await Expect(input).ToHaveValueAsync("03/15/2026 09:30");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(input).ToHaveValueAsync("03/15/2026 09:30");
    }

    [Test]
    public async Task MinMax_TypedValueMovedToTheLimit()
    {
        await GoToPage();

        var input = Page.Locator("#injury-time");
        await input.FillAsync(DateTime.Today.AddDays(3).ToString("M/d/yyyy") + " 9:00 AM");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(input).ToHaveValueAsync(DateTime.Today.AddHours(16).ToString("M/d/yyyy h:mm tt"));
    }

    [Test]
    public async Task Clear_ButtonEmptiesNullable()
    {
        await GoToPage();

        var input = Page.Locator("#clear-nullable");
        await input.FillAsync("3/5/2026 2:00 PM");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Locator(".nx-field:has(#clear-nullable) .nx-field-clear").ClickAsync();
        await Expect(Page.Locator("#clear-nullable-value")).ToHaveTextAsync("Value: null");
        await Expect(input).ToHaveValueAsync("");
    }

    [Test]
    public async Task Sheet_TabsStayOnTop_PickDayThenTime()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/datetimepicker/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        var field = page.Locator(".nx-field:has(#step-15)");
        await field.ScrollIntoViewIfNeededAsync();
        await field.Locator(".nx-datetimepicker-toggle").TapAsync();

        var sheet = page.Locator(".nx-datetimepicker-sheet");
        await Expect(sheet).ToBeVisibleAsync();
        var box = (await sheet.BoundingBoxAsync())!;
        Assert.That(box.Width, Is.EqualTo(390).Within(1));
        Assert.That(box.Height, Is.EqualTo(844).Within(1));

        await sheet.Locator(".nx-calendar-today-button").TapAsync();
        await Expect(sheet.Locator(".nx-datetimepicker-sheet-tab").Nth(1)).ToHaveAttributeAsync("aria-selected", "true");

        var tabs = sheet.Locator(".nx-datetimepicker-sheet-tabs");
        var header = (await sheet.Locator(".nx-sheet-header").BoundingBoxAsync())!;
        var tabsBox = (await tabs.BoundingBoxAsync())!;
        Assert.That(tabsBox.Y, Is.GreaterThanOrEqualTo(header.Y + header.Height - 1));

        var item = sheet.Locator(".nx-datetimepicker-sheet-list .nx-timepicker-item", new() { HasText = "6:15 PM" });
        await item.ScrollIntoViewIfNeededAsync();
        Assert.That((await item.BoundingBoxAsync())!.Height, Is.GreaterThanOrEqualTo(44));
        await item.TapAsync();

        await Expect(sheet).ToHaveCountAsync(0);
        await Expect(page.Locator("#step-15")).ToHaveValueAsync(DateTime.Today.AddHours(18).AddMinutes(15).ToString("M/d/yyyy h:mm tt"));
        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
    }

    [Test]
    public async Task Validation_RequiredThenSchedules()
    {
        await GoToPage();

        await Page.Locator("#datetime-submit").ClickAsync();
        await Expect(Page.Locator("#datetime-form .nx-validation-message")).ToHaveTextAsync("Pick when the meeting starts.");
        await Expect(Page.Locator("#meeting-start")).ToHaveAttributeAsync("aria-invalid", "true");

        await Page.Locator("#meeting-start").FillAsync("3/5/2026 2:00 PM");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Locator("#datetime-submit").ClickAsync();
        await Expect(Page.Locator("#datetime-result")).ToHaveTextAsync("Scheduled for 3/5/2026 2:00 PM");
    }
}
