using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Nx.Playwright;

/// <summary>NxSlider, NxChipList and NxDateInput.</summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class SliderChipDateInputTests : PageTest
{
    private readonly string _baseUrl;

    public SliderChipDateInputTests(string baseUrl) => _baseUrl = baseUrl;

    private async Task GoToPage(string path)
    {
        await Page.GotoAsync(_baseUrl + path);
        await Expect(Page.Locator(".layout-container[data-interactive=true]")).ToBeVisibleAsync();
    }

    // ── NxSlider ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Slider_KeysAndDrag_UpdateTheValue()
    {
        await GoToPage("/slider/overview");
        var slider = Page.Locator("#overview-slider");

        await slider.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: 5");
        await Page.Keyboard.PressAsync("End");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: 10");

        var box = (await slider.BoundingBoxAsync())!;
        await Page.Mouse.ClickAsync(box.X + 9, box.Y + box.Height / 2);
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: 0");
    }

    [Test]
    public async Task Slider_TicksSitUnderTheThumb()
    {
        await GoToPage("/slider/overview");
        var slider = Page.Locator("#overview-slider");
        await slider.FocusAsync();
        await Page.Keyboard.PressAsync("Home");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: 2");

        var thumbCenter = await slider.EvaluateAsync<double>(
            "e => { const r = e.getBoundingClientRect(); const t = 18; return r.left + t / 2 + (r.width - t) * (e.value - e.min) / (e.max - e.min); }");
        var tick = Page.Locator(".nx-slider-tick", new() { HasText = "2" }).First;
        var box = (await tick.BoundingBoxAsync())!;
        Assert.That(box.X + box.Width / 2, Is.EqualTo(thumbCenter).Within(1.5));
    }

    [Test]
    public async Task Slider_OnRelease_RaisesOnce()
    {
        await GoToPage("/slider/examples");
        var slider = Page.Locator("#release-slider");
        await slider.ScrollIntoViewIfNeededAsync();
        var box = (await slider.BoundingBoxAsync())!;

        await Page.Mouse.MoveAsync(box.X + box.Width * 0.4f, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + box.Width * 0.8f, box.Y + box.Height / 2, new() { Steps = 10 });
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator("#release-count")).ToHaveTextAsync("On release × 1");
    }

    [Test]
    public async Task Slider_Buttons_Step()
    {
        await GoToPage("/slider/examples");
        await Page.Locator("#buttons-slider").ScrollIntoViewIfNeededAsync();
        var plus = Page.Locator(".nx-slider", new() { Has = Page.Locator("#buttons-slider") }).GetByRole(AriaRole.Button, new() { Name = "Increase" });

        await plus.ClickAsync();
        await Expect(Page.Locator(".nx-slider", new() { Has = Page.Locator("#buttons-slider") }).Locator(".nx-slider-value")).ToHaveTextAsync("3");
    }

    // ── NxChipList ────────────────────────────────────────────────────────────

    [Test]
    public async Task Chips_ArrowsMove_DeleteRemoves_FocusStays()
    {
        await GoToPage("/chiplist/overview");
        var chips = Page.Locator("#overview-chips .nx-chiplist-chip");

        await chips.First.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(chips.Nth(1)).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Delete");

        await Expect(Page.Locator("#overview-count")).ToHaveTextAsync("4 tickets");
        await Expect(chips.Nth(1)).ToHaveTextAsync("2-11873");
        await Expect(chips.Nth(1)).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("End");
        await Page.Keyboard.PressAsync("Backspace");
        await Expect(chips.Last).ToBeFocusedAsync();
        await Expect(Page.Locator("#overview-count")).ToHaveTextAsync("3 tickets");
    }

    [Test]
    public async Task Chips_RemoveButton_AndOneTabStop()
    {
        await GoToPage("/chiplist/overview");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Remove 1-40212" }).ClickAsync();
        await Expect(Page.Locator("#overview-count")).ToHaveTextAsync("4 tickets");

        await Page.Locator("#overview-chips .nx-chiplist-chip").First.FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#overview-reset")).ToBeFocusedAsync();
    }

    [Test]
    public async Task Chips_Wrap_OnAPhone()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GoToPage("/chiplist/examples");

        var tops = await Page.Locator("#tag-chips .nx-chiplist-chip").EvaluateAllAsync<double[]>("cs => cs.map(c => c.getBoundingClientRect().top)");
        Assert.That(tops.Distinct().Count(), Is.GreaterThan(1));
        var list = (await Page.Locator("#tag-chips").BoundingBoxAsync())!;
        var longest = (await Page.Locator("#tag-chips .nx-chiplist-chip").Last.BoundingBoxAsync())!;
        Assert.That(longest.X + longest.Width, Is.LessThanOrEqualTo(list.X + list.Width + 0.5), "a long chip is cut, not overflowing");
        Assert.That(await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth"), Is.LessThanOrEqualTo(0));
    }

    // ── NxDateInput ───────────────────────────────────────────────────────────

    [Test]
    public async Task DateInput_UpDown_StepThePartUnderTheCaret()
    {
        await GoToPage("/dateinput/overview");
        var input = Page.Locator("#overview-date");

        await input.FocusAsync();
        await Expect(input).ToHaveValueAsync("10/23/2026");
        // Programmatic focus leaves the caret wherever the browser puts it; put it in the month.
        await input.EvaluateAsync("e => e.setSelectionRange(0, 0)");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: 2026-11-23");
        Assert.That(await input.EvaluateAsync<int[]>("e => [e.selectionStart, e.selectionEnd]"), Is.EqualTo(new[] { 0, 2 }), "the month stays selected");

        await Page.Keyboard.PressAsync("ArrowUp");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: 2026-01-23");

        await input.EvaluateAsync("e => e.setSelectionRange(4, 4)");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: 2026-01-22");
        await input.EvaluateAsync("e => e.setSelectionRange(9, 9)");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: 2025-01-22");
    }

    [Test]
    public async Task DateInput_FastTyping_CommitsOnBlur()
    {
        await GoToPage("/dateinput/overview");
        var input = Page.Locator("#overview-date");

        await input.FocusAsync();
        await Page.Keyboard.PressAsync("ControlOrMeta+a");
        await Page.Keyboard.PressAsync("Backspace");
        await input.PressSequentiallyAsync("02292028");
        await Expect(input).ToHaveValueAsync("02/29/2028");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator("#overview-value")).ToHaveTextAsync("Value: 2028-02-29");
        await Expect(input).ToHaveValueAsync("2/29/2028");
    }

    [Test]
    public async Task DateInput_FollowsTheCalendar_AndBack()
    {
        await GoToPage("/dateinput/examples");
        var input = Page.Locator("#calendar-input");

        await input.FillAsync("11/05/2026");
        await input.PressAsync("Enter");
        await Expect(Page.Locator("#calendar-grid [aria-selected=true]")).ToHaveTextAsync("5");

        await Page.Locator("#calendar-focus").ClickAsync();
        await Expect(input).ToBeFocusedAsync();
    }
}
