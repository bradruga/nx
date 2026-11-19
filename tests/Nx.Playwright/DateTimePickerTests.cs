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

    /// <summary>Opens the 15-minute picker and picks a day, so the time list (from midnight) takes the keys.</summary>
    private async Task<ILocator> OpenTimeListAsync()
    {
        var input = Page.Locator("#step-15");
        await input.ScrollIntoViewIfNeededAsync();
        await input.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Popup).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(input).ToHaveAttributeAsync("aria-activedescendant", "step-15-time-0");
        await Expect(Popup.Locator(".nx-timepicker-item")).ToHaveCountAsync(96);
        return input;
    }

    /// <summary>Waits until the time list's active option is the index-th and lies inside the list's box.</summary>
    private Task ExpectActiveTimeInView(int index) =>
        Page.WaitForFunctionAsync("""
            ({ index }) => {
                const list = document.querySelector('.nx-datetimepicker-popup .nx-datetimepicker-time-list');
                const items = [...list.querySelectorAll('.nx-timepicker-item')];
                const active = list.querySelector('.nx-timepicker-item-active');
                if (!active || items.indexOf(active) !== index) return false;
                const l = list.getBoundingClientRect(), a = active.getBoundingClientRect();
                return a.top >= l.top - 1 && a.bottom <= l.bottom + 1;
            }
            """, new { index });

    [Test]
    public async Task Keyboard_TimeList_KeepsTheActiveOptionInView()
    {
        await GoToPage();
        var input = await OpenTimeListAsync();

        var at = 0;
        var keys = new[] { ("PageDown", 6), ("PageDown", 6), ("PageDown", 6), ("ArrowDown", 1), ("PageDown", 6), ("PageDown", 6), ("ArrowDown", 1), ("ArrowDown", 1) }
            .Concat(Enumerable.Repeat(("PageDown", 6), 12)).Append(("PageUp", -6));
        foreach (var (key, step) in keys)
        {
            await Page.Keyboard.PressAsync(key);
            at = Math.Clamp(at + step, 0, 95);
            await ExpectActiveTimeInView(at);
        }

        await Page.Keyboard.PressAsync("Enter");
        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(input).ToHaveValueAsync(new Regex("10:15 PM$"));
    }

    [Test]
    public async Task Keyboard_TimeList_FastKeysOnASlowConnection_LastOptionInView()
    {
        await GoToPage();
        var cdp = await Page.Context.NewCDPSessionAsync(Page);
        await cdp.SendAsync("Network.enable");
        await cdp.SendAsync("Network.emulateNetworkConditions", new() { ["offline"] = false, ["latency"] = 120, ["downloadThroughput"] = -1, ["uploadThroughput"] = -1 });
        var input = await OpenTimeListAsync();

        for (var i = 0; i < 16; i++)
            await Page.Keyboard.PressAsync("PageDown");

        await Expect(input).ToHaveAttributeAsync("aria-activedescendant", "step-15-time-95");
        await ExpectActiveTimeInView(95);
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

        // The time is set on columns: hours and AM/PM fit the screen, minutes go by five whatever the step.
        var columns = sheet.Locator(".nx-timecolumns");
        await Expect(columns.GetByRole(AriaRole.Listbox)).ToHaveCountAsync(3);
        await Expect(columns.Locator(".nx-timecolumns-hours .nx-timecolumns-item-selected")).ToBeFocusedAsync();
        foreach (var (column, text) in new[] { ("hours", "6"), ("minutes", "15"), ("periods", "PM") })
        {
            var option = columns.Locator($".nx-timecolumns-{column}").GetByRole(AriaRole.Option, new() { Name = text, Exact = true });
            await option.ScrollIntoViewIfNeededAsync();
            Assert.That((await option.BoundingBoxAsync())!.Height, Is.GreaterThanOrEqualTo(44));
            await option.TapAsync();
            await Expect(option).ToHaveAttributeAsync("aria-selected", "true");
        }
        await Expect(sheet.Locator(".nx-datetimepicker-sheet-tab").Nth(1)).ToContainTextAsync("6:15 PM");
        Assert.That((await columns.BoundingBoxAsync())!.Y, Is.GreaterThanOrEqualTo(tabsBox.Y + tabsBox.Height - 1), "the columns sit under the tabs");
        var done = sheet.Locator(".nx-datetimepicker-sheet-done");
        var doneBox = (await done.BoundingBoxAsync())!;
        Assert.That(doneBox.Height, Is.GreaterThanOrEqualTo(44));
        Assert.That(doneBox.Y + doneBox.Height, Is.LessThanOrEqualTo(844));
        await done.TapAsync();

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

    // ── Edit text on focus ────────────────────────────────────────────────────

    [Test]
    public async Task Focus_TypedRightAway_OnASlowConnection_NothingLostOrOverwritten()
    {
        await GoToPage("playground");
        var input = Page.Locator("#playground-datetime");
        // Shown as 3/5/2026 14:30, edited behind the mask as 03/05/2026 14:30.
        await input.FillAsync("3/5/2026 2:30 PM");
        await input.PressAsync("Enter");
        await Page.Locator("#pg-format").FillAsync("M/d/yyyy HH:mm");
        await Page.Locator("#pg-format").PressAsync("Tab");
        await Expect(input).ToHaveValueAsync("3/5/2026 14:30");
        var cdp = await Page.Context.NewCDPSessionAsync(Page);
        await cdp.SendAsync("Network.enable");
        await cdp.SendAsync("Network.emulateNetworkConditions", new() { ["offline"] = false, ["latency"] = 250, ["downloadThroughput"] = -1, ["uploadThroughput"] = -1 });

        await input.FocusAsync();
        Assert.That(await input.InputValueAsync(), Is.EqualTo("03/05/2026 14:30"));
        await Page.Keyboard.TypeAsync("031520260930");
        Assert.That(await input.InputValueAsync(), Is.EqualTo("03/15/2026 09:30"));

        await Page.WaitForTimeoutAsync(1500);
        Assert.That(await input.InputValueAsync(), Is.EqualTo("03/15/2026 09:30"));
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: 2026-03-15 09:30:00");
    }

    // ── DateTimeOffset ────────────────────────────────────────────────────────

    [Test]
    public async Task DateTimeOffset_KeepsItsOffset_TypedAndPicked()
    {
        await GoToPage();
        var input = Page.Locator("#offset-kept");
        await input.ScrollIntoViewIfNeededAsync();
        await Expect(input).ToHaveValueAsync("10/23/2026 6:00 PM");

        await input.FillAsync("10/25/2026 8:00 AM");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#offset-kept-value")).ToHaveTextAsync("Value: 2026-10-25 08:00 +09:00");

        await Page.Keyboard.PressAsync("ArrowDown");
        await Popup.Locator("td[data-date='2026-10-28']").ClickAsync();
        await Popup.GetByRole(AriaRole.Option, new() { Name = "9:30 AM", Exact = true }).ClickAsync();
        await Expect(Page.Locator("#offset-kept-value")).ToHaveTextAsync("Value: 2026-10-28 09:30 +09:00");
    }

    [TestCase("America/Chicago", "7/9/2026 8:00 AM", "2026-07-09 08:00 -05:00")]
    [TestCase("America/Chicago", "12/9/2026 8:00 AM", "2026-12-09 08:00 -06:00")]
    [TestCase("Australia/Adelaide", "7/9/2026 8:00 AM", "2026-07-09 08:00 +09:30")]
    public async Task DateTimeOffset_Empty_TakesTheBrowsersOffsetAtThatTime(string timeZone, string typed, string expected)
    {
        await using var context = await Browser.NewContextAsync(new() { TimezoneId = timeZone });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/datetimepicker/examples");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        await page.Locator("#offset-empty").FillAsync(typed);
        await page.Keyboard.PressAsync("Enter");

        await Expect(page.Locator("#offset-empty-value")).ToHaveTextAsync($"Value: {expected}");
    }

    [Test]
    public async Task Playground_DateTimeOffset()
    {
        await GoToPage("playground");

        await Page.Locator("#pg-type").SelectOptionAsync("DateTimeOffset?");
        // The switch replaces the picker a round trip later; text filled into the old one would be lost.
        await Expect(Page.Locator("#playground-value")).ToHaveAttributeAsync("data-type", "DateTimeOffset?");
        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync("Value: null");
        await Page.Locator("#playground-datetime").FillAsync("3/5/2026 2:30 PM");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#playground-value")).ToHaveTextAsync(new Regex(@"^Value: 2026-03-05 14:30:00 [+-]\d\d:\d\d$"));
    }

    // ── Phone sheet: time columns ─────────────────────────────────────────────

    [Test]
    public async Task Sheet_TimeColumns_Keyboard_BackDiscards()
    {
        await GoToPage();
        var input = Page.Locator("#sheet-always");
        await input.ScrollIntoViewIfNeededAsync();
        await input.FillAsync("3/5/2026 9:30 AM");
        await input.PressAsync("Enter");
        var committed = await input.InputValueAsync();

        await Page.Locator(".nx-field:has(#sheet-always) .nx-datetimepicker-toggle").ClickAsync();
        var sheet = Page.Locator(".nx-datetimepicker-sheet");
        await sheet.GetByRole(AriaRole.Tab, new() { Name = "Time" }).ClickAsync();
        var hours = sheet.Locator(".nx-timecolumns-hours");
        await Expect(hours.GetByRole(AriaRole.Option, new() { Name = "9", Exact = true })).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(hours.GetByRole(AriaRole.Option, new() { Name = "10", Exact = true })).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(sheet.GetByRole(AriaRole.Tab, new() { Name = "Time" })).ToContainTextAsync("10:30 AM");

        await sheet.Locator(".nx-sheet-close").ClickAsync();
        await Expect(sheet).ToHaveCountAsync(0);
        await Expect(input).ToHaveValueAsync(committed);
    }
}
