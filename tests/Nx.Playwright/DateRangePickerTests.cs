using System.Globalization;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class DateRangePickerTests : PageTest
{
    private readonly string _baseUrl;

    public DateRangePickerTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Input(string id) => Page.Locator($"#{id}");
    private ILocator Toggle(string id) => Page.Locator($".nx-field:has(#{id}) .nx-daterange-toggle");
    private ILocator Popup => Page.Locator(".nx-daterange-popup");
    private ILocator Sheet => Page.Locator(".nx-sheet");

    private async Task GoToPage(string page = "examples")
    {
        await Page.GotoAsync(_baseUrl + $"/daterange/{page}");
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task TwoMonths_PickStartThenEnd()
    {
        await GoToPage();

        await Toggle("range-basic-start").ClickAsync();
        await Expect(Popup.Locator("table.nx-calendar-month")).ToHaveCountAsync(2);
        await Popup.Locator("td[data-date='2026-11-02']").ClickAsync();
        await Expect(Input("range-basic-end")).ToBeFocusedAsync();
        await Expect(Page.Locator("#range-basic-value")).ToHaveTextAsync("Range: 2026-11-02 → null");

        await Popup.Locator("td[data-date='2026-11-06']").ClickAsync();
        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Page.Locator("#range-basic-value")).ToHaveTextAsync("Range: 2026-11-02 → 2026-11-06");
    }

    [Test]
    public async Task Band_FillsBetweenStartAndEnd()
    {
        await GoToPage();

        await Toggle("range-basic-end").ClickAsync();

        await Expect(Popup.Locator("td[data-date='2026-10-19']")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-calendar-range-start"));
        await Expect(Popup.Locator("td.nx-calendar-in-range")).ToHaveCountAsync(3);
        await Expect(Popup.Locator("td[data-date='2026-10-23']")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-calendar-range-end"));
    }

    [Test]
    public async Task Keyboard_TabBetweenFieldsKeepsCalendar_OutCloses()
    {
        await GoToPage();

        await Input("range-basic-start").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Popup).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Input("range-basic-end")).ToBeFocusedAsync();
        await Expect(Popup).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Popup).ToHaveCountAsync(0);
    }

    [Test]
    public async Task OutsideClick_Closes()
    {
        await GoToPage();

        await Toggle("range-basic-start").ClickAsync();
        await Expect(Popup).ToBeVisibleAsync();
        // An empty spot right of the page title: clear of the popup and the sticky top bar.
        await Page.Mouse.ClickAsync(1200, 150);

        await Expect(Popup).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Typed_StartAfterEnd_Swaps()
    {
        await GoToPage();

        var start = Input("range-typed-start");
        await start.ScrollIntoViewIfNeededAsync();
        await start.ClickAsync();
        await start.FillAsync("");
        await start.PressSequentiallyAsync("20102026");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#range-typed-value")).ToHaveTextAsync("Range: 2026-10-09 → 2026-10-20");
        await Expect(start).ToHaveValueAsync("09.10.2026");
    }

    [Test]
    public async Task Limits_SundaysDisabled()
    {
        await GoToPage();

        await Toggle("range-limits-start").ScrollIntoViewIfNeededAsync();
        await Toggle("range-limits-start").ClickAsync();

        // The popup opens on today's month, clamped to Oct 1 – Nov 30 2026: test a Sunday inside that range.
        await Expect(Popup.Locator("td[data-date]").First).ToBeVisibleAsync();
        var dates = await Popup.Locator("td[data-date]").EvaluateAllAsync<string[]>("tds => tds.map(td => td.dataset.date)");
        var sunday = dates.Select(d => DateOnly.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture))
            .First(d => d.DayOfWeek == DayOfWeek.Sunday && d >= new DateOnly(2026, 10, 1) && d <= new DateOnly(2026, 11, 30));
        await Expect(Popup.Locator($"td[data-date='{sunday:yyyy-MM-dd}']")).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(Popup.Locator("th.nx-calendar-week").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task DateTime_KeepsEachTime()
    {
        await GoToPage();

        await Toggle("range-datetime-start").ScrollIntoViewIfNeededAsync();
        await Toggle("range-datetime-start").ClickAsync();
        await Popup.Locator("td[data-date='2026-11-03']").ClickAsync();
        await Popup.Locator("td[data-date='2026-11-05']").ClickAsync();

        await Expect(Page.Locator("#range-datetime-value")).ToHaveTextAsync("Range: 2026-11-03 15:00 → 2026-11-05 11:00");
    }

    [Test]
    public async Task Sheet_WalksStartThenEnd()
    {
        await GoToPage();

        await Toggle("range-sheet-start").ScrollIntoViewIfNeededAsync();
        await Toggle("range-sheet-start").ClickAsync();
        await Expect(Sheet).ToBeVisibleAsync();
        await Expect(Sheet.Locator(".nx-daterange-sheet-hint")).ToHaveTextAsync("Pick the start date");
        var days = Sheet.Locator("td[data-date]:not(.nx-calendar-other)");
        await days.Nth(4).ClickAsync();
        await Expect(Sheet.Locator(".nx-daterange-sheet-hint")).ToHaveTextAsync("Pick the end date");
        await days.Nth(8).ClickAsync();

        await Expect(Sheet).ToHaveCountAsync(0);
        await Expect(Page.Locator("#range-sheet-value")).Not.ToContainTextAsync("null");
    }

    [Test]
    public async Task Validation_RequiredAndMaxLength()
    {
        await GoToPage();

        await Page.Locator("#range-submit").ScrollIntoViewIfNeededAsync();
        await Page.Locator("#range-submit").ClickAsync();
        await Expect(Page.Locator("#range-form .validation-message").First).ToHaveTextAsync("Pick the first day.");

        await Input("leave-start").FillAsync("10/01/2026");
        await Input("leave-end").FillAsync("10/30/2026");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Locator("#range-submit").ClickAsync();
        await Expect(Page.Locator("#range-form .validation-message")).ToHaveTextAsync("Leave is at most 14 days.");
        await Expect(Input("leave-end")).ToHaveAttributeAsync("aria-invalid", "true");
    }

    [Test]
    public async Task Phone_FieldsFitAndSheetIsOneMonth()
    {
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, HasTouch = true, IsMobile = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_baseUrl + "/daterange/overview");
        await Expect(page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();

        Assert.That(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
        await page.Locator(".nx-field:has(#overview-range-start) .nx-daterange-toggle").TapAsync();

        await Expect(page.Locator(".nx-sheet table.nx-calendar-month")).ToHaveCountAsync(1);
    }
}
